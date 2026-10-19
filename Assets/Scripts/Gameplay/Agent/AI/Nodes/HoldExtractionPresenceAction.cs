using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：在撤离点内每帧续写"在点内"，撤离计时由 RaidFlowController 负责；永远返回运行中。
    /// 退出或被中断（例如移出范围时被前面的移动节点抢占）时撤销登记，防止切目标后继续倒计时。
    /// </summary>
    public sealed class HoldExtractionPresenceAction : AgentActionNodeBase
    {
        private readonly AgentExtractionMemory _memory;
        private global::ExtractionPointController _activeExtractionPoint;
        private string _activeAgentId;

        public HoldExtractionPresenceAction(string nodeName, AgentExtractionMemory memory)
            : base(nodeName)
        {
            _memory = memory;
            DisplayName = "登记在撤离点内";
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            TryGetAgent(context, out IAgentReadOnly agent);
            SetActiveExtractionPoint(agent, _memory.ExtractionPoint);
            return Running();
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            ClearActiveExtractionPoint();
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            ClearActiveExtractionPoint();
        }

        private void SetActiveExtractionPoint(
            IAgentReadOnly agent,
            global::ExtractionPointController extractionPoint)
        {
            string agentId = agent != null ? agent.AgentIdValue : string.Empty;
            if (_activeExtractionPoint == extractionPoint &&
                string.Equals(_activeAgentId, agentId, System.StringComparison.Ordinal))
            {
                // 每帧续写 true，兼容 RaidFlowController 的持续计时模型
                global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                    agentId,
                    extractionPoint,
                    true);
                return;
            }

            // 切换撤离点前先清旧点，避免两个撤离点同时处于激活状态
            ClearActiveExtractionPoint();
            _activeExtractionPoint = extractionPoint;
            _activeAgentId = agentId;
            global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                _activeAgentId,
                _activeExtractionPoint,
                true);
        }

        private void ClearActiveExtractionPoint()
        {
            if (_activeExtractionPoint == null)
                return;

            global::RaidFlowController.Instance?.SetAgentInsideExtractionPoint(
                _activeAgentId,
                _activeExtractionPoint,
                false);
            _activeExtractionPoint = null;
            _activeAgentId = string.Empty;
        }
    }
}
