using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 玩家打断规则校验服务
    /// </summary>
    public sealed class BoardInterruptService
    {
        private readonly SO_BoardGame_RuleSet _ruleSet;

        public BoardInterruptService(SO_BoardGame_RuleSet ruleSet)
        {
            _ruleSet = ruleSet;
        }

        /// <summary>
        /// 校验玩家当前是否允许改写目标
        /// 这里集中处理 Boss 战禁改、撤离禁打断、无效节点等规则
        /// </summary>
        public BoardInterruptEvaluation Evaluate(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            string targetNodeId)
        {
            if (sessionState.Outcome != BoardSessionOutcome.None)
            {
                return BoardInterruptEvaluation.Fail("The run has ended and the target cannot be redirected");
            }

            if (string.IsNullOrEmpty(targetNodeId) || !nodeStatesById.ContainsKey(targetNodeId))
            {
                return BoardInterruptEvaluation.Fail("The target node is invalid");
            }

            BoardAgentState agentState = sessionState.AgentState;

            if (sessionState.IsAwaitingLootInteraction)
            {
                return BoardInterruptEvaluation.Fail("Target redirection is disabled while loot interaction is pending");
            }

            if (agentState.CurrentActionType == BoardActionType.FightingBoss)
            {
                return BoardInterruptEvaluation.Fail("Target redirection is disabled during a boss fight");
            }

            if (agentState.CurrentActionType == BoardActionType.Extracting && !_ruleSet.ExtractRules.CanInterrupt)
            {
                return BoardInterruptEvaluation.Fail("The current extraction cannot be interrupted");
            }

            if (agentState.CurrentActionType == BoardActionType.Downed ||
                agentState.CurrentActionType == BoardActionType.Completed)
            {
                return BoardInterruptEvaluation.Fail("The current state does not allow target redirection");
            }

            if (agentState.CurrentTargetNodeId == targetNodeId &&
                !agentState.IsOnEdge &&
                agentState.RemainingPathNodeIds.Count == 0)
            {
                return BoardInterruptEvaluation.Fail("The target has not changed");
            }

            return BoardInterruptEvaluation.Success();
        }
    }

    /// <summary>
    /// 打断规则校验结果
    /// </summary>
    public sealed class BoardInterruptEvaluation
    {
        private BoardInterruptEvaluation() { }

        public bool CanInterrupt { get; private set; }
        public string Message { get; private set; }

        public static BoardInterruptEvaluation Success()
        {
            return new BoardInterruptEvaluation
            {
                CanInterrupt = true,
                Message = string.Empty
            };
        }

        public static BoardInterruptEvaluation Fail(string message)
        {
            return new BoardInterruptEvaluation
            {
                CanInterrupt = false,
                Message = message
            };
        }
    }
}
