using System.Collections.Generic;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// HUD 文案格式化器
    /// 负责把当前焦点 Agent 的局部状态和全局提示整理成 HUD 可直接显示的字符串
    /// </summary>
    internal sealed class BoardGameHudContextFormatter
    {
        private readonly BoardGameRuntimeQueryController _runtimeQueryController;

        public BoardGameHudContextFormatter(BoardGameRuntimeQueryController runtimeQueryController)
        {
            _runtimeQueryController = runtimeQueryController;
        }

        /// <summary>
        /// 构造 HUD 顶部的全局事件提示
        /// </summary>
        public string BuildStatusText(BoardGameSessionState sessionState)
        {
            if (sessionState == null || string.IsNullOrEmpty(sessionState.StatusMessage))
            {
                return "Event: None";
            }

            return $"Event: {sessionState.StatusMessage}";
        }

        /// <summary>
        /// 构造右下角控制提示
        /// 优先说明当前是谁占用了升级或 loot 交互
        /// </summary>
        public string BuildRedirectHint()
        {
            BoardAgentState activeLevelUpAgentState = _runtimeQueryController.GetActiveLevelUpAgentState();

            if (_runtimeQueryController.IsAwaitingLevelUpChoice && activeLevelUpAgentState != null)
            {
                return $"Level Up: {activeLevelUpAgentState.DisplayName} choose 1/2/3 or click an upgrade";
            }

            BoardAgentState activeInteractionAgentState = _runtimeQueryController.GetActiveInteractionAgentState();
            BoardNodeRuntimeState activeLootNodeState = _runtimeQueryController.SessionState != null
                ? _runtimeQueryController.GetNodeState(_runtimeQueryController.SessionState.ActiveLootNodeId)
                : null;

            if (_runtimeQueryController.IsLootInteractionOpen && activeInteractionAgentState != null)
            {
                string nodeLabel = activeLootNodeState != null ? activeLootNodeState.NodeId : "loot";
                return $"Loot: {activeInteractionAgentState.DisplayName} is managing {nodeLabel}";
            }

            if (_runtimeQueryController.IsAwaitingLootInteraction && activeInteractionAgentState != null)
            {
                return $"Loot: {activeInteractionAgentState.DisplayName} press F to open or continue searching";
            }

            return "Control: Tab switches focus, click a node to redirect";
        }

        /// <summary>
        /// 生成当前焦点 Agent 的局部状态文案
        /// </summary>
        public string BuildFocusContextText(BoardAgentState agentState)
        {
            if (agentState == null)
            {
                return string.Empty;
            }

            if (agentState.IsOnEdge)
            {
                string edgeFromNodeId = string.IsNullOrEmpty(agentState.CurrentEdgeFromNodeId) ? "?" : agentState.CurrentEdgeFromNodeId;
                string edgeToNodeId = string.IsNullOrEmpty(agentState.CurrentEdgeToNodeId) ? "?" : agentState.CurrentEdgeToNodeId;
                return $"Current: Moving {Mathf.Clamp01(agentState.CurrentActionProgress):P0}  {edgeFromNodeId} -> {edgeToNodeId}";
            }

            BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(agentState.CurrentNodeId);

            if (nodeState == null)
            {
                return "Current: No node";
            }

            return $"Current: {nodeState.NodeId}  {BuildNodeContextLabel(nodeState, agentState, true)}";
        }

        /// <summary>
        /// 生成焦点 Agent 周边节点的摘要
        /// 只保留当前有实时意义的搜索 血量和撤离信息
        /// </summary>
        public string BuildNearbyContextText(BoardAgentState agentState)
        {
            if (agentState == null)
            {
                return string.Empty;
            }

            List<string> visibleNodeIds = _runtimeQueryController.GetFocusedRuntimeInfoVisibleNodeIds();
            string anchorNodeId = _runtimeQueryController.GetRuntimeInfoAnchorNodeId(agentState.AgentId);
            List<string> nearbyEntries = new List<string>();

            for (int index = 0; index < visibleNodeIds.Count; index++)
            {
                string nodeId = visibleNodeIds[index];

                if (nodeId == anchorNodeId)
                {
                    continue;
                }

                BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(nodeId);
                string entry = BuildNodeBriefContext(nodeState);

                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                nearbyEntries.Add(entry);

                if (nearbyEntries.Count >= 4)
                {
                    break;
                }
            }

            return nearbyEntries.Count > 0
                ? $"Nearby: {string.Join("  |  ", nearbyEntries)}"
                : "Nearby: None";
        }

        private string BuildNodeContextLabel(
            BoardNodeRuntimeState nodeState,
            BoardAgentState agentState,
            bool isCurrentNode)
        {
            if (nodeState == null)
            {
                return string.Empty;
            }

            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    if (nodeState.ResourceState == BoardResourceStateType.Looted)
                    {
                        return "Looted";
                    }

                    if (nodeState.HasPendingLootInteraction())
                    {
                        return nodeState.IsLootRevealComplete()
                            ? "Loot Ready"
                            : $"Search {nodeState.GetLootRevealProgress01():P0}";
                    }

                    if (nodeState.SearchProgressSeconds > Mathf.Epsilon && nodeState.SearchRequiredSeconds > Mathf.Epsilon)
                    {
                        return $"Search {nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds:P0}";
                    }

                    return isCurrentNode && agentState.CurrentActionType == BoardActionType.Searching
                        ? "Searching"
                        : "Unsearched";

                case BoardNodeType.Enemy:
                    return $"Enemy HP {nodeState.EnemyCurrentHealth}/{Mathf.Max(1, nodeState.EnemyMaxHealth)}";

                case BoardNodeType.Boss:
                    return $"Boss HP {nodeState.BossCurrentHealth}/{Mathf.Max(1, nodeState.BossMaxHealth)}";

                case BoardNodeType.Extract:
                    if (agentState.CurrentActionType == BoardActionType.Extracting)
                    {
                        return $"Extract {Mathf.Clamp01(agentState.CurrentActionProgress):P0}";
                    }

                    if (nodeState.ExtractState == BoardExtractStateType.Extracted)
                    {
                        return "Extracted";
                    }

                    return nodeState.ExtractProgressSeconds > Mathf.Epsilon && nodeState.ExtractRequiredSeconds > Mathf.Epsilon
                        ? $"Extract {nodeState.ExtractProgressSeconds / nodeState.ExtractRequiredSeconds:P0}"
                        : "Ready";

                case BoardNodeType.Start:
                    return "Start";

                default:
                    return nodeState.GetStatusLabel();
            }
        }

        private string BuildNodeBriefContext(BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null)
            {
                return string.Empty;
            }

            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    if (nodeState.HasPendingLootInteraction())
                    {
                        return nodeState.IsLootRevealComplete()
                            ? $"{nodeState.NodeId} Loot"
                            : $"{nodeState.NodeId} Search {nodeState.GetLootRevealProgress01():P0}";
                    }

                    if (nodeState.SearchProgressSeconds > Mathf.Epsilon && nodeState.SearchRequiredSeconds > Mathf.Epsilon)
                    {
                        return $"{nodeState.NodeId} Search {nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds:P0}";
                    }

                    return string.Empty;

                case BoardNodeType.Enemy:
                    return nodeState.EnemyState == BoardEnemyStateType.Cleared && !nodeState.HasPendingLootInteraction()
                        ? string.Empty
                        : $"{nodeState.NodeId} HP {nodeState.EnemyCurrentHealth}/{Mathf.Max(1, nodeState.EnemyMaxHealth)}";

                case BoardNodeType.Boss:
                    return nodeState.BossState == BoardBossStateType.Defeated && !nodeState.HasPendingLootInteraction()
                        ? string.Empty
                        : $"{nodeState.NodeId} HP {nodeState.BossCurrentHealth}/{Mathf.Max(1, nodeState.BossMaxHealth)}";

                case BoardNodeType.Extract:
                    return nodeState.ExtractProgressSeconds > Mathf.Epsilon && nodeState.ExtractRequiredSeconds > Mathf.Epsilon
                        ? $"{nodeState.NodeId} Extract {nodeState.ExtractProgressSeconds / nodeState.ExtractRequiredSeconds:P0}"
                        : string.Empty;

                default:
                    return string.Empty;
            }
        }
    }
}
