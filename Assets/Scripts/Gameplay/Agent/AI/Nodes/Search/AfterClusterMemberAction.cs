using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：一个成员处理完后，群里没有待处理成员就完成搜索；否则清掉当前成员状态，下一帧接着找下一个（保持运行中，不退出子树）
    /// </summary>
    public sealed class AfterClusterMemberAction : AgentSearchActionBase
    {
        public AfterClusterMemberAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "成员完成后", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!Memory.Cluster.HasPendingResources(Memory.ResourceSearch))
            {
                Memory.CompleteResourceSearch(context);
                return Succeed();
            }

            Memory.ResetConcreteResourceState();
            return Running();
        }
    }
}
