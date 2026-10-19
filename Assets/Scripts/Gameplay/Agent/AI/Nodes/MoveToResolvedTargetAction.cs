using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：向移动记忆里的目标点移动，到达返回成功；可选到达即结束当前指令
    /// </summary>
    public sealed class MoveToResolvedTargetAction : AgentActionNodeBase
    {
        private const string InvalidAgent = "Agent context is invalid";

        private readonly BlackboardKey _stoppingDistanceKey;
        private readonly float _defaultStoppingDistance;
        private readonly bool _finishDirectiveOnArrival;
        private readonly AgentMoveMemory _memory;

        public MoveToResolvedTargetAction(
            string nodeName,
            BlackboardKey stoppingDistanceKey,
            float defaultStoppingDistance,
            bool finishDirectiveOnArrival,
            AgentMoveMemory memory)
            : base(nodeName)
        {
            _stoppingDistanceKey = stoppingDistanceKey;
            _defaultStoppingDistance = defaultStoppingDistance;
            _finishDirectiveOnArrival = finishDirectiveOnArrival;
            _memory = memory;
            DisplayName = finishDirectiveOnArrival ? "移向目标点（到达即完成）" : "移向目标点";
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            float stoppingDistance = GetFloat(
                context,
                _stoppingDistanceKey,
                _defaultStoppingDistance);

            // 移动参数来自黑板，方便不同 Agent 使用同一节点但不同配置
            bool arrived = MoveAgentTowards(
                agent,
                _memory.TargetPosition,
                stoppingDistance,
                moveSpeed,
                context.DeltaTime);
            if (!arrived) return Running();

            if (_finishDirectiveOnArrival && agent is IAgentCommandReceiver receiver)
                receiver.FinishDirective(_memory.CommandId);
            return Succeed();
        }
    }
}
