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
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            bool bagSystemEnabled = context.BagLayoutSettings.EnableBagSystem;

            if (!nodeState.HasUnfinishedSearch())
            {
                return false;
            }

            if (!nodeState.HasPendingLootContainer())
            {
                List<BoardItemInstance> generatedItems = context.LootResolutionService.GenerateResourceLoot(nodeState);
                BoardExperienceGrantResult experienceResult = context.ProgressionService.GrantExperienceFromItems(sessionState, agentState, generatedItems);
                context.LootResolutionService.PrepareNodeLootContainer(nodeState, generatedItems, context.BagLayoutSettings);

                if (!nodeState.HasPendingLootContainer())
                {
                    nodeState.ResourceState = BoardResourceStateType.Looted;
                    BoardNodeActionHandlerUtility.FinishCurrentTarget(
                        context,
                        sessionState,
                        agentState,
                        $"Search complete{experienceResult.Summary}");
                    return true;
                }

                sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                    agentState,
                    nodeState,
                    bagSystemEnabled
                        ? $"Loot ready, press F to start searching{experienceResult.Summary}"
                        : $"Searching{experienceResult.Summary}");
            }
            else
            {
                sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                    agentState,
                    nodeState,
                    bagSystemEnabled
                        ? (nodeState.IsLootRevealComplete()
                            ? "Search complete, close the loot bag to continue"
                            : "Loot ready, press F to continue searching")
                        : (nodeState.IsLootRevealComplete()
                            ? "Search complete, auto collecting loot"
                            : "Searching"));
            }

            nodeState.ResourceState = BoardResourceStateType.Searching;

            if (bagSystemEnabled)
            {
                nodeState.SyncSearchProgressFromLootReveal();

                nodeState.ResourceState = nodeState.IsLootRevealComplete()
                    ? BoardResourceStateType.SearchCompleted
                    : (nodeState.SearchProgressSeconds > 0f
                        ? BoardResourceStateType.PartiallySearched
                        : BoardResourceStateType.Unsearched);

                BoardNodeActionHandlerUtility.FinishCurrentTarget(
                    context,
                    sessionState,
                    agentState,
                    nodeState.IsLootRevealComplete()
                        ? "Loot ready, press F to reopen"
                        : "Loot ready, press F to start or continue searching");
                return true;
            }

            sessionState.ActiveInteractionAgentId = agentState.AgentId;
            sessionState.ActiveLootNodeId = nodeState.NodeId;
            sessionState.IsLootInteractionOpen = false;
            sessionState.FocusedAgentId = agentState.AgentId;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = Mathf.Max(0.1f, nodeState.SearchRequiredSeconds);
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = GetSearchActionProgress(nodeState, false);
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            nodeState.ResourceState = BoardResourceStateType.Searching;

            if (context.BagLayoutSettings.EnableBagSystem)
            {
                nodeState.SyncSearchProgressFromLootReveal();
                agentState.CurrentActionDuration = 1f;
            }
            else
            {
                agentState.CurrentActionDuration = Mathf.Max(0.1f, nodeState.SearchRequiredSeconds);
            }

            agentState.CurrentActionProgress = GetSearchActionProgress(nodeState, context.BagLayoutSettings.EnableBagSystem);
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            nodeState.ResourceState = nodeState.SearchProgressSeconds > 0f
                ? BoardResourceStateType.PartiallySearched
                : BoardResourceStateType.Unsearched;
        }

        private static float GetSearchActionProgress(BoardNodeRuntimeState nodeState, bool useLootRevealProgress)
        {
            if (useLootRevealProgress)
            {
                return nodeState.GetLootRevealProgress01();
            }

            return nodeState.SearchRequiredSeconds <= Mathf.Epsilon
                ? 1f
                : nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
        }
    }
}
