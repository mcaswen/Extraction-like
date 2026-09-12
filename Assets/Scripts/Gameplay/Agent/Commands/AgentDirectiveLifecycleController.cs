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
        public AgentDirectiveRequest? Active => _active;
        public AgentDirectiveRequest? SuspendedDirective => _suspendedDirective;
        public AgentDirectiveRequest? SuspendedExtraction => _suspendedDirective?.DirectiveType == AgentDirectiveType.Extract
            ? _suspendedDirective : null;
        public AgentDirectiveLifecycleController(IAgentReadOnly agent, AgentInterventionController storage, AgentNavigationMotor motor)
        { _agent = agent; _storage = storage; _motor = motor; }

        public AgentDirectiveResult Submit(AgentDirectiveRequest request, bool damageInterrupt = false)
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
            if (!manual && !damageInterrupt && _active.HasValue &&
                (AgentManualDirectiveLock.IsManualDirective(_active.Value) || AgentManualDirectiveLock.IsCombatDamageDirective(_active.Value)))
                return Publish(request, AgentDirectiveStage.Rejected, AgentDirectiveFailure.Superseded);
            if (!manual && _active.HasValue && SameTarget(_active.Value, request))
                return new AgentDirectiveResult(_active.Value, AgentDirectiveStage.Accepted);
            if (manual) DiscardSuspended(AgentDirectiveFailure.Superseded);
            if (damageInterrupt && _active.HasValue && !_suspendedDirective.HasValue &&
                (AgentManualDirectiveLock.IsManualDirective(_active.Value) || _active.Value.DirectiveType == AgentDirectiveType.Extract))
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

        public void Tick()
        {
            if (!_active.HasValue) return;
            if (_agent.IsDead) { Cancel(); return; }
            AgentDirectiveFailure failure = AgentDirectiveValidationService.ValidateTarget(_active.Value);
            if (failure != AgentDirectiveFailure.None)
            {
                bool completed = _active.Value.DirectiveType == AgentDirectiveType.Engage ||
                    (_active.Value.DirectiveType == AgentDirectiveType.Search && failure == AgentDirectiveFailure.TargetCompleted);
                Finish(_active.Value.CommandId, completed ? AgentDirectiveFailure.None : failure);
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
            string.IsNullOrEmpty(request.CommandId) ? "Auto_" + System.Guid.NewGuid().ToString("N") : request.CommandId, request.Priority);
        private static bool SameTarget(AgentDirectiveRequest a, AgentDirectiveRequest b) =>
            a.DirectiveType == b.DirectiveType && a.TargetObject == b.TargetObject && a.TargetId == b.TargetId &&
            (a.TargetObject != null || a.TargetPosition == b.TargetPosition) &&
            AgentManualDirectiveLock.IsCombatDamageDirective(a) == AgentManualDirectiveLock.IsCombatDamageDirective(b);
        private static AgentDirectiveResult Publish(AgentDirectiveRequest request, AgentDirectiveStage stage, AgentDirectiveFailure reason = AgentDirectiveFailure.None)
        {
            var result = new AgentDirectiveResult(request, stage, reason);
            AgentDirectiveFeedbackChannel.Publish(result);
            return result;
        }
    }
}
