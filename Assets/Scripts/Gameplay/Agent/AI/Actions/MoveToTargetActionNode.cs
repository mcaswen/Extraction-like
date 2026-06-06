using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 将 Agent 移动到当前指令目标附近
    /// </summary>
    public sealed class MoveToTargetActionNode : AgentActionNodeBase
    {
        private readonly AgentDirectiveType _directiveType;
        private readonly AgentTargetKind _targetKind;
        private readonly BlackboardKey _stoppingDistanceKey;
        private readonly float _defaultStoppingDistance;

        /// <summary>
        /// 创建移动到指令目标的行为节点
        /// </summary>
        /// <param name="nodeName"></param>
        /// <param name="directiveType"></param>
        /// <param name="targetKind"></param>
        /// <param name="stoppingDistanceKey"></param>
        /// <param name="defaultStoppingDistance"></param>
        public MoveToTargetActionNode(
            string nodeName,
            AgentDirectiveType directiveType,
            AgentTargetKind targetKind,
            BlackboardKey stoppingDistanceKey,
            float defaultStoppingDistance)
            : base(nodeName)
        {
            _directiveType = directiveType;
            _targetKind = targetKind;
            _stoppingDistanceKey = stoppingDistanceKey;
            _defaultStoppingDistance = defaultStoppingDistance;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, "Agent context is invalid");

            if (!TryGetDirective(context, _directiveType, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(_directiveType);

            AgentTargetRef targetRef = directiveRequest.TargetRef;
            if (_targetKind != AgentTargetKind.None && targetRef.Kind != _targetKind)
            {
                // 目标类型不匹配时失败，让状态机等待正确指令而不是误移动
                return Fail(
                    BehaviorFailureCode.ConditionFailed,
                    $"Target kind [{targetRef.Kind}] does not match [{_targetKind}]");
            }

            UnityEngine.Vector3 targetPosition;
            bool resolvedTargetPosition = _targetKind == AgentTargetKind.Resource
                ? TryResolveResourceNavigationTargetPosition(targetRef, agent.Position, agent.NavMeshAgent, out targetPosition)
                : TryResolveTargetPosition(targetRef, out targetPosition);

            if (!resolvedTargetPosition)
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Directive target position is invalid");

            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            float stoppingDistance = GetFloat(
                context,
                _stoppingDistanceKey,
                _defaultStoppingDistance);

            // 移动参数来自黑板，方便不同 Agent 使用同一节点但不同配置
            return MoveAgentTowards(
                agent,
                targetPosition,
                stoppingDistance,
                moveSpeed,
                context.DeltaTime)
                ? Succeed()
                : Running();
        }
    }
}
