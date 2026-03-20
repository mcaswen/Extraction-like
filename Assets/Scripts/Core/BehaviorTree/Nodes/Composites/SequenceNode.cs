using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Composites
{
    /// <summary>
    /// 序列节点类定义，不断地按照顺序执行子节点，直到遇到第一个执行失败的节点
    /// </summary>
    public sealed class SequenceNode : CompositeNode
    {
        private int _currentChildIndex;

        public SequenceNode(string nodeName, params BehaviorNode[] children)
            : base(nodeName, children)
        {
        }

        /// <summary>
        /// 进入时，从第一个子节点开始执行
        /// </summary>
        /// <param name="context"></param>
        protected override void OnEnter(BehaviorTreeContext context)
        {
            _currentChildIndex = 0;
        }

        /// <summary>
        /// 按照顺序执行子节点，直到遇到第一个执行失败的节点
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeStatus Tick(BehaviorTreeContext context)
        {
            while (_currentChildIndex < Children.Count)
            {
                BehaviorNode currentChild = Children[_currentChildIndex];
                BehaviorNodeStatus childStatus = currentChild.Execute(context);

                switch (childStatus)
                {
                    // 若成功则继续执行
                    case BehaviorNodeStatus.Success:
                        currentChild.Exit(context, BehaviorNodeStatus.Success);
                        _currentChildIndex++;
                        continue;

                    // 若失败则退出，并返回失败状态
                    case BehaviorNodeStatus.Failure:
                        currentChild.Exit(context, BehaviorNodeStatus.Failure);
                        return BehaviorNodeStatus.Failure;

                    case BehaviorNodeStatus.Running:
                        return BehaviorNodeStatus.Running;
                }
            }

            // 全部子节点成功，返回成功状态
            return BehaviorNodeStatus.Success;
        }

        /// <summary>
        /// 退出时重置当前子节点索引，可覆写
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeStatus status)
        {
            _currentChildIndex = 0;
        }

        /// <summary>
        /// 中断时重置当前子节点索引，可覆写
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAbortChildren(BehaviorTreeContext context)
        {
            _currentChildIndex = 0;
        }
    }
}