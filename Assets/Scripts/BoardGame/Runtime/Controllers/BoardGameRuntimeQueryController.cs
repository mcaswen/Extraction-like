using System;
using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 运行时只读查询控制器
    /// 负责向表现层提供节点查询和路径高亮结果
    /// </summary>
    public sealed class BoardGameRuntimeQueryController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly IReadOnlyDictionary<string, BoardNodeRuntimeState> _nodeStatesById;
        private readonly BoardGameSelectionStateController _selectionStateController;
        private readonly BoardGameAgentFocusController _agentFocusController;
        private readonly BoardGraphService _graphService;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly BoardGameBagLayoutSettings _bagLayoutSettings;

        public BoardGameRuntimeQueryController(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardGameSelectionStateController selectionStateController,
            BoardGameAgentFocusController agentFocusController,
            BoardGraphService graphService,
            SO_BoardGame_RuleSet ruleSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _sessionState = sessionState;
            _nodeStatesById = nodeStatesById;
            _selectionStateController = selectionStateController;
            _agentFocusController = agentFocusController;
            _graphService = graphService;
            _ruleSet = ruleSet;
            _bagLayoutSettings = bagLayoutSettings;
        }

        public event Action Changed;

        public BoardGameSessionState SessionState => _sessionState;
        public string SelectedNodeId => _selectionStateController.SelectedNodeId;
        public BoardGameBagLayoutSettings BagLayoutSettings => _bagLayoutSettings;
        public bool IsBagSystemEnabled => _bagLayoutSettings.EnableBagSystem;
        public bool IsProgressionEnabled => _ruleSet.ProgressionRules.Enabled;
        public bool IsAwaitingLevelUpChoice => IsProgressionEnabled && _sessionState.IsAwaitingLevelUpChoice;
        public bool IsAwaitingLootInteraction => _sessionState.IsAwaitingLootInteraction;
        public bool IsLootInteractionOpen => _sessionState.IsLootInteractionOpen;
        public bool IsInteractionLocked => IsAwaitingLevelUpChoice || IsLootInteractionOpen;

        /// <summary>
        /// 按节点 ID 查询对应的运行时状态
        /// </summary>
        public BoardNodeRuntimeState GetNodeState(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) &&
                   _nodeStatesById.TryGetValue(nodeId, out BoardNodeRuntimeState nodeState)
                ? nodeState
                : null;
        }

        /// <summary>
        /// 获取当前被鼠标选中的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetSelectedNodeState()
        {
            return GetNodeState(_selectionStateController.SelectedNodeId);
        }

        /// <summary>
        /// 获取当前焦点 Agent
        /// </summary>
        public BoardAgentState GetFocusedAgentState()
        {
            return _agentFocusController.GetFocusedAgentState();
        }

        /// <summary>
        /// 按 AgentId 查询运行时 Agent
        /// </summary>
        public BoardAgentState GetAgentState(string agentId)
        {
            return _sessionState.GetAgentState(agentId);
        }

        /// <summary>
        /// 获取当前升级门控归属的 Agent
        /// </summary>
        public BoardAgentState GetActiveLevelUpAgentState()
        {
            return _sessionState.GetActiveLevelUpAgentState();
        }

        /// <summary>
        /// 获取当前 loot 交互归属的 Agent
        /// </summary>
        public BoardAgentState GetActiveInteractionAgentState()
        {
            return _sessionState.GetActiveInteractionAgentState();
        }

        /// <summary>
        /// 返回当前会话中的全部 Agent 状态
        /// </summary>
        public IReadOnlyList<BoardAgentState> GetAgentStates()
        {
            return _sessionState.AgentStates;
        }

        /// <summary>
        /// 返回当前存活的 Agent 数量
        /// </summary>
        public int GetAliveAgentCount()
        {
            int aliveCount = 0;

            foreach (BoardAgentState agentState in _sessionState.AgentStates)
            {
                if (agentState != null && agentState.IsAlive)
                {
                    aliveCount++;
                }
            }

            return aliveCount;
        }

        /// <summary>
        /// 返回会话中的 Agent 总数
        /// </summary>
        public int GetTotalAgentCount()
        {
            return _sessionState.AgentStates.Count;
        }

        /// <summary>
        /// 为地图表现层生成当前应高亮的路径边列表
        /// </summary>
        public List<string> GetHighlightedEdgeIds()
        {
            BoardAgentState agentState = GetFocusedAgentState();
            return agentState != null ? GetHighlightedEdgeIds(agentState.AgentId) : new List<string>();
        }

        /// <summary>
        /// 按 AgentId 生成应高亮的路径边列表
        /// </summary>
        public List<string> GetHighlightedEdgeIds(string agentId)
        {
            List<string> edgeIds = new List<string>();
            BoardAgentState agentState = GetAgentState(agentId);

            if (agentState == null)
            {
                return edgeIds;
            }

            if (agentState.IsOnEdge && !string.IsNullOrEmpty(agentState.CurrentEdgeId))
            {
                edgeIds.Add(agentState.CurrentEdgeId);
            }

            string previousNodeId = agentState.CurrentNodeId;

            if (string.IsNullOrEmpty(previousNodeId) && agentState.RemainingPathNodeIds.Count > 0)
            {
                previousNodeId = agentState.RemainingPathNodeIds[0];
            }

            for (int index = 0; index < agentState.RemainingPathNodeIds.Count; index++)
            {
                string nextNodeId = agentState.RemainingPathNodeIds[index];

                if (string.IsNullOrEmpty(previousNodeId) || previousNodeId == nextNodeId)
                {
                    previousNodeId = nextNodeId;
                    continue;
                }

                if (_graphService.TryGetEdgeBetween(previousNodeId, nextNodeId, out BoardMapEdgeDefinition edgeDefinition))
                {
                    edgeIds.Add(edgeDefinition.EdgeId);
                }

                previousNodeId = nextNodeId;
            }

            return edgeIds;
        }

        /// <summary>
        /// 返回某个 Agent 当前允许看到实时节点信息的节点集合
        /// 规则为所在节点及其相邻节点，若在边上则取边两端及其相邻节点
        /// </summary>
        public List<string> GetRuntimeInfoVisibleNodeIds(string agentId)
        {
            List<string> visibleNodeIds = new List<string>();
            BoardAgentState agentState = GetAgentState(agentId);

            if (agentState == null)
            {
                return visibleNodeIds;
            }

            if (agentState.IsOnEdge)
            {
                AddNodeAndNeighbors(agentState.CurrentEdgeFromNodeId, visibleNodeIds);
                AddNodeAndNeighbors(agentState.CurrentEdgeToNodeId, visibleNodeIds);
                return visibleNodeIds;
            }

            AddNodeAndNeighbors(agentState.CurrentNodeId, visibleNodeIds);
            return visibleNodeIds;
        }

        /// <summary>
        /// 返回当前焦点 Agent 的局部实时节点集合
        /// </summary>
        public List<string> GetFocusedRuntimeInfoVisibleNodeIds()
        {
            BoardAgentState focusedAgentState = GetFocusedAgentState();
            return focusedAgentState != null
                ? GetRuntimeInfoVisibleNodeIds(focusedAgentState.AgentId)
                : new List<string>();
        }

        /// <summary>
        /// 读取某个 Agent 的当前局部中心节点
        /// 在节点上时返回当前位置，在边上时优先返回前进方向上的端点
        /// </summary>
        public string GetRuntimeInfoAnchorNodeId(string agentId)
        {
            BoardAgentState agentState = GetAgentState(agentId);

            if (agentState == null)
            {
                return string.Empty;
            }

            if (!agentState.IsOnEdge)
            {
                return agentState.CurrentNodeId ?? string.Empty;
            }

            return agentState.CurrentEdgeProgress01 >= 0.5f
                ? agentState.CurrentEdgeToNodeId ?? string.Empty
                : agentState.CurrentEdgeFromNodeId ?? string.Empty;
        }

        /// <summary>
        /// 广播只读运行时状态发生变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>
        /// 把节点及其相邻节点加入结果列表，并保持节点 ID 不重复
        /// </summary>
        private void AddNodeAndNeighbors(string nodeId, List<string> visibleNodeIds)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            AddUniqueNodeId(nodeId, visibleNodeIds);

            foreach (BoardMapNodeDefinition neighbor in _graphService.GetNeighbors(nodeId))
            {
                AddUniqueNodeId(neighbor.NodeId, visibleNodeIds);
            }
        }

        private static void AddUniqueNodeId(string nodeId, List<string> visibleNodeIds)
        {
            if (string.IsNullOrEmpty(nodeId) || visibleNodeIds.Contains(nodeId))
            {
                return;
            }

            visibleNodeIds.Add(nodeId);
        }
    }
}
