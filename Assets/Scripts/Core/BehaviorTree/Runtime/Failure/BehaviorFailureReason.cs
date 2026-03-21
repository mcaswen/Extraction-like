
namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 失败原因结构体定义
    /// 包含失败类型、来源节点名称和详细信息
    /// </summary>
    public readonly struct BehaviorFailureReason
    {
        public static readonly BehaviorFailureReason None =
            new BehaviorFailureReason(BehaviorFailureCode.None, string.Empty, string.Empty);

        public BehaviorFailureCode Code { get; }
        public string SourceNodeName { get; }
        public string Detail { get; }

        public bool IsNone => Code == BehaviorFailureCode.None;

        public BehaviorFailureReason(
            BehaviorFailureCode code,
            string sourceNodeName,
            string detail)
        {
            Code = code;
            SourceNodeName = sourceNodeName ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public override string ToString()
        {
            if (IsNone)
            {
                return "None";
            }

            return $"{Code} @ {SourceNodeName} :: {Detail}";
        }
    }
}