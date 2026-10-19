using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 条件：智能体存在且未阵亡
    /// </summary>
    public sealed class AgentCanActCondition : ConditionNode
    {
        public AgentCanActCondition(string nodeName)
            : base(nodeName)
        {
            DisplayName = "智能体可行动";
        }

        protected override bool Evaluate(BehaviorTreeContext context)
            => AgentTreeQueries.TryGetAgent(context, out _);
    }
}
