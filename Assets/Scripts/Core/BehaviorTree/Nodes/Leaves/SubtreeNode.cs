using System;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Leaves
{
    /// <summary>
    /// 子树节点类定义，允许在主树中嵌套一个完整的子行为树，执行子树的逻辑并将结果返回给主树
    /// </summary>
    public sealed class SubtreeNode : ActionNode
    {
        private readonly Runtime.BehaviorTree _subtree;
        private BehaviorTreeRunner _subtreeRunner;

        public SubtreeNode(string nodeName, Runtime.BehaviorTree subtree)
            : base(nodeName)
        {
            _subtree = subtree ?? throw new ArgumentNullException(nameof(subtree));
        }

        /// <summary>
        /// 进入子树节点时，创建一个新的 BehaviorTreeRunner 实例来执行子树，并传入当前上下文
        /// </summary>
        /// <param name="context"></param>
        protected override void OnEnter(BehaviorTreeContext context)
        {
            _subtreeRunner = new BehaviorTreeRunner(_subtree, context);
        }

        /// <summary>
        /// 对于每个 Tick，调用子树的 Tick 方法来执行子树逻辑，并根据子树的执行结果返回状态和失败原因（如果有）
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            BehaviorNodeResult result = _subtreeRunner.Tick(context.DeltaTime, context.TimeSeconds);

            if (result.IsFailure)
            {
                return Fail(
                    new BehaviorFailureReason(
                        BehaviorFailureCode.SubtreeFailed,
                        NodeName,
                        result.FailureReason.ToString()));
            }

            return result;
        }

        /// <summary>
        /// 中断时，与主树的中断事件同步，确保子树也能正确响应中断请求
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAbort(BehaviorTreeContext context)
        {
            if (_subtreeRunner != null)
            {
                _subtreeRunner.Abort(
                    new BehaviorFailureReason(
                        BehaviorFailureCode.ExternalAbort,
                        NodeName,
                        "Parent subtree node aborted."));
            }
        }

        /// <summary>
        /// 退出时，清理子树 Runner 的引用，确保资源正确释放
        /// </summary>
        /// <param name="context"></param>
        /// <param name="result"></param>
        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            _subtreeRunner = null;
        }
    }
}