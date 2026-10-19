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

        public override BehaviorNodeKind Kind => BehaviorNodeKind.Decorator;

        public override int ChildCount => 1;

        public override BehaviorNode GetChild(int index)
            => index == 0 ? ChildNode : throw new ArgumentOutOfRangeException(nameof(index));

        protected override void OnAbort(BehaviorTreeContext context)
        {
            ChildNode.Abort(context);
        }
    }
}