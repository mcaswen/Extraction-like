
namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 状态变化原因枚举定义，用于描述状态机状态变化的具体原因
    /// </summary>
    public enum StateChangeReason
    {
        None = 0,
        Start = 1,
        Transition = 2,
        ForceTransition = 3,
        ExternalAbort = 4
    }
}