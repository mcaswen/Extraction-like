using Core.BehaviorTree.Runtime;
using System;

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
            if (repeatCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatCount),
                    "Repeat count must be greater than zero.");
            }

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
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (_currentCount >= _repeatCount)
            {
                return Succeed();
            }

            BehaviorNodeResult childResult = ChildNode.Execute(context);

            if (childResult.IsRunning)
            {
                return Running();
            }

            ChildNode.Exit(context, childResult);
            _currentCount++;

            if (_repeatCount >= 0 && _currentCount >= _repeatCount)
            {
                return Succeed();
            }

            return Running();
        }
    }
}