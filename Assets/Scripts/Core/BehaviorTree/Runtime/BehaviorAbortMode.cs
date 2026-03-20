
namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树节点的中断模式枚举定义
    /// </summary>
    public enum BehaviorAbortMode
    {
        None = 0,
        Self = 1,
        LowerPriority = 2,
        Both = 3
    }
}