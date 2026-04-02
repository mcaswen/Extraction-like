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
        /// 每帧只更新当前动作，对局结束后不再推进
        /// </summary>
        public void Tick(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime)
        {
            if (sessionState.Outcome != BoardSessionOutcome.None)
            {
                return;
            }

            BoardAgentState agentState = sessionState.AgentState;
            sessionState.ElapsedSeconds += deltaTime;
            agentState.AutonomousDecisionElapsedSeconds += deltaTime;

            if (!agentState.IsAlive)
            {
                FailSession(sessionState, "AI HP reached zero, run failed");
                return;
            }

            switch (agentState.CurrentActionType)
            {
                case BoardActionType.Idle:
                    _navigationService.TickIdle(sessionState, nodeStatesById);
                    break;
                case BoardActionType.Moving:
                    _navigationService.TickMoving(sessionState, nodeStatesById, deltaTime);
                    break;
                case BoardActionType.Searching:
                case BoardActionType.FightingEnemy:
                case BoardActionType.FightingBoss:
                case BoardActionType.Extracting:
                    _nodeActionService.TickCurrentNodeAction(sessionState, nodeStatesById, deltaTime);
                    break;
            }

            if (!agentState.IsAlive)
            {
                FailSession(sessionState, "AI HP reached zero, run failed");
            }
        }

        /// <summary>
        /// 由玩家发起一次目标改写
        /// 会先检查能否打断，再把当前动作安全收口，然后重新计算路径
        /// </summary>
        public bool TryRedirect(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            string targetNodeId,
            out string message)
        {
            return _navigationService.TryRedirect(sessionState, nodeStatesById, targetNodeId, out message);
        }

        /// <summary>
        /// 统一设置失败结果
        /// </summary>
        private static void FailSession(BoardGameSessionState sessionState, string message)
        {
            sessionState.Outcome = BoardSessionOutcome.Failure;
            sessionState.AgentState.CurrentActionType = BoardActionType.Downed;
            sessionState.AgentState.CurrentActionProgress = 0f;
            sessionState.StatusMessage = message;
        }
    }
}
