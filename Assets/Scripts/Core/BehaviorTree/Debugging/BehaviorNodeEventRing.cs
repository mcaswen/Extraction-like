using System;

namespace Core.BehaviorTree.Debugging
{
    /// <summary>
    /// 定长环形缓冲的节点事件接收器；满后覆盖最旧记录，记录和读取都不分配内存
    /// </summary>
    public sealed class BehaviorNodeEventRing : IBehaviorNodeEventSink
    {
        private readonly BehaviorNodeEvent[] _events;
        private int _next;

        public BehaviorNodeEventRing(int capacity = 512)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _events = new BehaviorNodeEvent[capacity];
        }

        public int Capacity => _events.Length;

        public int Count { get; private set; }

        /// <summary>累计写入次数，用于读者判断自上次读取以来新增了多少条</summary>
        public long TotalRecorded { get; private set; }

        public void Record(in BehaviorNodeEvent nodeEvent)
        {
            _events[_next] = nodeEvent;
            _next = (_next + 1) % _events.Length;
            if (Count < _events.Length)
                Count++;
            TotalRecorded++;
        }

        /// <summary>按时间顺序取第 index 条（0 为仍保留的最旧记录）</summary>
        public BehaviorNodeEvent Get(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            int start = (_next - Count + _events.Length) % _events.Length;
            return _events[(start + index) % _events.Length];
        }

        public void Clear()
        {
            Count = 0;
            _next = 0;
        }
    }
}
