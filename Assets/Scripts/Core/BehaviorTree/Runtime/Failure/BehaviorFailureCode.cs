
namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 失败类型枚举
    /// 方便区分失败的来源
    /// </summary>
    public enum BehaviorFailureCode
    {
        None = 0,

        ConditionFailed = 1,
        MissingBlackboardValue = 2,
        CooldownBlocked = 3,
        Timeout = 4,
        ObserverAborted = 5,

        ChildFailed = 100,
        SubtreeFailed = 101,

        ExternalAbort = 200
    }
}