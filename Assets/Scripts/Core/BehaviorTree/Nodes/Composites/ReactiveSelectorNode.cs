using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Composites
{
    /// <summary>
    /// 响应式选择器（优先级回退）：每帧从第一个子节点重新执行，第一个返回成功或运行中的子节点胜出。
    /// 胜出的子节点不是上一帧运行中的那个时，在它返回之后中断上一帧运行中的子节点。
    /// </summary>
    public sealed class ReactiveSelectorNode : CompositeNode
    {
        private const string AllChildrenFailed = "All reactive selector children failed.";

        private int _runningIndex = -1;

        public ReactiveSelectorNode(string nodeName, params BehaviorNode[] children)
            : base(nodeName, children)
        {
        }

        /// <summary>上一帧返回运行中的子节点序号；没有时为 -1</summary>
        public int RunningChildIndex => _runningIndex;

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _runningIndex = -1;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            for (int index = 0; index < Children.Count; index++)
            {
                BehaviorNode child = Children[index];
                BehaviorNodeResult result = child.Execute(context);
                if (result.IsRunning)
                {
                    AbortRunningChildOtherThan(context, index);
                    _runningIndex = index;
                    return Running();
                }

                child.Exit(context, result);
                if (result.IsSuccess)
                {
                    AbortRunningChildOtherThan(context, index);
                    _runningIndex = -1;
                    return Succeed();
                }
            }

            _runningIndex = -1;
            return Fail(BehaviorFailureCode.ChildFailed, AllChildrenFailed);
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            _runningIndex = -1;
        }

        protected override void OnAbortChildren(BehaviorTreeContext context)
        {
            _runningIndex = -1;
        }

        // 已经在本帧退出的子节点再次中断是空操作
        private void AbortRunningChildOtherThan(BehaviorTreeContext context, int index)
        {
            if (_runningIndex >= 0 && _runningIndex != index)
                Children[_runningIndex].Abort(context);
        }
    }
}
