using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Tracks player-issued target commands so autonomous target refresh cannot replace the current task.
    /// </summary>
    public static class AgentManualDirectiveLock
    {
        public const string CommandIdPrefix = "ManualTargetClick";
        public const int ManualDirectivePriority = 1000;

        public static string CreateCommandId(string targetId)
        {
            return string.IsNullOrWhiteSpace(targetId)
                ? $"{CommandIdPrefix}_{Time.frameCount}"
                : $"{CommandIdPrefix}_{targetId.Trim()}_{Time.frameCount}";
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

            return !IsManualTargetCompleted(directiveRequest);
        }

        public static bool IsManualDirective(AgentDirectiveRequest directiveRequest)
        {
            return directiveRequest.Priority >= ManualDirectivePriority &&
                   !string.IsNullOrWhiteSpace(directiveRequest.CommandId) &&
                   directiveRequest.CommandId.StartsWith(
                       CommandIdPrefix,
                       System.StringComparison.Ordinal);
        }

        private static bool IsManualTargetCompleted(AgentDirectiveRequest directiveRequest)
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
