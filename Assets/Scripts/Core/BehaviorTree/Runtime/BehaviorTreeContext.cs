using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Debugging;

namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树执行上下文类定义，包含黑板、调试跟踪和时间戳等信息
    /// </summary>
    public sealed class BehaviorTreeContext
    {
        public BehaviorBlackboard Blackboard { get; }
        public BehaviorTreeDebugTrace DebugTrace { get; }

        public object UserContext { get; set; }

        public int TickVersion { get; internal set; }
        public float DeltaTime { get; internal set; }
        public double TimeSeconds { get; internal set; }

        public BehaviorTreeContext(
            BehaviorBlackboard blackboard,
            BehaviorTreeDebugTrace debugTrace = default,
            object userContext = null)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            DebugTrace = debugTrace;
            UserContext = userContext;
        }

        public void LogNodeEvent(string nodeName, string message)
        {
            DebugTrace.AddRecord(TimeSeconds, nodeName, message);
        }
    }
}