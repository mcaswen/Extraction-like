using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Tracks high-priority directives so autonomous target refresh cannot replace the current task.
    /// </summary>
    public static class AgentManualDirectiveLock
    {
        public const string CommandIdPrefix = "ManualTargetClick";
        public const int ManualDirectivePriority = 1000;
        public const string CombatDamageCommandIdPrefix = "CombatDamageInterrupt";
        public const int CombatDamageDirectivePriority = 900;

        public static string CreateCommandId(string targetId)
        {
            return string.IsNullOrWhiteSpace(targetId)
                ? $"{CommandIdPrefix}_{System.Guid.NewGuid():N}"
                : $"{CommandIdPrefix}_{targetId.Trim()}_{System.Guid.NewGuid():N}";
        }

        public static string CreateCombatDamageCommandId(UnityEngine.Object source)
        {
            int sourceId = source != null ? source.GetInstanceID() : 0;
            return $"{CombatDamageCommandIdPrefix}_{sourceId}_{System.Guid.NewGuid():N}";
        }

        public static bool ShouldHoldManualDirective(IAgentReadOnly agent)
        {
            if (agent == null ||
                agent.Blackboard == null ||
                !agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest) ||
                !IsManualDirective(directiveRequest))
            {
                return false;
            }

            // 生命周期拥有完成和恢复撤离的顺序，发现器不能在其清理请求前抢先解锁。
            return true;
        }

        public static bool ShouldHoldCombatDamageDirective(IAgentReadOnly agent)
        {
            if (agent == null ||
                agent.Blackboard == null ||
                !agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest) ||
                !IsCombatDamageDirective(directiveRequest))
            {
                return false;
            }

            return true;
        }

        public static bool IsManualDirective(AgentDirectiveRequest directiveRequest)
        {
            return directiveRequest.Priority >= ManualDirectivePriority &&
                   !string.IsNullOrWhiteSpace(directiveRequest.CommandId) &&
                   directiveRequest.CommandId.StartsWith(
                       CommandIdPrefix,
                       System.StringComparison.Ordinal);
        }

        public static bool IsManualResourceDirective(AgentDirectiveRequest directiveRequest)
        {
            return IsManualDirective(directiveRequest) &&
                   directiveRequest.DirectiveType == AgentDirectiveType.Search &&
                   directiveRequest.TargetRef.Kind == AgentTargetKind.Resource;
        }

        public static bool ShouldHoldManualResourceDirective(AgentDirectiveRequest directiveRequest)
        {
            return IsManualResourceDirective(directiveRequest) &&
                   !IsDirectiveTargetCompleted(directiveRequest);
        }

        public static bool IsCombatDamageDirective(AgentDirectiveRequest directiveRequest)
        {
            return directiveRequest.Priority >= CombatDamageDirectivePriority &&
                   directiveRequest.DirectiveType == AgentDirectiveType.Engage &&
                   directiveRequest.TargetRef.Kind == AgentTargetKind.Enemy &&
                   !string.IsNullOrWhiteSpace(directiveRequest.CommandId) &&
                   directiveRequest.CommandId.StartsWith(
                       CombatDamageCommandIdPrefix,
                       System.StringComparison.Ordinal);
        }

        private static bool IsDirectiveTargetCompleted(AgentDirectiveRequest directiveRequest)
        {
            if (!string.IsNullOrWhiteSpace(directiveRequest.TargetId) &&
                GameplayTargetRegistry.ActiveInstance != null &&
                GameplayTargetRegistry.ActiveInstance.TryGetTarget(
                    directiveRequest.TargetId,
                    out GameplayTargetAuthoringBase target))
            {
                return target.HasBeenCompleted;
            }

            GameObject targetObject = directiveRequest.TargetObject;
            if (directiveRequest.TargetRef.IsConcreteObject && targetObject == null)
                return true;

            if (targetObject == null)
                return false;

            if (TryGetTargetAuthoring(targetObject, out GameplayTargetAuthoringBase authoringTarget))
                return authoringTarget.HasBeenCompleted;

            if (targetObject.TryGetComponent(out global::EnemyHealthController enemy))
                return !enemy.IsAlive;

            return false;
        }

        private static bool TryGetTargetAuthoring(
            GameObject targetObject,
            out GameplayTargetAuthoringBase target)
        {
            target = targetObject.GetComponent<GameplayTargetAuthoringBase>();
            if (target != null)
                return true;

            target = targetObject.GetComponentInParent<GameplayTargetAuthoringBase>();
            if (target != null)
                return true;

            target = targetObject.GetComponentInChildren<GameplayTargetAuthoringBase>();
            return target != null;
        }
    }
}
