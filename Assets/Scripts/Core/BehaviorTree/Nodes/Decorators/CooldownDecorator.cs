using Core.BehaviorTree.Runtime;
using System;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 冷却装饰节点类定义，在子节点执行完毕后进入冷却状态，在冷却期间子节点无法执行
    /// </summary>
    public sealed class CooldownDecorator : DecoratorNode
    {
        private readonly float _cooldownSeconds;
        private double _nextAvailableTime;

        public CooldownDecorator(string nodeName, float cooldownSeconds, BehaviorNode childNode)
            : base(nodeName, childNode)
        {
            if (cooldownSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cooldownSeconds),
                    "Cooldown seconds must be greater than zero.");
            }

            _cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// 对于每个 Tick，根据时间戳判断节点的可用状态，并执行子节点，完毕后重新进入冷却状态
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (context.TimeSeconds < _nextAvailableTime)
            {
                return Fail(
                    BehaviorFailureCode.CooldownBlocked,
                    $"Node is cooling down until {_nextAvailableTime:F3}.");
            }

            BehaviorNodeResult childResult = ChildNode.Execute(context);

            if (!childResult.IsRunning)
            {
                ChildNode.Exit(context, childResult);
                _nextAvailableTime = context.TimeSeconds + _cooldownSeconds;
            }

            return childResult;
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            base.OnAbort(context);
        }
    }
}