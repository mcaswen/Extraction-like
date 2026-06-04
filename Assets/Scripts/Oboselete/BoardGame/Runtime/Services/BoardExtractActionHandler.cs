using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 撤离动作处理器
    /// </summary>
    internal sealed class BoardExtractActionHandler : IBoardNodeActionHandler
    {
        public BoardNodeType SupportedNodeType => BoardNodeType.Extract;
        public BoardActionType SupportedActionType => BoardActionType.Extracting;

        public bool TryBegin(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            if (agentState.HasExtracted)
            {
                return false;
            }

            nodeState.ExtractState = BoardExtractStateType.Extracting;
            agentState.CurrentActionType = BoardActionType.Extracting;
            agentState.CurrentActionDuration = context.RuleSet.ExtractRules.DurationSeconds;
            agentState.CurrentActionProgress = agentState.ExtractProgressSeconds / context.RuleSet.ExtractRules.DurationSeconds;
            sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                agentState,
                nodeState,
                "Started extracting");
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            nodeState.ExtractState = BoardExtractStateType.Extracting;
            agentState.ExtractProgressSeconds = Mathf.Min(
                context.RuleSet.ExtractRules.DurationSeconds,
                agentState.ExtractProgressSeconds + deltaTime);
            agentState.CurrentActionDuration = context.RuleSet.ExtractRules.DurationSeconds;
            agentState.CurrentActionProgress = agentState.ExtractProgressSeconds / context.RuleSet.ExtractRules.DurationSeconds;

            if (agentState.ExtractProgressSeconds < context.RuleSet.ExtractRules.DurationSeconds)
            {
                return;
            }

            agentState.CurrentActionType = BoardActionType.Completed;
            agentState.CurrentActionProgress = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            sessionState.FinalExtractedValue = CalculateExtractedValue(sessionState);

            if (AreAllLivingAgentsExtracted(sessionState))
            {
                nodeState.ExtractState = BoardExtractStateType.Extracted;
                sessionState.Outcome = BoardSessionOutcome.Success;
                sessionState.StatusMessage = BoardGameStatusMessageUtility.System(
                    $"All surviving AI extracted, final extracted value {sessionState.FinalExtractedValue}");
                return;
            }

            nodeState.ExtractState = BoardExtractStateType.Available;
            sessionState.StatusMessage = BoardGameStatusMessageUtility.Agent(
                agentState,
                $"Extracted successfully, {CountRemainingLivingAgents(sessionState)} surviving AI remain");
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            if (!context.RuleSet.ExtractRules.PreserveProgressOnInterrupt)
            {
                agentState.ExtractProgressSeconds = 0f;
            }

            nodeState.ExtractState = BoardExtractStateType.Available;
        }

        /// <summary>
        /// 统计所有已安全撤离 Agent 的总价值
        /// </summary>
        private static int CalculateExtractedValue(BoardGameSessionState sessionState)
        {
            int totalValue = 0;

            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState != null && agentState.HasExtracted)
                {
                    totalValue += agentState.InventoryState.TotalValue;
                }
            }

            return totalValue;
        }

        /// <summary>
        /// 只要还有存活但未撤离的 Agent，整局就不能判成功
        /// </summary>
        private static bool AreAllLivingAgentsExtracted(BoardGameSessionState sessionState)
        {
            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState != null && agentState.IsAlive && !agentState.HasExtracted)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 统计还未撤离的存活 Agent 数量
        /// </summary>
        private static int CountRemainingLivingAgents(BoardGameSessionState sessionState)
        {
            int remainingCount = 0;

            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState != null && agentState.IsAlive && !agentState.HasExtracted)
                {
                    remainingCount++;
                }
            }

            return remainingCount;
        }
    }
}
