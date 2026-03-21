using System.Collections.Generic;

namespace Core.StateMachine.Debugging
{
    /// <summary>
    /// 分层状态机调试跟踪类定义，用于记录状态机的状态变化和相关调试信息
    /// 作为缓冲区，保留最近的 N 条记录
    /// </summary>
    public sealed class StateMachineDebugTrace
    {
        private readonly List<StateMachineDebugRecord> _records = new List<StateMachineDebugRecord>();
        private readonly int _maxRecordCount;

        public StateMachineDebugTrace(int maxRecordCount = 256)
        {
            _maxRecordCount = maxRecordCount;
        }

        public IReadOnlyList<StateMachineDebugRecord> Records => _records;

        public void AddRecord(double timeSeconds, string stateName, string message)
        {
            if (_records.Count >= _maxRecordCount)
            {
                _records.RemoveAt(0);
            }

            _records.Add(new StateMachineDebugRecord(timeSeconds, stateName, message));
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}