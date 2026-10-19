using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Composites
{
    /// <summary>
    /// 响应式序列：每帧从第一个子节点重新执行，前面的条件变化本帧即生效。
    /// 子节点返回运行中或失败时，中断上一帧仍在运行、本帧没有轮到的子节点；中断发生在该子节点返回之后。
    /// </summary>
    public sealed class ReactiveSequenceNode : CompositeNode
    {
        private int _runningIndex = -1;

        public ReactiveSequenceNode(string nodeName, params BehaviorNode[] children)
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
                if (result.IsFailure)
                {
                    AbortRunningChildOtherThan(context, index);
                    _runningIndex = -1;
                    return Fail(result.FailureReason);
                }
            }

            _runningIndex = -1;
            return Succeed();
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
