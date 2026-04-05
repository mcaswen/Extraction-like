using System.Collections.Generic;
using BoardGame.Runtime;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 节点动作服务
    /// 负责节点动作处理器注册、动作推进和待处理 loot 接管
    /// </summary>
    internal sealed class BoardAgentNodeActionService
    {
        private readonly BoardNodeActionHandlerContext _nodeActionHandlerContext;
        private readonly Dictionary<BoardNodeType, IBoardNodeActionHandler> _nodeActionHandlersByNodeType =
            new Dictionary<BoardNodeType, IBoardNodeActionHandler>();
        private readonly Dictionary<BoardActionType, IBoardNodeActionHandler> _nodeActionHandlersByActionType =
            new Dictionary<BoardActionType, IBoardNodeActionHandler>();

        public BoardAgentNodeActionService(BoardNodeActionHandlerContext nodeActionHandlerContext)
        {
            _nodeActionHandlerContext = nodeActionHandlerContext;

            RegisterNodeActionHandler(new BoardSearchActionHandler());
            RegisterNodeActionHandler(new BoardCombatActionHandler(false));
            RegisterNodeActionHandler(new BoardCombatActionHandler(true));
            RegisterNodeActionHandler(new BoardExtractActionHandler());
        }

        /// <summary>
        /// 推进当前所在节点上的持续动作
        /// 具体实现由节点动作处理器负责
        /// </summary>
        public void TickCurrentNodeAction(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            float deltaTime)
        {
            if (!_nodeActionHandlersByActionType.TryGetValue(agentState.CurrentActionType, out IBoardNodeActionHandler handler))
            {
                return;
            }

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            handler.Tick(_nodeActionHandlerContext, sessionState, agentState, nodeState, deltaTime);
        }

        /// <summary>
        /// 当角色真正抵达当前目标节点时，尝试进入对应的持续动作
        /// 玩家远点引导下的沿途节点会走单独的中途停靠逻辑
        /// </summary>
        public bool TryBeginActionOnCurrentTargetNode(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            return TryBeginActionOnCurrentNode(sessionState, agentState, nodeStatesById, true);
        }

        /// <summary>
        /// 玩家指定远处目标时，允许沿途节点先执行自己的动作
        /// 这样重定向既能表达前进方向，也不会跳过路上的资源点或战斗点
        /// </summary>
        public bool TryBeginActionOnVisitedRedirectNode(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            if (agentState.IntentSource != BoardIntentSource.PlayerRedirect ||
                agentState.RemainingPathNodeIds.Count == 0)
            {
                return false;
            }

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState) ||
                nodeState.NodeType == BoardNodeType.Extract)
            {
                return false;
            }

            return TryBeginActionOnCurrentNode(sessionState, agentState, nodeStatesById, false);
        }

        /// <summary>
        /// 玩家改写目标前，对当前动作做安全收口
        /// 具体节点动作的收口逻辑交给对应处理器
        /// </summary>
        public void FinalizeCurrentActionForRedirect(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            if (_nodeActionHandlersByActionType.TryGetValue(agentState.CurrentActionType, out IBoardNodeActionHandler handler) &&
                TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                handler.FinalizeForRedirect(_nodeActionHandlerContext, sessionState, agentState, nodeState);
            }

            if (agentState.CurrentActionType != BoardActionType.Moving)
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                agentState.CurrentActionProgress = 0f;
            }

            agentState.CurrentActionAccumulatorSeconds = 0f;
        }

        /// <summary>
        /// 尝试在当前节点开始动作
        /// 可选择是否要求该节点必须等于当前目标节点
        /// </summary>
        private bool TryBeginActionOnCurrentNode(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            bool requireCurrentTargetMatch)
        {
            if (string.IsNullOrEmpty(agentState.CurrentNodeId) ||
                !nodeStatesById.TryGetValue(agentState.CurrentNodeId, out BoardNodeRuntimeState nodeState))
            {
                return false;
            }

            if (requireCurrentTargetMatch &&
                (string.IsNullOrEmpty(agentState.CurrentTargetNodeId) ||
                 agentState.CurrentNodeId != agentState.CurrentTargetNodeId))
            {
                return false;
            }

            if (TryBeginPendingLootInteraction(sessionState, agentState, nodeState))
            {
                return true;
            }

            if (!_nodeActionHandlersByNodeType.TryGetValue(nodeState.NodeType, out IBoardNodeActionHandler handler))
            {
                return false;
            }

            return handler.TryBegin(_nodeActionHandlerContext, sessionState, agentState, nodeState);
        }

        /// <summary>
        /// 若节点上还有未收口的 loot，就先把状态机切回统一的 Searching 交互流程
        /// 这样节点动作和 loot 面板始终共享同一套完成度
        /// </summary>
        private bool TryBeginPendingLootInteraction(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null || !nodeState.HasPendingLootContainer() || !nodeState.HasRemainingLootItems())
            {
                return false;
            }

            sessionState.ActiveInteractionAgentId = agentState.AgentId;
            bool bagSystemEnabled = _nodeActionHandlerContext.BagLayoutSettings.EnableBagSystem;
            sessionState.ActiveLootNodeId = nodeState.NodeId;
            sessionState.IsLootInteractionOpen = false;
            sessionState.FocusedAgentId = agentState.AgentId;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = bagSystemEnabled
                ? 1f
                : GetBaglessLootActionDuration(nodeState);
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = bagSystemEnabled
                ? nodeState.GetLootRevealProgress01()
                : GetBaglessLootActionProgress(nodeState);

            if (nodeState.NodeType == BoardNodeType.Resource)
            {
                nodeState.ResourceState = nodeState.IsLootRevealComplete()
                    ? BoardResourceStateType.SearchCompleted
                    : BoardResourceStateType.Searching;
            }

            sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                agentState,
                nodeState,
                bagSystemEnabled
                    ? (nodeState.IsLootRevealComplete()
                        ? "Loot remains, press F to reopen"
                        : "Loot remains, press F to continue searching")
                    : GetBaglessLootStatus(nodeState));
            return true;
        }

        private static float GetBaglessLootActionDuration(BoardNodeRuntimeState nodeState)
        {
            if (nodeState.NodeType == BoardNodeType.Resource && !nodeState.IsLootRevealComplete())
            {
                return Mathf.Max(0.1f, nodeState.SearchRequiredSeconds);
            }

            return 1f;
        }

        private static float GetBaglessLootActionProgress(BoardNodeRuntimeState nodeState)
        {
            if (nodeState.NodeType == BoardNodeType.Resource && !nodeState.IsLootRevealComplete())
            {
                return nodeState.SearchRequiredSeconds <= Mathf.Epsilon
                    ? 1f
                    : nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
            }

            return nodeState.GetLootRevealProgressWithPartial01();
        }

        private static string GetBaglessLootStatus(BoardNodeRuntimeState nodeState)
        {
            if (nodeState.IsLootRevealComplete())
            {
                return $"Auto collecting remaining loot at {nodeState.NodeId}";
            }

            return nodeState.NodeType == BoardNodeType.Resource
                ? $"Resuming search at {nodeState.NodeId}"
                : $"Searching loot at {nodeState.NodeId}";
        }

        /// <summary>
        /// 从节点索引中读取角色当前所在节点状态
        /// </summary>
        private static bool TryGetCurrentNodeState(
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            out BoardNodeRuntimeState nodeState)
        {
            nodeState = null;
            return !string.IsNullOrEmpty(agentState.CurrentNodeId) &&
                   nodeStatesById.TryGetValue(agentState.CurrentNodeId, out nodeState);
        }

        /// <summary>
        /// 按节点类型和动作类型注册处理器
        /// 让状态机只需要按当前上下文查表分发
        /// </summary>
        private void RegisterNodeActionHandler(IBoardNodeActionHandler handler)
        {
            _nodeActionHandlersByNodeType[handler.SupportedNodeType] = handler;
            _nodeActionHandlersByActionType[handler.SupportedActionType] = handler;
        }
    }
}
