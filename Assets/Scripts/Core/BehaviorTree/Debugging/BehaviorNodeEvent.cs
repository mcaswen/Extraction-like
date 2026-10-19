using Core.BehaviorTree.Nodes;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Debugging
{
    /// <summary>
    /// 结构化节点事件；只含值类型和已有引用，记录时不分配内存
    /// </summary>
    public readonly struct BehaviorNodeEvent
    {
        public readonly BehaviorNode Node;
        public readonly BehaviorNodeEventKind Kind;
        public readonly BehaviorNodeStatus Status;
        public readonly BehaviorFailureCode FailureCode;
        public readonly int TickVersion;
        public readonly double TimeSeconds;

        public BehaviorNodeEvent(
            BehaviorNode node,
            BehaviorNodeEventKind kind,
            BehaviorNodeStatus status,
            BehaviorFailureCode failureCode,
            int tickVersion,
            double timeSeconds)
        {
            Node = node;
            Kind = kind;
            Status = status;
            FailureCode = failureCode;
            TickVersion = tickVersion;
            TimeSeconds = timeSeconds;
        }
    }
}
