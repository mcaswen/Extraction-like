using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Perception;
using UnityEngine;

namespace Gameplay.Agent.Commands
{
    /// <summary>Owns directive identity, atomic task facts and one interrupted player task or extraction.</summary>
    public sealed class AgentDirectiveLifecycleController
    {
        private readonly IAgentReadOnly _agent;
        private readonly AgentInterventionController _storage;
        private readonly AgentNavigationMotor _motor;
        private AgentDirectiveRequest? _active;
        private AgentDirectiveRequest? _suspendedDirective;
        public event System.Action<AgentDirectiveResult> ResultPublished;
        public AgentDirectiveRequest? Active => _active;
        public AgentDirectiveRequest? SuspendedDirective => _suspendedDirective;
        public AgentDirectiveRequest? SuspendedExtraction => _suspendedDirective?.DirectiveType == AgentDirectiveType.Extract
            ? _suspendedDirective : null;
        public AgentDirectiveLifecycleController(IAgentReadOnly agent, AgentInterventionController storage, AgentNavigationMotor motor)
        { _agent = agent; _storage = storage; _motor = motor; }

        public AgentDirectiveResult Submit(AgentDirectiveRequest request, bool damageInterrupt = false)
            => SubmitCore(request, damageInterrupt, false);

        /// <summary>路线编排器专用。先验证，再原子替换旧活动/挂起任务；普通入口不能解除路线保护。</summary>
        public AgentDirectiveResult SubmitRouteStep(AgentDirectiveRequest request)
            => request.RouteContext.IsValid ? SubmitCore(request, false, true)
                : Publish(WithIdentity(request), AgentDirectiveStage.Rejected, AgentDirectiveFailure.InvalidTarget);

        private AgentDirectiveResult SubmitCore(AgentDirectiveRequest request, bool damageInterrupt, bool routeReplacement)
        {
            request = WithIdentity(request);
            AgentDirectiveFailure failure = AgentDirectiveValidationService.Validate(_agent, request);
            // A known attacker may start a bounded pursuit even while navigation is rebuilding.
            if (damageInterrupt && failure == AgentDirectiveFailure.NavigationNotReady)
                failure = AgentDirectiveFailure.None;
            if (failure != AgentDirectiveFailure.None) return Publish(request, AgentDirectiveStage.Rejected, failure);
            bool manual = AgentManualDirectiveLock.IsManualDirective(request);
            if (!manual && damageInterrupt && _active.HasValue && AgentManualDirectiveLock.IsCombatDamageDirective(_active.Value))
            {
                // 连续受击不能反复重置追击，失去视线/导航失败仍由执行节点有界结束。
                if (AgentDirectiveValidationService.ValidateTarget(_active.Value) == AgentDirectiveFailure.None)
                    return new AgentDirectiveResult(_active.Value, AgentDirectiveStage.Accepted);
                // 目标可能在本帧 Tick 前失效，先统一完成和恢复，再处理这次新伤害。
                Finish(_active.Value.CommandId);
            }
            if (!manual && !damageInterrupt && !routeReplacement && _active.HasValue &&
                (AgentManualDirectiveLock.IsManualDirective(_active.Value) || AgentManualDirectiveLock.IsCombatDamageDirective(_active.Value) || _active.Value.RouteContext.IsValid))
                return Publish(request, AgentDirectiveStage.Rejected, AgentDirectiveFailure.Superseded);
            if (!manual && _active.HasValue && SameTarget(_active.Value, request))
                return new AgentDirectiveResult(_active.Value, AgentDirectiveStage.Accepted);
            if (manual || routeReplacement) DiscardSuspended(AgentDirectiveFailure.Superseded);
            if (damageInterrupt && _active.HasValue && !_suspendedDirective.HasValue &&
                (AgentManualDirectiveLock.IsManualDirective(_active.Value) || _active.Value.DirectiveType == AgentDirectiveType.Extract || _active.Value.RouteContext.IsValid))
            {
                _suspendedDirective = _active;
                Publish(_active.Value, AgentDirectiveStage.Suspended);
            }
            else if (_active.HasValue) Publish(_active.Value, AgentDirectiveStage.Cancelled, AgentDirectiveFailure.Superseded);
            Activate(request);
            return Publish(request, AgentDirectiveStage.Accepted);
        }

        public bool Finish(string commandId, AgentDirectiveFailure failure = AgentDirectiveFailure.None)
        {
            if (!_active.HasValue || _active.Value.CommandId != commandId) return false;
            AgentDirectiveRequest completed = _active.Value;
            bool resume = AgentManualDirectiveLock.IsCombatDamageDirective(completed);
            ClearActive();
            Publish(completed, failure == AgentDirectiveFailure.None ? AgentDirectiveStage.Completed : AgentDirectiveStage.Failed, failure);
            if (resume && _suspendedDirective.HasValue)
            {
                AgentDirectiveRequest suspended = _suspendedDirective.Value;
                _suspendedDirective = null;
                AgentDirectiveFailure targetFailure = AgentDirectiveValidationService.ValidateTarget(suspended);
                if (targetFailure != AgentDirectiveFailure.None && IsTargetCompletion(suspended, targetFailure))
                {
                    Publish(suspended, AgentDirectiveStage.Completed);
                    return true;
                }
                AgentDirectiveFailure validation = AgentDirectiveValidationService.Validate(_agent, suspended);
                if (validation == AgentDirectiveFailure.None) { Activate(suspended); Publish(suspended, AgentDirectiveStage.Resumed); }
                else Publish(suspended, AgentDirectiveStage.Failed, validation);
            }
            return true;
        }

        public void Cancel()
        {
            DiscardSuspended();
            if (_active.HasValue) Publish(_active.Value, AgentDirectiveStage.Cancelled);
            ClearActive();
        }

        /// <summary>结束一个根任务时，只清理它的步骤。正在执行的伤害反击仍可自行结束。</summary>
        public void CancelRoute(string rootRequestId, long routeVersion)
        {
            bool Owns(AgentDirectiveRequest value) => value.RouteContext.IsValid &&
                value.RouteContext.RootRequestId == rootRequestId && value.RouteContext.RouteVersion == routeVersion;
            if (_suspendedDirective.HasValue && Owns(_suspendedDirective.Value)) DiscardSuspended();
            if (!_active.HasValue || !Owns(_active.Value)) return;
            var cancelled = _active.Value;
            ClearActive();
            Publish(cancelled, AgentDirectiveStage.Cancelled);
        }

        public void Tick()
        {
            if (!_active.HasValue) return;
            if (_agent.IsDead) { Cancel(); return; }
            AgentDirectiveFailure failure = AgentDirectiveValidationService.ValidateTarget(_active.Value);
            if (failure != AgentDirectiveFailure.None)
            {
                Finish(_active.Value.CommandId, IsTargetCompletion(_active.Value, failure) ? AgentDirectiveFailure.None : failure);
            }
            if (_active.HasValue)
                _agent.Blackboard.SetValue(AgentBlackboardKeys.HasVisibleEnemy, IsVisible(_active.Value), Time.timeAsDouble);
        }

        private void Activate(AgentDirectiveRequest request)
        {
            _active = request;
            _storage.SubmitDirective(request, Time.timeAsDouble);
            SetTaskFacts(request);
            _motor.Reset(request.CommandId);
        }
        private static bool IsTargetCompletion(AgentDirectiveRequest request, AgentDirectiveFailure failure) =>
            request.DirectiveType == AgentDirectiveType.Engage ||
            (request.DirectiveType == AgentDirectiveType.Search && failure == AgentDirectiveFailure.TargetCompleted);
        private void ClearActive()
        {
            _active = null; _storage.ClearDirective(Time.timeAsDouble); SetTaskFacts(default); _motor.Reset(null);
        }
        private void DiscardSuspended(AgentDirectiveFailure reason = AgentDirectiveFailure.None)
        {
            if (!_suspendedDirective.HasValue) return;
            AgentDirectiveRequest discarded = _suspendedDirective.Value;
            _suspendedDirective = null;
            Publish(discarded, AgentDirectiveStage.Cancelled, reason);
        }
        private void SetTaskFacts(AgentDirectiveRequest request)
        {
            var board = _agent.Blackboard;
            double now = Time.timeAsDouble;
            board.SetValue(AgentBlackboardKeys.ShouldExtract, request.DirectiveType == AgentDirectiveType.Extract, now);
            board.SetValue(AgentBlackboardKeys.HasResourceTarget, request.DirectiveType == AgentDirectiveType.Search, now);
            board.SetValue(AgentBlackboardKeys.HasInteractableTarget, false, now);
            board.SetValue(AgentBlackboardKeys.HasEnemySourceTarget, request.TargetRef.Kind == AgentTargetKind.EnemySource, now);
            board.SetValue(AgentBlackboardKeys.HasVisibleEnemy, IsVisible(request), now);
        }
        private bool IsVisible(AgentDirectiveRequest request)
        {
            if (request.DirectiveType != AgentDirectiveType.Engage || request.TargetObject == null) return false;
            float range = _agent is AgentPawnRoot pawn ? pawn.TargetDiscoveryRange : 30f;
            return TargetVisibilityQuery.Check(_agent.CachedTransform, CombatAimPointResolver.Resolve(_agent.CachedTransform),
                request.TargetObject.transform, range) == TargetVisibilityResult.Visible;
        }
        private AgentDirectiveRequest WithIdentity(AgentDirectiveRequest request) => new AgentDirectiveRequest(
            request.DirectiveType, request.TargetRef, request.PayloadId, _agent.AgentId,
            string.IsNullOrEmpty(request.CommandId) ? "Auto_" + System.Guid.NewGuid().ToString("N") : request.CommandId, request.Priority, request.RouteContext);
        private static bool SameTarget(AgentDirectiveRequest a, AgentDirectiveRequest b) =>
            a.RouteContext.Equals(b.RouteContext) && a.DirectiveType == b.DirectiveType && a.TargetObject == b.TargetObject && a.TargetId == b.TargetId &&
            (a.TargetObject != null || a.TargetPosition == b.TargetPosition) &&
            AgentManualDirectiveLock.IsCombatDamageDirective(a) == AgentManualDirectiveLock.IsCombatDamageDirective(b);
        private AgentDirectiveResult Publish(AgentDirectiveRequest request, AgentDirectiveStage stage, AgentDirectiveFailure reason = AgentDirectiveFailure.None)
        {
            var result = new AgentDirectiveResult(request, stage, reason);
            ResultPublished?.Invoke(result);
            AgentDirectiveFeedbackChannel.Publish(result);
            return result;
        }
    }
}
