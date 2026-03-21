using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 黑板条件装饰节点类定义，根据黑板中的值判断是否执行子节点
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class BlackboardConditionDecorator<T> : DecoratorNode
    {
        private readonly BlackboardKey _blackboardKey; // 用于条件判断的黑板键
        private readonly Func<T, bool> _predicate; // 用于条件判断的谓词函数
        private readonly bool _treatMissingAsFailure;

        public BlackboardConditionDecorator(
            string nodeName,
            BlackboardKey blackboardKey,
            Func<T, bool> predicate,
            BehaviorNode childNode,
            bool treatMissingAsFailure = true)
            : base(nodeName, childNode)
        {
            _blackboardKey = blackboardKey;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _treatMissingAsFailure = treatMissingAsFailure;
        }

        /// <summary>
        /// 对于每个 Tick，检查特定黑板键是否存在并满足条件，如果满足则执行子节点，否则返回失败状态
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!context.Blackboard.TryGetValue(_blackboardKey, out T value))
            {
                return _treatMissingAsFailure
                    ? Fail(
                        BehaviorFailureCode.MissingBlackboardValue,
                        $"Missing blackboard key [{_blackboardKey}].")
                    : ChildNode.Execute(context);
            }

            if (!_predicate(value))
            {
                return Fail(
                    BehaviorFailureCode.ConditionFailed,
                    $"Blackboard key [{_blackboardKey}] does not satisfy the predicate.");
            }

            BehaviorNodeResult childResult = ChildNode.Execute(context);

            if (!childResult.IsRunning)
            {
                ChildNode.Exit(context, childResult);
            }

            return childResult;
        }
    }
}