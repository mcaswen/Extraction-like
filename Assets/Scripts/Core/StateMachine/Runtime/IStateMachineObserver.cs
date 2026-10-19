namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 状态机观察者：只接收状态机本来就在做的求值和转移，不触发额外求值
    /// </summary>
    public interface IStateMachineObserver
    {
        /// <summary>本帧开始查找可触发转移，leafState 为查找起点</summary>
        void OnTransitionEvaluationStarted(StateMachineState leafState, double timeSeconds);

        /// <summary>某条转移的条件刚被求值</summary>
        void OnTransitionEvaluated(StateMachineState ownerState, StateTransition transition, bool satisfied);

        /// <summary>完成一次叶子状态切换；transition 为空表示强制切换</summary>
        void OnTransitionPerformed(
            StateMachineState fromLeaf,
            StateMachineState toLeaf,
            StateTransition transition,
            StateChangeReason reason,
            double timeSeconds);
    }
}
