using System;

namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 状态转换类定义，表示从一个状态到另一个状态的转变条件和目标状态
    /// 每条转移至少要有：名字、目标状态、条件函数和优先级
    /// </summary>
    public sealed class StateTransition
    {
        private readonly Func<StateMachineContext, bool> _condition;

        public string Name { get; }
        public StateMachineState TargetState { get; }
        public int Priority { get; }

        public StateTransition(
            string name,
            StateMachineState targetState,
            Func<StateMachineContext, bool> condition,
            int priority = 0)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Transition" : name;
            TargetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Priority = priority;
        }

        public bool CanTransition(StateMachineContext context)
        {
            return _condition(context);
        }
    }
}