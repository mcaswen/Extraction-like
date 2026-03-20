
namespace Core.BehaviorTree.Debugging
{
    /// <summary>
    /// 行为树调试记录结构体定义，包含时间戳、节点名称和调试信息字符串
    /// </summary>
    public readonly struct BehaviorTreeDebugRecord
    {
        public readonly double TimeSeconds;
        public readonly string NodeName;
        public readonly string Message;

        public BehaviorTreeDebugRecord(double timeSeconds, string nodeName, string message)
        {
            TimeSeconds = timeSeconds;
            NodeName = nodeName;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{TimeSeconds:F3}] {NodeName} :: {Message}";
        }
    }
}