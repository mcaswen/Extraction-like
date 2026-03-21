using System;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 超时装饰节点类定义，根据指定的超时时间执行子节点，如果子节点在超时时间内未完成，则返回失败
    /// 主要是为了超时就回退/防卡死的业务逻辑
    /// </summary>
    public sealed class TimeoutDecorator : DecoratorNode
    {
        private readonly float _timeoutSeconds;

        private double _deadlineTime;
        private bool _hasTimedOut;

        public TimeoutDecorator(string nodeName, float timeoutSeconds, BehaviorNode childNode)
            : base(nodeName, childNode)
        {
            if (timeoutSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    "Timeout seconds must be greater than zero.");
            }

            _timeoutSeconds = timeoutSeconds;
        }

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _deadlineTime = context.TimeSeconds + _timeoutSeconds;
            _hasTimedOut = false;
        }


        /// <summary>
        /// 对于每个 Tick，检查当前时间是否超过截止时间，如果超时则中止子节点并返回失败，否则继续执行子节点
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (_hasTimedOut)
            {
                return Fail(
                    BehaviorFailureCode.Timeout,
                    $"Child node exceeded timeout of {_timeoutSeconds:F2}s.");
            }

            if (context.TimeSeconds >= _deadlineTime)
            {
                _hasTimedOut = true;
                ChildNode.Abort(context);
                context.LogNodeEvent(NodeName, $"Timed out after {_timeoutSeconds:F2}s");

                return Fail(
                    BehaviorFailureCode.Timeout,
                    $"Child node exceeded timeout of {_timeoutSeconds:F2}s.");
            }

            BehaviorNodeResult childResult = ChildNode.Execute(context);

            if (!childResult.IsRunning)
            {
                ChildNode.Exit(context, childResult);
            }

            return childResult;
        }

        /// <summary>
        /// 退出时重置状态
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            _hasTimedOut = false;
            _deadlineTime = 0d;
        }

        /// <summary>
        /// 中断时重置状态
        /// </summary>
        /// <param name="context"></param>
        protected override void OnAbort(BehaviorTreeContext context)
        {
            base.OnAbort(context);
            _hasTimedOut = false;
            _deadlineTime = 0d;
        }
    }
}