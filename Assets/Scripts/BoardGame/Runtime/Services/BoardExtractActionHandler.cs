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
            BoardNodeRuntimeState nodeState)
        {
            if (nodeState.ExtractState == BoardExtractStateType.Extracted)
            {
                return false;
            }

            BoardAgentState agentState = sessionState.AgentState;
            nodeState.ExtractState = BoardExtractStateType.Extracting;
            agentState.CurrentActionType = BoardActionType.Extracting;
            agentState.CurrentActionDuration = context.RuleSet.ExtractRules.DurationSeconds;
            agentState.CurrentActionProgress = nodeState.ExtractProgressSeconds / context.RuleSet.ExtractRules.DurationSeconds;
            sessionState.StatusMessage = $"Started extracting at {nodeState.NodeId}";
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;
            nodeState.ExtractState = BoardExtractStateType.Extracting;
            nodeState.ExtractProgressSeconds = Mathf.Min(
                context.RuleSet.ExtractRules.DurationSeconds,
                nodeState.ExtractProgressSeconds + deltaTime);
            agentState.CurrentActionDuration = context.RuleSet.ExtractRules.DurationSeconds;
            agentState.CurrentActionProgress = nodeState.ExtractProgressSeconds / context.RuleSet.ExtractRules.DurationSeconds;

            if (nodeState.ExtractProgressSeconds < context.RuleSet.ExtractRules.DurationSeconds)
            {
                return;
            }

            nodeState.ExtractState = BoardExtractStateType.Extracted;
            agentState.CurrentActionType = BoardActionType.Completed;
            agentState.CurrentActionProgress = 1f;
            sessionState.FinalExtractedValue = agentState.InventoryState.TotalValue;
            sessionState.Outcome = BoardSessionOutcome.Success;
            sessionState.StatusMessage = $"Extraction complete, final extracted value {sessionState.FinalExtractedValue}";
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState)
        {
            if (!context.RuleSet.ExtractRules.PreserveProgressOnInterrupt)
            {
                nodeState.ExtractProgressSeconds = 0f;
            }

            nodeState.ExtractState = BoardExtractStateType.Available;
        }
    }
}
