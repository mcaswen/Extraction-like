using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 行为树节点共用的只读查询：取智能体和取当前指令
    /// </summary>
    public static class AgentTreeQueries
    {
        // UserContext 由 AgentBrainController 注入，节点不反查场景对象
        public static bool TryGetAgent(BehaviorTreeContext context, out IAgentReadOnly agent)
        {
            agent = context.UserContext as IAgentReadOnly;
            return agent != null && agent.CachedTransform != null && !agent.IsDead;
        }

        // 不消费类型不匹配的指令，避免节点误处理其他目标
        public static bool TryGetDirective(
            BehaviorTreeContext context,
            AgentDirectiveType directiveType,
            out AgentDirectiveRequest directiveRequest)
        {
            if (!context.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out directiveRequest))
            {
                return false;
            }

            return directiveRequest.DirectiveType == directiveType;
        }
    }
}
