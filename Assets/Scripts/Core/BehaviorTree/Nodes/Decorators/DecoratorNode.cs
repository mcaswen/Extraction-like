using System;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes
{
    /// <summary>
    /// 装饰节点基类定义，只包含一个子节点
    /// </summary>
    public abstract class DecoratorNode : BehaviorNode
    {
        protected readonly BehaviorNode ChildNode;

        protected DecoratorNode(string nodeName, BehaviorNode childNode)
            : base(nodeName)
        {
            ChildNode = childNode ?? throw new ArgumentNullException(nameof(childNode));
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            ChildNode.Abort(context);
        }
    }
}