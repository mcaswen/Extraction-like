using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.Core
{
    /// <summary>
    /// Agent干预层控制器
    /// 当前阶段只负责接收和暂存干预请求，并写入黑板
    /// 不在此处解释具体业务含义
    /// </summary>
    public sealed class AgentInterventionController
    {
        private readonly BehaviorBlackboard _blackboard;

        /// <summary>
        /// 创建 Agent 干预层控制器
        /// </summary>
        /// <param name="blackboard"></param>
        public AgentInterventionController(BehaviorBlackboard blackboard)
        {
            _blackboard = blackboard;
        }

        /// <summary>
        /// 提交时，只将相关信息写入黑板
        /// </summary>
        /// <param name="directiveRequest"></param>
        /// <param name="timeSeconds"></param>
        public void SubmitDirective(AgentDirectiveRequest directiveRequest, double timeSeconds)
        {
            _blackboard.SetValue(
                AgentBlackboardKeys.HasPendingDirective,
                directiveRequest.DirectiveType != AgentDirectiveType.None,
                timeSeconds);

            _blackboard.SetValue(
                AgentBlackboardKeys.PendingDirectiveRequest,
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
                AgentBlackboardKeys.HasPendingDirective,
                false,
                timeSeconds);

            _blackboard.RemoveValue(
                AgentBlackboardKeys.PendingDirectiveRequest,
                timeSeconds);
        }
    }
}
