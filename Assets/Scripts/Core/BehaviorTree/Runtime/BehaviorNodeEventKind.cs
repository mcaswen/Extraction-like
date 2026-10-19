namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 节点生命周期事件类别
    /// </summary>
    public enum BehaviorNodeEventKind
    {
        Enter = 0,
        Tick = 1,
        Exit = 2,
        Abort = 3
    }
}
