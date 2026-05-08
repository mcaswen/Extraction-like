using Core.BehaviorTree.Blackboard;
using Gameplay.Player.Data;

namespace Gameplay.Player.Core
{
    /// <summary>
    /// 玩家干预层控制器
    /// 当前阶段只负责接收和暂存干预请求，并写入黑板
    /// 不在此处解释具体业务含义
    /// </summary>
    public sealed class PlayerInterventionController
    {
        private readonly BehaviorBlackboard _blackboard;

        public PlayerInterventionController(BehaviorBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        /// <summary>
        /// 提交时，只将相关信息写入黑板
        /// </summary>
        /// <param name="directiveRequest"></param>
        /// <param name="timeSeconds"></param>
        public void SubmitDirective(PlayerDirectiveRequest directiveRequest, double timeSeconds)
        {
            _blackboard.SetValue(
                PlayerBlackboardKeys.HasPendingDirective,
                directiveRequest.DirectiveType != PlayerDirectiveType.None,
                timeSeconds);

            _blackboard.SetValue(
                PlayerBlackboardKeys.PendingDirectiveRequest,
                directiveRequest,
                timeSeconds);
        }

        /// <summary>
        /// 清除时，将相关信息从黑板中移除
        /// </summary>
        /// <param name="timeSeconds"></param>
        public void ClearDirective(double timeSeconds)
        {
            _blackboard.SetValue(
                PlayerBlackboardKeys.HasPendingDirective,
                false,
                timeSeconds);

            _blackboard.RemoveValue(
                PlayerBlackboardKeys.PendingDirectiveRequest,
                timeSeconds);
        }
    }
}