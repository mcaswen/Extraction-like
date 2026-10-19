namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树节点类别
    /// </summary>
    public enum BehaviorNodeKind
    {
        Composite = 0,
        Decorator = 1,
        Condition = 2,
        Action = 3
    }
}
