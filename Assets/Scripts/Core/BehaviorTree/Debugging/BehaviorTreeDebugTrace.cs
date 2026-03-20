using System.Collections.Generic;

namespace Core.BehaviorTree.Debugging
{
    /// <summary>
    /// 行为树调试跟踪类定义，包含一个调试记录列表相关记录逻辑
    /// </summary>
    public sealed class BehaviorTreeDebugTrace
    {
        private readonly List<BehaviorTreeDebugRecord> _records = new List<BehaviorTreeDebugRecord>();
        private readonly int _maxRecordCount;

        public BehaviorTreeDebugTrace(int maxRecordCount = 256)
        {
            _maxRecordCount = maxRecordCount;
        }

        public IReadOnlyList<BehaviorTreeDebugRecord> Records => _records;

        /// <summary>
        /// 添加一条新的调试记录，如果记录数量超过最大限制则移除最旧的记录
        /// </summary>
        /// <param name="timeSeconds"></param>
        /// <param name="nodeName"></param>
        /// <param name="message"></param>
        public void AddRecord(double timeSeconds, string nodeName, string message)
        {
            if (_records.Count >= _maxRecordCount)
            {
                _records.RemoveAt(0);
            }

            _records.Add(new BehaviorTreeDebugRecord(timeSeconds, nodeName, message));
        }

        /// <summary>
        /// 清除所有调试记录
        /// </summary>
        public void Clear()
        {
            _records.Clear();
        }
    }
}