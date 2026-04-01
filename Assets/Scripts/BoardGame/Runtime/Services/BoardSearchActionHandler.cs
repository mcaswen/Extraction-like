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
            if (!nodeState.HasPendingLootContainer())
            {
                List<BoardItemInstance> generatedItems = context.LootResolutionService.GenerateResourceLoot(nodeState);
                BoardExperienceGrantResult experienceResult = context.ProgressionService.GrantExperienceFromItems(sessionState, generatedItems);
                context.LootResolutionService.PrepareNodeLootContainer(nodeState, generatedItems, context.BagLayoutSettings);

                if (!nodeState.HasPendingLootContainer())
                {
                    nodeState.ResourceState = BoardResourceStateType.Looted;
                    BoardNodeActionHandlerUtility.FinishCurrentTarget(
                        context,
                        sessionState,
                        $"Search complete{experienceResult.Summary}");
                    return true;
                }

                sessionState.StatusMessage = $"Loot ready at {nodeState.NodeId}, press F to start searching{experienceResult.Summary}";
            }
            else
            {
                sessionState.StatusMessage = nodeState.IsLootRevealComplete()
                    ? $"Search complete at {nodeState.NodeId}, close the loot bag to continue"
                    : $"Loot ready at {nodeState.NodeId}, press F to continue searching";
            }

            nodeState.ResourceState = BoardResourceStateType.Searching;
            nodeState.SyncSearchProgressFromLootReveal();
            sessionState.ActiveLootNodeId = nodeState.NodeId;
            sessionState.IsLootInteractionOpen = false;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();
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
            nodeState.SyncSearchProgressFromLootReveal();
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();
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
