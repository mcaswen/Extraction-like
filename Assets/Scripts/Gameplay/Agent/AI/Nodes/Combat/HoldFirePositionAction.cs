using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：能开火时清零失视计时、记为已看到目标并停步
    /// </summary>
    public sealed class HoldFirePositionAction : AgentEngageActionBase
    {
        public HoldFirePositionAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "停步瞄准", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            Memory.LostSightSince = -1d;
            Memory.HasObservedTarget = true;
            StopAgentMovement(agent);
            return Succeed();
        }
    }
}
