using System.Collections.Generic;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 资源搜索动作处理器
    /// </summary>
    internal sealed class BoardSearchActionHandler : IBoardNodeActionHandler
    {
        public BoardNodeType SupportedNodeType => BoardNodeType.Resource;
        public BoardActionType SupportedActionType => BoardActionType.Searching;

        public bool TryBegin(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState)
        {
            if (!nodeState.HasUnfinishedSearch())
            {
                return false;
            }

            BoardAgentState agentState = sessionState.AgentState;
            nodeState.ResourceState = BoardResourceStateType.Searching;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = nodeState.SearchRequiredSeconds;
            agentState.CurrentActionProgress = nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
            sessionState.StatusMessage = $"Started searching {nodeState.NodeId}";
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;
            nodeState.ResourceState = BoardResourceStateType.Searching;
            nodeState.SearchProgressSeconds = Mathf.Min(nodeState.SearchRequiredSeconds, nodeState.SearchProgressSeconds + deltaTime);
            agentState.CurrentActionDuration = nodeState.SearchRequiredSeconds;
            agentState.CurrentActionProgress = nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;

            if (nodeState.SearchProgressSeconds < nodeState.SearchRequiredSeconds)
            {
                return;
            }

            nodeState.ResourceState = BoardResourceStateType.SearchCompleted;
            List<BoardItemInstance> generatedItems = context.LootResolutionService.GenerateResourceLoot(nodeState);
            BoardExperienceGrantResult experienceResult = context.ProgressionService.GrantExperienceFromItems(sessionState, generatedItems);
            BoardAutoCollectResult collectResult = context.LootResolutionService.AutoCollect(agentState.InventoryState, generatedItems);
            nodeState.ResourceState = BoardResourceStateType.Looted;
            BoardNodeActionHandlerUtility.FinishCurrentTarget(
                context,
                sessionState,
                $"Search complete{collectResult.Summary}{experienceResult.Summary}");
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState)
        {
            nodeState.ResourceState = nodeState.SearchProgressSeconds > 0f
                ? BoardResourceStateType.PartiallySearched
                : BoardResourceStateType.Unsearched;
        }
    }
}
