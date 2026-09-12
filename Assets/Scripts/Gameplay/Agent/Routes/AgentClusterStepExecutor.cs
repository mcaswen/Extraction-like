using System;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>只编排一个群。原 Lifecycle 拥有唯一活动/挂起指令，结果回调不重入执行。</summary>
    public sealed class AgentClusterStepExecutor : IDisposable
    {
        private readonly IAgentReadOnly _agent;
        private readonly AgentDirectiveLifecycleController _lifecycle;
        private readonly IAgentRouteTargetResolver _resolver;
        private readonly AgentNavigationQuery.Buffer _arrivalBuffer = new AgentNavigationQuery.Buffer();
        private AgentDirectiveRouteContext _context;
        private AgentClusterStepPhase _phase;
        private AgentRouteFailure _failure;
        private Vector3 _anchor;
        private string _commandId;
        private bool _final, _skipResources, _capacity, _disposed;
        private int _attempt, _failedAttempts, _alive;
        private double _nextTick, _retryAt, _waitStarted;
        private AgentDirectiveResult? _terminalResult;
        private const double RetryInterval = 0.5, WaitLimit = 5;
        private const int MaxFailedAttempts = 3;
        public AgentClusterStepSnapshot Snapshot => new AgentClusterStepSnapshot(_context, _phase, _anchor,
            _commandId, _alive, IsRetaliating, _failure);
        private bool IsRetaliating => _lifecycle.Active.HasValue && AgentManualDirectiveLock.IsCombatDamageDirective(_lifecycle.Active.Value);

        public AgentClusterStepExecutor(IAgentReadOnly agent, AgentDirectiveLifecycleController lifecycle, IAgentRouteTargetResolver resolver)
        {
            _agent = agent ?? throw new ArgumentNullException(nameof(agent));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _lifecycle.ResultPublished += OnResult;
            AgentResourceInteractionChannel.Published += OnResource;
        }

        /// <summary>新实例先接受实际移动，失败不取消旧实例拥有的步骤，供根任务原子改令。</summary>
        public bool TryBegin(AgentDirectiveRouteContext context, bool final, bool skipResources = false)
        {
            if (_disposed || _phase != AgentClusterStepPhase.None || !context.IsValid) return false;
            _context = context; _final = final; _skipResources = skipResources;
            if (!ReadFacts(out var facts)) return false;
            _anchor = facts.Anchor;
            return SubmitTravel();
        }

        public void Tick()
        {
            if (_disposed || _phase == AgentClusterStepPhase.None || Snapshot.IsTerminal) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextTick) return;
            _nextTick = now + 0.05;
            if (_agent.IsDead) { End(AgentClusterStepPhase.Failed, AgentRouteFailure.AgentUnavailable); return; }
            // 包括两个成员/步骤之间发生的反击；不能用新子指令覆盖当前有效攻击者。
            if (IsRetaliating) { _waitStarted = now; _retryAt = now; return; }
            if (_capacity) { End(AgentClusterStepPhase.Failed, AgentRouteFailure.CapacityExtraction); return; }
            if (!ReadFacts(out var facts)) return;
            if (Vector3.SqrMagnitude(facts.Anchor - _anchor) > 0.01f)
            { End(AgentClusterStepPhase.Failed, AgentRouteFailure.StaleContext); return; }

            if (_phase == AgentClusterStepPhase.Travelling)
            {
                if (!_terminalResult.HasValue) return;
                var result = ConsumeResult();
                if (result.Stage != AgentDirectiveStage.Completed) { End(AgentClusterStepPhase.Failed, MapFailure(result.Reason)); return; }
                float stop = _agent.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.MoveStoppingDistance, 2f);
                var arrival = AgentNavigationQuery.Check(_agent.NavMeshAgent, _anchor, stop, _arrivalBuffer);
                if (arrival.Status != AgentNavigationStatus.Arrived)
                {
                    if (++_failedAttempts >= MaxFailedAttempts) End(AgentClusterStepPhase.Failed, AgentRouteFailure.NoProgress);
                    else SubmitTravel();
                    return;
                }
                _phase = AgentClusterStepPhase.Processing; _failedAttempts = 0; _waitStarted = now;
            }
            // 活动成员被同伴击杀时，先看整群事实，不能由单次 Engage 失败断言整群失败。
            if (facts.Status == AgentRouteTargetStatus.Completed ||
                (facts.Kind == MapGraphNodeKind.Extraction && !_final) ||
                (facts.Kind == MapGraphNodeKind.Resource && _skipResources))
            { End(AgentClusterStepPhase.Completed); return; }
            if (facts.Status == AgentRouteTargetStatus.SpawnFailed)
            { End(AgentClusterStepPhase.Failed, AgentRouteFailure.SpawnFailed); return; }
            if (facts.Status == AgentRouteTargetStatus.WaitingSpawn)
            {
                _phase = AgentClusterStepPhase.WaitingSpawn;
                if (now - _waitStarted >= WaitLimit) End(AgentClusterStepPhase.Failed, AgentRouteFailure.SpawnFailed);
                return;
            }
            if (_terminalResult.HasValue)
            {
                var result = ConsumeResult();
                if (result.Stage == AgentDirectiveStage.Cancelled)
                { End(AgentClusterStepPhase.Cancelled, AgentRouteFailure.Superseded); return; }
                if (result.Stage == AgentDirectiveStage.Completed) _failedAttempts = 0;
                else if (++_failedAttempts >= MaxFailedAttempts)
                { End(AgentClusterStepPhase.Failed, MapFailure(result.Reason)); return; }
                _retryAt = now + RetryInterval; _phase = AgentClusterStepPhase.WaitingForMember;
            }
            if (_lifecycle.Active.HasValue && Owns(_lifecycle.Active.Value)) return;
            if (now < _retryAt) return;
            // 等待背包不受重试限时约束；没有可执行成员才启动有限重试。
            _commandId = NextCommandId();
            if (!_resolver.TryCreateProcessingDirective(_context.NodeId, _agent.AgentId, _commandId,
                    _context.IsPlayerRoute ? 1000 : 0, out var directive, out var failure))
            { RetryOrFail(now, MapFailure(failure)); return; }
            _terminalResult = null;
            var accepted = _lifecycle.SubmitRouteStep(directive.WithRouteContext(_context));
            if (!accepted.Accepted) { RetryOrFail(now, MapFailure(accepted.Reason)); return; }
            _phase = directive.DirectiveType == AgentDirectiveType.Extract ? AgentClusterStepPhase.Extracting : AgentClusterStepPhase.Processing;
        }

        private bool SubmitTravel()
        {
            _commandId = NextCommandId(); _terminalResult = null;
            var request = new AgentDirectiveRequest(AgentDirectiveType.MoveTo,
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Location, _context.NodeId, _anchor),
                targetAgentId: _agent.AgentId, commandId: _commandId, priority: _context.IsPlayerRoute ? 1000 : 0, routeContext: _context);
            var result = _lifecycle.SubmitRouteStep(request);
            if (!result.Accepted)
            { _phase = AgentClusterStepPhase.Failed; _failure = MapFailure(result.Reason); return false; }
            _phase = AgentClusterStepPhase.Travelling; return true;
        }
        private bool ReadFacts(out AgentRouteTargetFacts facts)
        {
            if (!_resolver.TryGetFacts(_context.NodeId, out facts) || facts.Status == AgentRouteTargetStatus.Missing)
            { End(AgentClusterStepPhase.Failed, AgentRouteFailure.MissingTarget); return false; }
            _alive = facts.AliveMembers;
            if (!facts.CanTraverse) { End(AgentClusterStepPhase.Failed, AgentRouteFailure.TargetUnavailable); return false; }
            return true;
        }
        private void RetryOrFail(double now, AgentRouteFailure failure)
        {
            _terminalResult = null;
            if (++_failedAttempts >= MaxFailedAttempts) { End(AgentClusterStepPhase.Failed, failure); return; }
            _phase = AgentClusterStepPhase.WaitingForMember; _retryAt = now + RetryInterval;
        }
        private bool Owns(AgentDirectiveRequest value) => value.RouteContext.Equals(_context) && value.CommandId == _commandId;
        private string NextCommandId() => "RouteStep_" + _context.RootRequestId + "_" + _context.RouteVersion + "_" + _context.StepIndex + "_" + ++_attempt;
        private void OnResult(AgentDirectiveResult result)
        {
            if (!Owns(result.Request) || Snapshot.IsTerminal) return;
            if (result.Stage == AgentDirectiveStage.Completed || result.Stage == AgentDirectiveStage.Failed || result.Stage == AgentDirectiveStage.Cancelled)
                _terminalResult = result;
        }
        private void OnResource(AgentResourceInteractionEvent value)
        {
            if (value.AgentId != _agent.AgentIdValue || value.CommandId != _commandId || Snapshot.IsTerminal) return;
            if (value.Stage == AgentResourceInteractionStage.CapacityBlocked) _capacity = true;
            else if (value.Stage == AgentResourceInteractionStage.WaitingForInventory) _phase = AgentClusterStepPhase.WaitingForInventory;
            else if (value.Stage == AgentResourceInteractionStage.Approaching || value.Stage == AgentResourceInteractionStage.Left)
                _phase = AgentClusterStepPhase.Processing;
        }
        private AgentDirectiveResult ConsumeResult() { var value = _terminalResult.Value; _terminalResult = null; return value; }
        private void End(AgentClusterStepPhase phase, AgentRouteFailure failure = AgentRouteFailure.None)
        {
            _phase = phase; _failure = failure; _terminalResult = null;
            _lifecycle.CancelRoute(_context.RootRequestId, _context.RouteVersion);
        }
        public void Cancel() { if (!Snapshot.IsTerminal) End(AgentClusterStepPhase.Cancelled, AgentRouteFailure.Superseded); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _lifecycle.ResultPublished -= OnResult; AgentResourceInteractionChannel.Published -= OnResource;
        }
        public static AgentRouteFailure MapFailure(AgentDirectiveFailure failure) => failure switch
        {
            AgentDirectiveFailure.NavigationNotReady => AgentRouteFailure.NavigationNotReady,
            AgentDirectiveFailure.Unreachable => AgentRouteFailure.Unreachable,
            AgentDirectiveFailure.NoProgress => AgentRouteFailure.NoProgress,
            AgentDirectiveFailure.AgentUnavailable => AgentRouteFailure.AgentUnavailable,
            AgentDirectiveFailure.Superseded => AgentRouteFailure.Superseded,
            _ => AgentRouteFailure.NoExecutableMember
        };
    }
}
