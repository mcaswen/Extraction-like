using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Debugging;

namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 分层状态机上下文类定义，封装了状态机运行时所需的各种信息和工具
    /// 包括行为树上下文、黑板访问、调试跟踪以及时间信息等，统一打包给状态和条件转移
    /// </summary>
    public sealed class StateMachineContext
    {
        public BehaviorTreeContext BehaviorTreeContext { get; }
        public BehaviorBlackboard Blackboard => BehaviorTreeContext.Blackboard;
        public StateMachineDebugTrace DebugTrace { get; }

        public object UserContext
        {
            get => BehaviorTreeContext.UserContext;
            set => BehaviorTreeContext.UserContext = value;
        }

        public float DeltaTime { get; internal set; }
        public double TimeSeconds { get; internal set; }

        public StateMachineContext(
            BehaviorTreeContext behaviorTreeContext,
            StateMachineDebugTrace debugTrace = null)
        {
            BehaviorTreeContext = behaviorTreeContext ?? throw new ArgumentNullException(nameof(behaviorTreeContext));
            DebugTrace = debugTrace;
        }

        public void LogStateEvent(string stateName, string message)
        {
            DebugTrace?.AddRecord(TimeSeconds, stateName, message);
        }
    }
}