using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Commands;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 让 Agent 保持在撤离点范围内，并把进入状态桥接给 RaidFlowController
    /// </summary>
    public sealed class ExtractActionNode : AgentActionNodeBase
    {
        private global::ExtractionPointController _activeExtractionPoint;
        private string _activeAgentId;

        /// <summary>
        /// 创建撤离行为节点
        /// </summary>
        /// <param name="nodeName"></param>
        public ExtractActionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, "Agent context is invalid");

            if (!TryGetDirective(context, AgentDirectiveType.Extract, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Extract);

            if (!TryResolveExtractionTarget(
                    directiveRequest,
                    agent,
                    out Vector3 targetPosition,
                    out global::ExtractionPointController extractionPoint))
            {
                FailPendingDirective(context, AgentDirectiveFailure.InvalidTarget);
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Extraction target position is invalid");
            }

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            if (!MoveAgentTowards(agent, targetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                ClearActiveExtractionPoint();
                return Running();
            }

            // 现有撤离逻辑由 RaidFlowController 计时，这里只桥接进入状态
            SetActiveExtractionPoint(agent, extractionPoint);
            return Running();
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            // 状态退出时必须通知离开，避免 RaidFlowController 保留旧撤离点
            ClearActiveExtractionPoint();
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            // 中断同样要收尾撤离状态，防止切目标后继续倒计时
            ClearActiveExtractionPoint();
        }

        // 撤离必须解析到具体撤离点，避免只移动到 cluster 中心但没有 RaidFlow 计时实体。
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

        private void SetActiveExtractionPoint(
            IAgentReadOnly agent,
            global::ExtractionPointController extractionPoint)
        {
            string agentId = agent != null ? agent.AgentIdValue : string.Empty;
            if (_activeExtractionPoint == extractionPoint &&
                string.Equals(_activeAgentId, agentId, System.StringComparison.Ordinal))
            {
                // 每帧续写 true，兼容 RaidFlowController 的持续计时模型
                global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                    agentId,
                    extractionPoint,
                    true);
                return;
            }

            // 切换撤离点前先清旧点，避免两个撤离点同时处于激活状态
            ClearActiveExtractionPoint();
            _activeExtractionPoint = extractionPoint;
            _activeAgentId = agentId;
            global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                _activeAgentId,
                _activeExtractionPoint,
                true);
        }

        private void ClearActiveExtractionPoint()
        {
            if (_activeExtractionPoint == null)
                return;

            global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                _activeAgentId,
                _activeExtractionPoint,
                false);
            _activeExtractionPoint = null;
            _activeAgentId = string.Empty;
        }
    }
}
