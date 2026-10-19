using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：向接近点移动，保持运行中
    /// </summary>
    public sealed class MoveToApproachPointAction : AgentEngageActionBase
    {
        public MoveToApproachPointAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "移向接近点", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            MoveAgentTowards(agent, Memory.ApproachPosition, 0.1f, Memory.MoveSpeed, context.DeltaTime);
            return Running();
        }
    }
}
