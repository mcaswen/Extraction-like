using System;
using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>每名 Agent 唯一根路线所有者。规划、单群执行、原动作生命周期各有独立所有权。</summary>
    public sealed class AgentRouteController : IDisposable
    {
        private sealed class PlanningWork
        {
            public AgentRouteRequest Request;
            public long Version;
            public bool IsReplan;
            public int Restarts;
            public double Deadline;
            public AgentRoutePlanner Planner;
            public AgentRouteEnvironment Environment;
            public Vector3 Origin;
            public string[] EntryNodes;
            public long PreviousVersion;
            public int PreviousCursor;
        }
        private static readonly Unity.Profiling.ProfilerMarker TickMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Route.Tick");
        private readonly IAgentReadOnly _agent;
        private readonly AgentDirectiveLifecycleController _lifecycle;
        private AgentRouteEnvironment _environment;
        private AgentRouteState _state;
        private AgentClusterStepExecutor _step;
        private PlanningWork _pending;
        private long _version;
        private bool _disposed;
        private double _nextExecutionTick;
        public event Action<AgentRouteResult> ResultPublished;
        public AgentRouteSnapshot Snapshot => new AgentRouteSnapshot(_state, _step?.Snapshot ?? default, _pending?.Request);
        public long PlanningQueryCount { get; private set; }

        public AgentRouteController(IAgentReadOnly agent, AgentDirectiveLifecycleController lifecycle, AgentRouteEnvironment environment)
        { _agent = agent ?? throw new ArgumentNullException(nameof(agent)); _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle)); _environment = environment; }
        public void UpdateEnvironment(AgentRouteEnvironment environment) => _environment = environment;

        public AgentRouteResult Submit(AgentRouteRequest request)
        {
            request = request.WithTargetAgentId(_agent.AgentId);
            if (_disposed || _agent.IsDead) return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.AgentUnavailable);
            if (_environment?.Targets == null || _environment.Graph == null)
                return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.MapUnavailable);
            if (string.IsNullOrEmpty(request.RequestId) || !Enum.IsDefined(typeof(AgentRouteSource), request.Source))
                return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.InvalidRequest);
            if (string.IsNullOrEmpty(request.TargetNodeId))
            {
                if (!_environment.Targets.TryResolveNode(request.TargetRef, out var nodeId))
                    return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.MissingTarget);
                request = request.WithTargetNodeId(nodeId);
            }
            if (!_environment.Graph.TryGetNode(request.TargetNodeId, out _) ||
                !_environment.Targets.TryGetFacts(request.TargetNodeId, out var facts))
                return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.MissingTarget);
            if (!facts.CanTraverse) return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.TargetUnavailable);
            if (_pending != null && SameTarget(_pending.Request, request))
                return new AgentRouteResult(_pending.Request, _pending.Version, AgentRouteStage.Planning);
            if (_state?.IsActive == true && SameTarget(_state.Request, request) && _pending == null)
                return new AgentRouteResult(_state.Request, _state.Version, AgentRouteStage.Accepted);
            if (request.Source == AgentRouteSource.Autonomous && (_state?.IsActive == true || _pending != null || IsRetaliating))
                return Publish(request, 0, AgentRouteStage.Rejected, AgentRouteFailure.Superseded);
            RejectPending(AgentRouteFailure.Superseded);
            _pending = NewWork(request, false);
            return Publish(request, _pending.Version, AgentRouteStage.Planning);
        }

        public void Tick()
        {
            using var marker = TickMarker.Auto();
            if (_disposed) return;
            if (_agent.IsDead) { Terminate(AgentRouteStage.Dead); return; }
            if (_pending != null) AdvancePlanning();
            if (_state?.IsActive != true || _step == null || (_pending?.IsReplan ?? false)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextExecutionTick) return;
            _nextExecutionTick = now + 0.05;
            if (!ReferenceEquals(_state.Environment, _environment) || _environment?.NavigationReady != true ||
                _environment.Targets == null || _environment.TargetRevision != _environment.Targets.Revision)
            {
                if (CanContinuePlan()) _state.Environment = _environment;
                else { BeginReplan(AgentRouteFailure.StaleContext); return; }
            }
            _step.Tick();
            var step = _step.Snapshot;
            if (!step.IsTerminal) return;
            if (step.Phase == AgentClusterStepPhase.Completed)
            {
                if (IsRetaliating) return;
                if (_state.Cursor + 1 == _state.Plan.NodeIds.Count) { EndActive(AgentRouteStage.Completed); return; }
                if (!StartNextStep()) BeginReplan(_step.Snapshot.Failure);
            }
            else if (step.Failure == AgentRouteFailure.StaleContext || step.Failure == AgentRouteFailure.Unreachable ||
                step.Failure == AgentRouteFailure.NavigationNotReady || step.Failure == AgentRouteFailure.NoProgress)
                BeginReplan(step.Failure);
            else EndActive(step.Phase == AgentClusterStepPhase.Cancelled ? AgentRouteStage.Cancelled : AgentRouteStage.Failed, step.Failure);
        }

        private PlanningWork NewWork(AgentRouteRequest request, bool replan) => new PlanningWork {
            Request = request, Version = ++_version, IsReplan = replan, Deadline = Time.realtimeSinceStartupAsDouble + 8,
            EntryNodes = CaptureEntryNodes(), PreviousVersion = _state?.Version ?? 0, PreviousCursor = _state?.Cursor ?? -1
        };
        private void AdvancePlanning()
        {
            var work = _pending;
            if (Time.realtimeSinceStartupAsDouble >= work.Deadline)
            { PlanningFailed(AgentRouteFailure.NavigationNotReady); return; }
            if (_environment?.IsReady != true || !AgentNavigationQuery.IsReady(_agent.NavMeshAgent)) return;
            if (_environment.TargetRevision != _environment.Targets.Revision) return;
            if (work.IsReplan && IsRetaliating) return;
            if (work.Planner == null)
            {
                work.Environment = _environment; work.Origin = GroundPosition();
                if (!work.IsReplan) work.EntryNodes = CaptureEntryNodes();
                work.PreviousVersion = _state?.Version ?? 0; work.PreviousCursor = _state?.Cursor ?? -1;
                work.Planner = new AgentRoutePlanner(work.Environment.Graph, work.Environment.GraphRevision,
                    work.Environment.Costs, work.Environment.Targets, work.Environment.Profile,
                    work.Origin, work.Request.TargetNodeId, work.EntryNodes);
            }
            PlanningQueryCount += work.Planner.Advance(4);
            if (!work.Planner.IsDone) return;
            var plan = work.Planner.Result;
            bool stale = !SameEnvironment(work.Environment, _environment) || work.Planner.Failure == AgentRouteFailure.StaleContext ||
                plan != null && plan.BindingRevision != _environment.TargetRevision ||
                work.PreviousVersion != (_state?.Version ?? 0) || work.PreviousCursor != (_state?.Cursor ?? -1) ||
                (GroundPosition() - work.Origin).sqrMagnitude > 4f;
            if (stale)
            {
                if (++work.Restarts > 3) { PlanningFailed(AgentRouteFailure.StaleContext); return; }
                work.Planner.Cancel(); work.Planner = null; return;
            }
            if (plan == null) { PlanningFailed(work.Planner.Failure); return; }
            bool skipResources = _agent.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.InventoryRequiresExtraction) &&
                work.Environment.Graph.TryGetNode(plan.TargetNodeId, out var target) && target.NodeKind == Gameplay.MapGraph.Config.MapGraphNodeKind.Extraction;
            var next = new AgentClusterStepExecutor(_agent, _lifecycle, work.Environment.Targets);
            if (!next.TryBegin(new AgentDirectiveRouteContext(work.Request.RequestId, work.Version, plan.EntryNodeId, 0,
                work.Request.Source == AgentRouteSource.Player), plan.NodeIds.Count == 1, skipResources))
            { var failure = next.Snapshot.Failure; next.Dispose(); PlanningFailed(failure); return; }
            var previous = _state; _step?.Dispose(); _step = next;
            _state = new AgentRouteState { Request = work.Request, Version = work.Version, Plan = plan, Environment = work.Environment,
                Stage = AgentRouteStage.Accepted, SkipResources = skipResources, ReplanCount = work.IsReplan ? previous.ReplanCount : 0 };
            _pending = null;
            if (previous?.IsActive == true && !work.IsReplan)
                Publish(previous.Request, previous.Version, AgentRouteStage.Cancelled, AgentRouteFailure.Superseded);
            Publish(work.Request, work.Version, AgentRouteStage.Accepted, isReplan: work.IsReplan);
        }

        private bool StartNextStep()
        {
            int index = _state.Cursor + 1;
            string from = _state.Plan.NodeIds[index - 1], to = _state.Plan.NodeIds[index];
            if (!_environment.Graph.TryGetEdgeBetween(from, to, out var edge) ||
                !_environment.Costs.TryGetCost(edge, from, out _))
            { BeginReplan(AgentRouteFailure.Disconnected); return true; }
            var next = new AgentClusterStepExecutor(_agent, _lifecycle, _environment.Targets);
            bool accepted = next.TryBegin(new AgentDirectiveRouteContext(_state.Request.RequestId, _state.Version, to, index,
                _state.Request.Source == AgentRouteSource.Player), index == _state.Plan.NodeIds.Count - 1, _state.SkipResources);
            _step.Dispose(); _step = next;
            if (accepted) _state.Cursor = index;
            return accepted;
        }
        private void BeginReplan(AgentRouteFailure failure)
        {
            if (IsRetaliating) return;
            if (_pending != null && !_pending.IsReplan)
            { EndActive(AgentRouteStage.Failed, failure); return; }
            if (_state == null || ++_state.ReplanCount > 2)
            { EndActive(AgentRouteStage.Failed, failure); return; }
            _pending = NewWork(_state.Request, true);
            _step?.Cancel();
            Publish(_pending.Request, _pending.Version, AgentRouteStage.Planning, failure, isReplan: true);
        }
        private string[] CaptureEntryNodes()
        {
            if (_state?.IsActive != true || _state.Plan == null) return null;
            int cursor = _state.Cursor;
            string current = _state.Plan.NodeIds[cursor];
            if (cursor > 0 && _step?.Snapshot.Phase == AgentClusterStepPhase.Travelling)
                return new[] { _state.Plan.NodeIds[cursor - 1], current };
            return new[] { current };
        }
        private Vector3 GroundPosition()
        {
            var nav = _agent.NavMeshAgent;
            return nav.nextPosition - Vector3.up * nav.baseOffset * Mathf.Abs(nav.transform.lossyScale.y);
        }
        private static bool SameTarget(AgentRouteRequest a, AgentRouteRequest b) => a.TargetNodeId == b.TargetNodeId && a.Source == b.Source;
        private static bool SameEnvironment(AgentRouteEnvironment a, AgentRouteEnvironment b) => a != null && b != null &&
            a.IsReady && b.IsReady && a.ContextVersion == b.ContextVersion && a.GraphRevision == b.GraphRevision &&
            a.TargetRevision == b.TargetRevision && b.TargetRevision == b.Targets.Revision &&
            ReferenceEquals(a.Graph, b.Graph) && ReferenceEquals(a.Targets, b.Targets) && ReferenceEquals(a.Profile, b.Profile) &&
            a.Costs.Revision == b.Costs.Revision && a.Costs.ProfileId == b.Costs.ProfileId;
        // 无关成本补验不打断有效任务。仅在环境修订时检查剩余路径，不查询 NavMesh。
        private bool CanContinuePlan()
        {
            var next = _environment;
            if (next == null || !next.NavigationReady || next.Graph?.IsValid != true || next.Targets == null ||
                next.Costs == null || next.TargetRevision != next.Targets.Revision ||
                !ReferenceEquals(next.Targets, _state.Environment.Targets) ||
                next.Costs.ProfileId != _state.Environment.Costs.ProfileId) return false;
            int firstEdge = _state.Cursor;
            for (int i = _state.Cursor; i < _state.Plan.NodeIds.Count; i++)
            {
                string id = _state.Plan.NodeIds[i];
                if (!next.Graph.TryGetNode(id, out var node) || !_state.Environment.Graph.TryGetNode(id, out var oldNode) ||
                    node.NodeKind != oldNode.NodeKind || !next.Targets.TryGetFacts(id, out var facts) || !facts.CanTraverse) return false;
                if (i == _state.Cursor && (facts.Anchor - _step.Snapshot.Anchor).sqrMagnitude > 0.0001f) return false;
                if (i == firstEdge && (i == 0 || _step.Snapshot.Phase != AgentClusterStepPhase.Travelling)) continue;
                string from = _state.Plan.NodeIds[i-1];
                if (!next.Graph.TryGetEdgeBetween(from, id, out var edge) || !next.Costs.TryGetCost(edge, from, out _)) return false;
            }
            return next.TargetRevision == next.Targets.Revision;
        }
        private bool IsRetaliating => _lifecycle.Active.HasValue && AgentManualDirectiveLock.IsCombatDamageDirective(_lifecycle.Active.Value);
        private void PlanningFailed(AgentRouteFailure reason)
        {
            bool replan = _pending?.IsReplan ?? false;
            RejectPending(reason);
            if (replan) EndActive(AgentRouteStage.Failed, reason);
        }
        private void RejectPending(AgentRouteFailure reason)
        {
            if (_pending == null) return;
            var work = _pending; _pending = null; work.Planner?.Cancel();
            Publish(work.Request, work.Version, AgentRouteStage.Rejected, reason, isReplan: work.IsReplan);
        }
        private void EndActive(AgentRouteStage stage, AgentRouteFailure failure = AgentRouteFailure.None)
        {
            if (_state?.IsActive != true) return;
            _state.Stage = stage; _state.Failure = failure;
            _step?.Cancel();
            Publish(_state.Request, _state.Version, stage, failure);
        }
        public void Cancel()
        { RejectPending(AgentRouteFailure.Superseded); EndActive(AgentRouteStage.Cancelled, AgentRouteFailure.Superseded); }
        public void Terminate(AgentRouteStage stage, AgentRouteFailure failure = AgentRouteFailure.None)
        {
            if (_disposed) return;
            RejectPending(failure == AgentRouteFailure.None ? AgentRouteFailure.AgentUnavailable : failure); EndActive(stage, failure);
            _step?.Dispose(); _disposed = true;
        }
        public bool AllowsExtractionAt(GameObject point)
        {
            if (_disposed || _state?.IsActive != true || _state.Cursor != _state.Plan.NodeIds.Count - 1 ||
                _step?.Snapshot.Phase != AgentClusterStepPhase.Extracting || !_lifecycle.Active.HasValue) return false;
            var active = _lifecycle.Active.Value;
            return active.DirectiveType == AgentDirectiveType.Extract && active.TargetObject == point &&
                active.RouteContext.Equals(_step.Snapshot.Context);
        }
        public void Dispose() => Terminate(AgentRouteStage.Cancelled);
        private AgentRouteResult Publish(AgentRouteRequest request, long version, AgentRouteStage stage,
            AgentRouteFailure failure = AgentRouteFailure.None, bool isReplan = false)
        {
            var result = new AgentRouteResult(request, version, stage, failure, isReplan: isReplan);
            ResultPublished?.Invoke(result); return result;
        }
    }
}
