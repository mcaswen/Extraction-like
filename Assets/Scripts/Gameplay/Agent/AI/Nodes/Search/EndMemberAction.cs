using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：当前成员没有可处理的内容（对象已不存在，或既不是箱子也不是地面物品）；直接指向的资源则完成整条指令
    /// </summary>
    public sealed class EndMemberAction : AgentSearchActionBase
    {
        public EndMemberAction(string nodeName, string displayName, AgentSearchMemory memory)
            : base(nodeName, displayName, memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (Memory.ClearDirectiveOnComplete)
                Memory.CompleteResourceSearch(context);
            return Succeed();
        }
    }
}
