using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Leaves
{
    /// <summary>
    /// 行为树的动作叶子节点类定义，用于执行具体的行为逻辑
    /// </summary>
    public abstract class ActionNode : BehaviorNode
    {
        protected ActionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected override abstract BehaviorNodeResult Tick(BehaviorTreeContext context);
    }
}