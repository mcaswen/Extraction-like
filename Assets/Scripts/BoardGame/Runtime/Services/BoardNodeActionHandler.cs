using BoardGame.Config;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 节点动作处理器接口
    /// 负责某一类节点动作的进入、推进与打断收口
    /// </summary>
    internal interface IBoardNodeActionHandler
    {
        BoardNodeType SupportedNodeType { get; }
        BoardActionType SupportedActionType { get; }

        bool TryBegin(BoardNodeActionHandlerContext context, BoardGameSessionState sessionState, BoardNodeRuntimeState nodeState);
        void Tick(BoardNodeActionHandlerContext context, BoardGameSessionState sessionState, BoardNodeRuntimeState nodeState, float deltaTime);
        void FinalizeForRedirect(BoardNodeActionHandlerContext context, BoardGameSessionState sessionState, BoardNodeRuntimeState nodeState);
    }

    /// <summary>
    /// 节点动作处理器共享上下文
    /// 只暴露动作执行所需的服务与规则
    /// </summary>
    internal sealed class BoardNodeActionHandlerContext
    {
        public BoardNodeActionHandlerContext(
            SO_BoardGame_RuleSet ruleSet,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService,
            BoardProgressionService progressionService)
        {
            RuleSet = ruleSet;
            CombatResolutionService = combatResolutionService;
            LootResolutionService = lootResolutionService;
            ProgressionService = progressionService;
        }

        public SO_BoardGame_RuleSet RuleSet { get; }
        public BoardCombatResolutionService CombatResolutionService { get; }
        public BoardLootResolutionService LootResolutionService { get; }
        public BoardProgressionService ProgressionService { get; }
    }

    /// <summary>
    /// 节点动作处理器共用状态迁移辅助
    /// </summary>
    internal static class BoardNodeActionHandlerUtility
    {
        public static void FinishCurrentTarget(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            string statusMessage)
        {
            BoardAgentState agentState = sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentTargetNodeId = string.Empty;
            agentState.IntentSource = BoardIntentSource.Autonomous;
            agentState.AutonomousDecisionElapsedSeconds = context.RuleSet.AutonomousRules.ReevaluateIntervalSeconds;
            sessionState.StatusMessage = statusMessage;
        }
    }
}
