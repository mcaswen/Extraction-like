namespace Core.BehaviorTree.Debugging
{
    /// <summary>
    /// 节点事件接收器；行为树上下文未设置接收器时不产生任何记录
    /// </summary>
    public interface IBehaviorNodeEventSink
    {
        void Record(in BehaviorNodeEvent nodeEvent);
    }
}
