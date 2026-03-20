using System;
using System.Collections.Generic;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes
{
    /// <summary>
    /// 复合节点基类定义，包含一个子节点列表
    /// </summary>
    public abstract class CompositeNode : BehaviorNode
    {
        // 子节点列表
        protected readonly List<BehaviorNode> Children = new List<BehaviorNode>();

        protected CompositeNode(string nodeName, params BehaviorNode[] children)
            : base(nodeName)
        {
            if (children == null || children.Length == 0)
            {
                throw new ArgumentException("Composite node must have at least one child.", nameof(children));
            }

            Children.AddRange(children);
        }

        /// <summary>
        /// 中断该节点的执行，强制退出当前节点并中断所有子节点
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAbort(BehaviorTreeContext context)
        {
            for (int index = 0; index < Children.Count; index++)
            {
                Children[index].Abort(context);
            }

            OnAbortChildren(context);
        }

        /// <summary>
        /// 供子类重写的中断虚方法，可用于处理特定的中断逻辑
        /// </summary>
        /// <param name="context"></param>

        protected virtual void OnAbortChildren(BehaviorTreeContext context) { }
    }
}