using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：指令直接指向某个箱子或地面物品时，把它作为当前成员；处理完即完成整条指令
    /// </summary>
    public sealed class SelectConcreteResourceAction : AgentSearchActionBase
    {
        public SelectConcreteResourceAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "指定具体资源", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Search);

            Memory.Member = directiveRequest.TargetObject;
            Memory.HasMemberNavigationPosition = false;
            Memory.ClearDirectiveOnComplete = true;
            return Succeed();
        }
    }
}
