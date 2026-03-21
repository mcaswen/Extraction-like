using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Leaves
{
    /// <summary>
    /// 行为树的条件叶子节点类定义，用于执行状态条件判断
    /// </summary>
    public abstract class ConditionNode : BehaviorNode
    {
        protected ConditionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected sealed override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            return Evaluate(context)
                ? Succeed()
                : Fail(BehaviorFailureCode.ConditionFailed, "Condition evaluated to false.");
        }

        protected abstract bool Evaluate(BehaviorTreeContext context);
    }
}