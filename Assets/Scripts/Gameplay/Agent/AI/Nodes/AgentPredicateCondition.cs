using System;
using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 条件：读取树记忆或黑板的纯判断；谓词在构建时创建，推进时不分配
    /// </summary>
    public sealed class AgentPredicateCondition : ConditionNode
    {
        private readonly Func<BehaviorTreeContext, bool> _predicate;

        public AgentPredicateCondition(string nodeName, string displayName, Func<BehaviorTreeContext, bool> predicate)
            : base(nodeName)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            DisplayName = displayName;
        }

        protected override bool Evaluate(BehaviorTreeContext context) => _predicate(context);
    }
}
