
namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 从节点执行返回的结果结构体定义，包含执行状态和失败原因
    /// 允许拿到失败原因，而非仅仅是结果
    /// </summary>
    public readonly struct BehaviorNodeResult
    {
        public BehaviorNodeStatus Status { get; }
        public BehaviorFailureReason FailureReason { get; }

        public bool IsSuccess => Status == BehaviorNodeStatus.Success;
        public bool IsFailure => Status == BehaviorNodeStatus.Failure;
        public bool IsRunning => Status == BehaviorNodeStatus.Running;

        private BehaviorNodeResult(
            BehaviorNodeStatus status,
            BehaviorFailureReason failureReason)
        {
            Status = status;
            FailureReason = failureReason;
        }

        /// <summary>
        /// 工厂方法创建成功结果
        /// </summary>
        /// <returns></returns>
        public static BehaviorNodeResult Success()
        {
            return new BehaviorNodeResult(
                BehaviorNodeStatus.Success,
                BehaviorFailureReason.None);
        }

        /// <summary>
        /// 工厂方法创建运行中结果
        /// </summary>
        /// <returns></returns>
        public static BehaviorNodeResult Running()
        {
            return new BehaviorNodeResult(
                BehaviorNodeStatus.Running,
                BehaviorFailureReason.None);
        }

        /// <summary>
        /// 工厂方法创建失败结果，包含失败原因
        /// </summary>
        /// <param name="failureReason"></param>
        /// <returns></returns>
        public static BehaviorNodeResult Failure(BehaviorFailureReason failureReason)
        {
            return new BehaviorNodeResult(
                BehaviorNodeStatus.Failure,
                failureReason);
        }

        public override string ToString()
        {
            return IsFailure
                ? $"{Status} ({FailureReason})"
                : Status.ToString();
        }
    }
}