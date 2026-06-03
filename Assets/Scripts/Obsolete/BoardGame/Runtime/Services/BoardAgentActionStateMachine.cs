using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 原型核心状态机
    /// 负责顶层动作分发，并把导航与节点动作委托给专用 service
    /// </summary>
    public sealed class BoardAgentActionStateMachine
    {
        private readonly BoardAgentNodeActionService _nodeActionService;
        private readonly BoardAgentNavigationService _navigationService;

        public BoardAgentActionStateMachine(
            BoardGraphService graphService,
            BoardPathfindingService pathfindingService,
            BoardAgentDecisionService decisionService,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService,
            BoardProgressionService progressionService,
            BoardInterruptService interruptService,
            SO_BoardGame_RuleSet ruleSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            BoardNodeActionHandlerContext nodeActionHandlerContext = new BoardNodeActionHandlerContext(
                ruleSet,
                combatResolutionService,
                lootResolutionService,
                progressionService,
                bagLayoutSettings);
            _nodeActionService = new BoardAgentNodeActionService(nodeActionHandlerContext);
            _navigationService = new BoardAgentNavigationService(
                graphService,
                pathfindingService,
                decisionService,
                interruptService,
                ruleSet,
                _nodeActionService);
        }

        /// <summary>
        /// 推进一次状态机
        /// 每帧只更新单个 Agent 的当前动作，对局结束后不再推进
        /// </summary>
        public void Tick(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            float deltaTime)
        {
            if (sessionState.Outcome != BoardSessionOutcome.None || agentState == null || agentState.HasExtracted)
            {
                return;
            }

            if (agentState.IsDownedPermanently)
            {
                return;
            }

            agentState.AutonomousDecisionElapsedSeconds += deltaTime;

            if (!agentState.IsAlive)
            {
                MarkAgentDowned(sessionState, agentState);
                return;
            }

            switch (agentState.CurrentActionType)
            {
                case BoardActionType.Idle:
                    _navigationService.TickIdle(sessionState, agentState, nodeStatesById);
                    break;
                case BoardActionType.Moving:
                    _navigationService.TickMoving(sessionState, agentState, nodeStatesById, deltaTime);
                    break;
                case BoardActionType.Searching:
                case BoardActionType.FightingEnemy:
                case BoardActionType.FightingBoss:
                case BoardActionType.Extracting:
                    _nodeActionService.TickCurrentNodeAction(sessionState, agentState, nodeStatesById, deltaTime);
                    break;
            }

            if (!agentState.IsAlive)
            {
                MarkAgentDowned(sessionState, agentState);
            }
        }

        /// <summary>
        /// 由玩家发起一次目标改写
        /// 会先检查能否打断，再把当前动作安全收口，然后重新计算路径
        /// </summary>
        public bool TryRedirect(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            string targetNodeId,
            out string message)
        {
            return _navigationService.TryRedirect(sessionState, agentState, nodeStatesById, targetNodeId, out message);
        }

        /// <summary>
        /// 统一处理单个 Agent 的倒地收口
        /// 所有 Agent 都倒地后才会判定整局失败
        /// </summary>
        private static void MarkAgentDowned(BoardGameSessionState sessionState, BoardAgentState agentState)
        {
            agentState.IsDownedPermanently = true;
            agentState.CurrentActionType = BoardActionType.Downed;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.ExtractProgressSeconds = 0f;
            agentState.PendingLevelUpCount = 0;
            agentState.CombatJoinStepIndex = -1;

            if (sessionState.ActiveInteractionAgentId == agentState.AgentId)
            {
                sessionState.ActiveInteractionAgentId = string.Empty;
                sessionState.ActiveLootNodeId = string.Empty;
                sessionState.IsLootInteractionOpen = false;
            }

            if (sessionState.ActiveLevelUpAgentId == agentState.AgentId)
            {
                sessionState.ActiveLevelUpAgentId = string.Empty;
                sessionState.PendingLevelUpChoices.Clear();
            }

            bool hasLivingAgent = false;

            foreach (BoardAgentState otherAgentState in sessionState.AgentStates)
            {
                if (otherAgentState != null && otherAgentState.IsAlive)
                {
                    hasLivingAgent = true;
                    break;
                }
            }

            if (!hasLivingAgent)
            {
                sessionState.Outcome = BoardSessionOutcome.Failure;
                sessionState.StatusMessage = BoardGameStatusMessageUtility.System("All AI HP reached zero, run failed");
                return;
            }

            sessionState.StatusMessage = BoardGameStatusMessageUtility.Agent(agentState, "Has been downed");
        }
    }
}
