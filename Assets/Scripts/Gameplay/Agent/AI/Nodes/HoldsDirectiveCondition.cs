using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 条件：当前指令是指定类型，且目标类型匹配（目标类型为 None 时不限）
    /// </summary>
    public sealed class HoldsDirectiveCondition : ConditionNode
    {
        private readonly AgentDirectiveType _directiveType;
        private readonly AgentTargetKind _targetKind;

        public HoldsDirectiveCondition(string nodeName, AgentDirectiveType directiveType, AgentTargetKind targetKind)
            : base(nodeName)
        {
            _directiveType = directiveType;
            _targetKind = targetKind;
            DisplayName = "持有" + AgentNodeText.Directive(directiveType) + "指令";
        }

        protected override bool Evaluate(BehaviorTreeContext context)
        {
            if (!AgentTreeQueries.TryGetDirective(context, _directiveType, out AgentDirectiveRequest request))
                return false;

            return _targetKind == AgentTargetKind.None || request.TargetRef.Kind == _targetKind;
        }
    }
}
