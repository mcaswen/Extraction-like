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
        private readonly BoardGraphService _graphService;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly BoardGameBagLayoutSettings _bagLayoutSettings;

        public BoardGameRuntimeQueryController(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardGameSelectionStateController selectionStateController,
            BoardGraphService graphService,
            SO_BoardGame_RuleSet ruleSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _sessionState = sessionState;
            _nodeStatesById = nodeStatesById;
            _selectionStateController = selectionStateController;
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
        /// 为地图表现层生成当前应高亮的路径边列表
        /// </summary>
        public List<string> GetHighlightedEdgeIds()
        {
            List<string> edgeIds = new List<string>();
            BoardAgentState agentState = _sessionState.AgentState;

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
        /// 广播只读运行时状态发生变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
