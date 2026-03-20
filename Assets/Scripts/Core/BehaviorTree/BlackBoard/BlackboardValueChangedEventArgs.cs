using System;

namespace Core.BehaviorTree.Blackboard
{
    /// <summary>
    /// 黑板值变化事件类定义
    /// </summary>
    public sealed class BlackboardValueChangedEventArgs : EventArgs
    {
        public BlackboardKey Key { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public int Version { get; }
        public double Timestamp { get; }

        public BlackboardValueChangedEventArgs(
            BlackboardKey key,
            object oldValue,
            object newValue,
            int version,
            double timestamp)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            Version = version;
            Timestamp = timestamp;
        }
    }
}