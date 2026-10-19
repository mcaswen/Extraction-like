using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：把撤离指令解析到具体撤离点（直接指向的撤离点，或撤离群里离智能体最近的点）。
    /// 只移动到群中心而没有撤离点实体，RaidFlowController 就无法计时，所以解析不到时按目标无效结束指令。
    /// </summary>
    public sealed class ResolveExtractionPointAction : AgentActionNodeBase
    {
        private const string InvalidAgent = "Agent context is invalid";
        private const string InvalidExtractionTarget = "Extraction target position is invalid";

        private readonly AgentExtractionMemory _memory;

        public ResolveExtractionPointAction(string nodeName, AgentExtractionMemory memory)
            : base(nodeName)
        {
            _memory = memory;
            DisplayName = "解析撤离点";
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);
            if (!TryGetDirective(context, AgentDirectiveType.Extract, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Extract);

            if (!TryResolveExtractionTarget(directiveRequest, agent, out Vector3 targetPosition, out global::ExtractionPointController extractionPoint))
            {
                FailPendingDirective(context, AgentDirectiveFailure.InvalidTarget);
                return Fail(BehaviorFailureCode.MissingBlackboardValue, InvalidExtractionTarget);
            }

            _memory.TargetPosition = targetPosition;
            _memory.CommandId = directiveRequest.CommandId;
            _memory.ExtractionPoint = extractionPoint;
            return Succeed();
        }

        private bool TryResolveExtractionTarget(
            AgentDirectiveRequest directiveRequest,
            IAgentReadOnly agent,
            out Vector3 targetPosition,
            out global::ExtractionPointController extractionPoint)
        {
            if (TryGetDirectExtractionPoint(directiveRequest.TargetRef, out extractionPoint))
            {
                targetPosition = extractionPoint.transform.position;
                return true;
            }

            if (TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out ExtractionClusterAuthoring extractionCluster))
            {
                if (!extractionCluster.TryGetNearestExtractionPoint(agent.Position, out extractionPoint))
                {
                    targetPosition = default;
                    return false;
                }

                targetPosition = extractionPoint.transform.position;
                return true;
            }

            targetPosition = default;
            extractionPoint = null;
            return false;
        }

        private static bool TryGetDirectExtractionPoint(
            AgentTargetRef targetRef,
            out global::ExtractionPointController extractionPoint)
        {
            extractionPoint = null;
            GameObject targetObject = targetRef.TargetObject;
            if (targetObject == null)
                return false;

            extractionPoint = targetObject.GetComponent<global::ExtractionPointController>();
            if (extractionPoint != null)
                return true;

            extractionPoint = targetObject.GetComponentInParent<global::ExtractionPointController>();
            return extractionPoint != null;
        }
    }
}
