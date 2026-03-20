
namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树节点的执行状态枚举定义
    /// </summary>
    public enum BehaviorNodeStatus
    {
        Success = 0,
        Failure = 1,
        Running = 2
    }
}