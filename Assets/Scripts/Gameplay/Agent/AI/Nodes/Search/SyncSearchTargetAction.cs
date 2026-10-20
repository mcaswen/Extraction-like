using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Targets.Authoring;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：同步当前搜索目标（目标切换时清掉旧的到达和等待状态），并判定目标形态：资源群、抽象点或具体对象
    /// </summary>
    public sealed class SyncSearchTargetAction : AgentSearchActionBase
    {
        public SyncSearchTargetAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "同步搜索目标", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Search);

            Memory.SyncActiveResourceTarget(directiveRequest);
            if (TryGetTargetComponent(directiveRequest.TargetRef, out ResourceClusterAuthoring resourceCluster))
            {
                Memory.Form = AgentSearchTargetForm.Cluster;
                Memory.Cluster = resourceCluster;
            }
            else
            {
                Memory.Form = directiveRequest.TargetObject == null ? AgentSearchTargetForm.AbstractPoint : AgentSearchTargetForm.ConcreteObject;
                Memory.Cluster = null;
            }

            return Succeed();
        }
    }
}
