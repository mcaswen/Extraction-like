using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：移向抽象资源点；未到达时清掉等待状态，到达后停步。抽象点暂时视为到达即完成，后续由资源点适配器接管
    /// </summary>
    public sealed class MoveToAbstractPointAction : AgentSearchActionBase
    {
        public MoveToAbstractPointAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "移向抽象资源点", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            if (!MoveAgentTowards(agent, Memory.MemberTargetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                Memory.ResetWaitState();
                Memory.HasReachedInteractionRange = false;
                return Running();
            }

            Memory.HasReachedInteractionRange = true;
            StopAgentMovement(agent);
            return Succeed();
        }
    }
}
