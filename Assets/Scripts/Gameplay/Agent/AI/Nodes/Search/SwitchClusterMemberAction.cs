using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：当前处理的成员变了，就清掉到达和等待状态
    /// </summary>
    public sealed class SwitchClusterMemberAction : AgentSearchActionBase
    {
        public SwitchClusterMemberAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "切换当前成员", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (Memory.ActiveConcreteResourceObject != Memory.Member)
            {
                Memory.ActiveConcreteResourceObject = Memory.Member;
                Memory.HasReachedInteractionRange = false;
                Memory.ResetWaitState();
            }

            return Succeed();
        }
    }
}
