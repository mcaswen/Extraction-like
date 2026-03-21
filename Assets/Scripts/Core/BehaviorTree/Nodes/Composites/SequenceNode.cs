using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Composites
{
    /// <summary>
    /// 序列节点类定义，是按顺序组合节点：
        /// 一个子节点失败，整体失败
        /// 全成功，整体成功
        /// 某个子节点还在跑，整体继续跑
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
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            while (_currentChildIndex < Children.Count)
            {
                BehaviorNode currentChild = Children[_currentChildIndex];
                BehaviorNodeResult childResult = currentChild.Execute(context);

                // 若遇到成功的子节点，则继续执行
                if (childResult.IsSuccess)
                {
                    currentChild.Exit(context, childResult);
                    _currentChildIndex++;
                    continue;
                }

                // 若遇到失败的子节点则直接退出，并返回失败状态
                if (childResult.IsFailure)
                {
                    currentChild.Exit(context, childResult);
                    return Fail(childResult.FailureReason);
                }

                return Running();
            }

            // 全部子节点成功，返回成功状态
            return Succeed();
        }

        /// <summary>
        /// 退出时重置当前子节点索引
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult status)
        {
            _currentChildIndex = 0;
        }

        /// <summary>
        /// 中断时重置当前子节点索引
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAbortChildren(BehaviorTreeContext context)
        {
            _currentChildIndex = 0;
        }
    }
}