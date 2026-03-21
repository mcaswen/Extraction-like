using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 观察装饰节点类定义，观察某个 Blackboard Key 的变化，并根据条件变化决定是否中断当前子节点
    /// 目前支持 Self Abort：
    /// - 子节点执行前，如果条件不满足，则直接 Failure
    /// - 子节点执行中持续观察，如果观察到条件变为不满足，则 Abort 子节点并返回 Failure
    /// </summary>
    public sealed class ObserverDecorator<T> : DecoratorNode
    {
        private readonly BlackboardKey _observedKey;
        private readonly Func<T, bool> _predicate;
        private readonly BehaviorAbortMode _abortMode;
        private readonly bool _treatMissingValueAsFailure;

        private bool _isSubscribed;
        private bool _shouldAbortSelf;
        private bool _lastConditionResult;

        public ObserverDecorator(
            string nodeName,
            BlackboardKey observedKey,
            Func<T, bool> predicate,
            BehaviorNode childNode,
            BehaviorAbortMode abortMode = BehaviorAbortMode.Self,
            bool treatMissingValueAsFailure = true)
            : base(nodeName, childNode)
        {
            _observedKey = observedKey;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _abortMode = abortMode;
            _treatMissingValueAsFailure = treatMissingValueAsFailure;
        }

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _shouldAbortSelf = false;
            _lastConditionResult = EvaluateCurrentCondition(context);

            Subscribe(context);
        }

        /// <summary>
        /// 对于每个 Tick，首先评估当前条件，并在子节点执行过程中持续观察条件变化以决定是否中断
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            bool currentConditionResult = EvaluateCurrentCondition(context);

            if (!currentConditionResult)
            {
                if (_shouldAbortSelf || _abortMode == BehaviorAbortMode.Self || _abortMode == BehaviorAbortMode.Both)
                {
                    ChildNode.Abort(context);
                }

                _lastConditionResult = false;
                _shouldAbortSelf = false;
                
                return Fail(
                    BehaviorFailureCode.ObserverAborted,
                    $"Observed blackboard key [{_observedKey}] no longer satisfies the predicate.");
            }

            if (_shouldAbortSelf && (_abortMode == BehaviorAbortMode.Self || _abortMode == BehaviorAbortMode.Both))
            {
                ChildNode.Abort(context);
                
                _shouldAbortSelf = false;
                _lastConditionResult = currentConditionResult;
                
                return Fail(
                    BehaviorFailureCode.ObserverAborted,
                    $"Observed blackboard key [{_observedKey}] changed and triggered self-abort.");
            }

            BehaviorNodeResult childResult = ChildNode.Execute(context);

            if (!childResult.IsRunning)
            {
                ChildNode.Exit(context, childResult);
            }

            _lastConditionResult = currentConditionResult;
            return childResult;
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            Unsubscribe(context);
            _shouldAbortSelf = false;
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            base.OnAbort(context);
            Unsubscribe(context);
            _shouldAbortSelf = false;
        }

        private bool EvaluateCurrentCondition(BehaviorTreeContext context)
        {
            if (!context.Blackboard.TryGetValue(_observedKey, out T value))
            {
                return !_treatMissingValueAsFailure;
            }

            return _predicate(value);
        }

        // 订阅黑板值变化事件
        private void Subscribe(BehaviorTreeContext context)
        {
            if (_isSubscribed)
                return;

            context.Blackboard.ValueChanged += OnBlackboardValueChanged;
            _isSubscribed = true;
        }

        // 取消订阅黑板值变化事件
        private void Unsubscribe(BehaviorTreeContext context)
        {
            if (!_isSubscribed)
                return;

            context.Blackboard.ValueChanged -= OnBlackboardValueChanged;
            _isSubscribed = false;
        }

        // 处理黑板值变化事件，根据条件变化决定是否中断当前子节点
        private void OnBlackboardValueChanged(object sender, BlackboardValueChangedEventArgs eventArgs)
        {
            if (!eventArgs.Key.Equals(_observedKey))
                return;

            bool nextConditionResult = TryEvaluateFromEvent(eventArgs);

            if (_lastConditionResult && !nextConditionResult)
            {
                _shouldAbortSelf = true;
            }

            _lastConditionResult = nextConditionResult;
        }

        // 尝试从事件参数中评估条件，避免每次变化都访问黑板
        private bool TryEvaluateFromEvent(BlackboardValueChangedEventArgs eventArgs)
        {
            if (eventArgs.NewValue == null)
            {
                return !_treatMissingValueAsFailure;
            }

            if (eventArgs.NewValue is T typedValue)
            {
                return _predicate(typedValue);
            }

            return !_treatMissingValueAsFailure;
        }
    }
}