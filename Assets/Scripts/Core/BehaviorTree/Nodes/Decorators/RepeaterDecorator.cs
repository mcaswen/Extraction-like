using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 重复装饰节点类定义，根据指定的重复次数执行子节点
    /// </summary>
    public sealed class RepeaterDecorator : DecoratorNode
    {
        private readonly int _repeatCount;
        private int _currentCount;

        public RepeaterDecorator(string nodeName, int repeatCount, BehaviorNode childNode)
            : base(nodeName, childNode)
        {
            _repeatCount = repeatCount;
        }

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _currentCount = 0;
        }

        /// <summary>
        /// 对于每个 Tick，执行子节点直到达到指定的重复次数
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeStatus Tick(BehaviorTreeContext context)
        {
            if (_currentCount >= _repeatCount)
            {
                return BehaviorNodeStatus.Success;
            }

            BehaviorNodeStatus childStatus = ChildNode.Execute(context);

            if (childStatus == BehaviorNodeStatus.Running)
            {
                return BehaviorNodeStatus.Running;
            }

            ChildNode.Exit(context, childStatus);
            _currentCount++;

            if (_currentCount >= _repeatCount)
            {
                return BehaviorNodeStatus.Success;
            }

            return BehaviorNodeStatus.Running;
        }
    }
}