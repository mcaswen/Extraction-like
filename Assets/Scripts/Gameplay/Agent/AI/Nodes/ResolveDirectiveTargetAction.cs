using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：把当前指令解析成本帧的移动目标点，写入移动记忆。
    /// 资源取最近的未处理成员，撤离取最近的撤离点，其余取目标位置；解析失败时按目标无效结束指令。
    /// </summary>
    public sealed class ResolveDirectiveTargetAction : AgentActionNodeBase
    {
        private const string InvalidAgent = "Agent context is invalid";
        private const string InvalidTargetPosition = "Directive target position is invalid";

        private readonly AgentDirectiveType _directiveType;
        private readonly AgentTargetKind _targetKind;
        private readonly AgentMoveMemory _memory;

        public ResolveDirectiveTargetAction(
            string nodeName,
            AgentDirectiveType directiveType,
            AgentTargetKind targetKind,
            AgentMoveMemory memory)
            : base(nodeName)
        {
            _directiveType = directiveType;
            _targetKind = targetKind;
            _memory = memory;
            DisplayName = "解析" + AgentNodeText.Target(targetKind) + "目标点";
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);
            if (!TryGetDirective(context, _directiveType, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(_directiveType);

            AgentTargetRef targetRef = directiveRequest.TargetRef;
            Vector3 targetPosition;
            bool resolvedTargetPosition;
            switch (_targetKind)
            {
                case AgentTargetKind.Resource:
                    resolvedTargetPosition = TryResolveResourceNavigationTargetPosition(
                        targetRef,
                        agent.Position,
                        agent.NavMeshAgent,
                        out targetPosition, directiveRequest.ResourceSearch);
                    break;
                case AgentTargetKind.Extraction:
                    resolvedTargetPosition = TryResolveExtractionNavigationTargetPosition(
                        targetRef,
                        agent.Position,
                        out targetPosition);
                    break;
                default:
                    resolvedTargetPosition = TryResolveTargetPosition(targetRef, out targetPosition);
                    break;
            }

            if (!resolvedTargetPosition)
            {
                // 资源查询会刷新聚合状态，队友可能已在生命周期检查后耗尽最后一个成员。
                if (_directiveType == AgentDirectiveType.Search &&
                    AgentDirectiveValidationService.ValidateTarget(directiveRequest) == AgentDirectiveFailure.TargetCompleted)
                {
                    ClearPendingDirective(context);
                    return Running(); // 等待宏状态切换，不让本帧后续节点消费已释放的指令。
                }

                FailPendingDirective(context, AgentDirectiveFailure.InvalidTarget);
                return Fail(BehaviorFailureCode.MissingBlackboardValue, InvalidTargetPosition);
            }

            _memory.TargetPosition = targetPosition;
            _memory.CommandId = directiveRequest.CommandId;
            return Succeed();
        }
    }
}
