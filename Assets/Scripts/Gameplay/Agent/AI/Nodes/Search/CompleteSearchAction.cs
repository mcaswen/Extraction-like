using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：本次搜索完成：播放一次完成音效、清资源目标事实、释放指令
    /// </summary>
    public sealed class CompleteSearchAction : AgentSearchActionBase
    {
        public CompleteSearchAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "完成搜索", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            Memory.CompleteResourceSearch(context);
            return Succeed();
        }
    }
}
