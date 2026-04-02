using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using BoardGame.Runtime.Services;
using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// 地图视图总控，负责运行时生成节点、边和 AI 表现对象
    /// </summary>
    public sealed class BoardGameMapViewController : MonoBehaviour
    {
        private readonly Dictionary<string, BoardGameNodeView> _nodeViewsById =
            new Dictionary<string, BoardGameNodeView>();

        private readonly Dictionary<string, BoardGameEdgeView> _edgeViewsById =
            new Dictionary<string, BoardGameEdgeView>();

        private SO_BoardGame_MapDefinition _mapDefinition;
        private BoardGraphService _graphService;
        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameSelectionStateController _selectionStateController;
        private BoardGameAgentView _agentView;

        /// <summary>
        /// 初始化地图视图并订阅运行时事件
        /// </summary>
        public void Initialize(
            SO_BoardGame_MapDefinition mapDefinition,
            BoardGraphService graphService,
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameSelectionStateController selectionStateController,
            Transform mapRoot,
            BoardGameNodeView nodeViewPrefab,
            BoardGameEdgeView edgeViewPrefab,
            BoardGameAgentView agentViewPrefab)
        {
            _mapDefinition = mapDefinition;
            _graphService = graphService;
            _runtimeQueryController = runtimeQueryController;
            _selectionStateController = selectionStateController;

            BuildMapViews(mapRoot != null ? mapRoot : transform, nodeViewPrefab, edgeViewPrefab, agentViewPrefab);
            _runtimeQueryController.Changed += RefreshViews;
            _selectionStateController.SelectionChanged += RefreshViews;
            RefreshViews();
        }

        /// <summary>
        /// 按 SO 地图定义生成节点和边的表现对象
        /// </summary>
        private void BuildMapViews(
            Transform mapRoot,
            BoardGameNodeView nodeViewPrefab,
            BoardGameEdgeView edgeViewPrefab,
            BoardGameAgentView agentViewPrefab)
        {
            foreach (BoardMapNodeDefinition nodeDefinition in _mapDefinition.Nodes)
            {
                BoardGameNodeView nodeView = Instantiate(nodeViewPrefab, mapRoot);
                nodeView.transform.position = nodeDefinition.Position;
                nodeView.Initialize(nodeDefinition.NodeId);
                _nodeViewsById[nodeDefinition.NodeId] = nodeView;
            }

            foreach (BoardMapEdgeDefinition edgeDefinition in _mapDefinition.Edges)
            {
                BoardGameEdgeView edgeView = Instantiate(edgeViewPrefab, mapRoot);
                Vector3 fromPosition = _graphService.GetNodePosition(edgeDefinition.FromNodeId);
                Vector3 toPosition = _graphService.GetNodePosition(edgeDefinition.ToNodeId);
                float fromRadius = GetNodeVisualRadius(edgeDefinition.FromNodeId);
                float toRadius = GetNodeVisualRadius(edgeDefinition.ToNodeId);
                edgeView.Initialize(
                    edgeDefinition.EdgeId,
                    fromPosition,
                    toPosition,
                    fromRadius,
                    toRadius);
                _edgeViewsById[edgeDefinition.EdgeId] = edgeView;
            }

            _agentView = Instantiate(agentViewPrefab, mapRoot);
        }

        /// <summary>
        /// 根据运行时状态刷新地图表现
        /// </summary>
        private void RefreshViews()
        {
            if (_runtimeQueryController == null || _selectionStateController == null)
            {
                return;
            }

            HashSet<string> highlightedEdgeIds = new HashSet<string>(_runtimeQueryController.GetHighlightedEdgeIds());
            BoardAgentState agentState = _runtimeQueryController.SessionState.AgentState;
            HashSet<string> runtimeInfoVisibleNodeIds = BuildRuntimeInfoVisibleNodeIds(agentState);

            foreach (KeyValuePair<string, BoardGameEdgeView> edgeViewPair in _edgeViewsById)
            {
                edgeViewPair.Value.Refresh(highlightedEdgeIds.Contains(edgeViewPair.Key));
            }

            foreach (KeyValuePair<string, BoardGameNodeView> nodeViewPair in _nodeViewsById)
            {
                BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(nodeViewPair.Key);
                nodeViewPair.Value.Refresh(
                    nodeState,
                    nodeViewPair.Key == agentState.CurrentTargetNodeId,
                    nodeViewPair.Key == _selectionStateController.SelectedNodeId,
                    false,
                    runtimeInfoVisibleNodeIds.Contains(nodeViewPair.Key));
            }

            _agentView?.Refresh(agentState, false);
        }

        /// <summary>
        /// 计算当前允许展示实时数值信息的节点集合
        /// 规则为 AI 所在点和其相邻点
        /// 若 AI 位于边上，则取该边两端及其相邻点
        /// </summary>
        private HashSet<string> BuildRuntimeInfoVisibleNodeIds(BoardAgentState agentState)
        {
            HashSet<string> visibleNodeIds = new HashSet<string>();

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
        /// 把某个节点及其相邻节点加入可见集合
        /// </summary>
        private void AddNodeAndNeighbors(string nodeId, HashSet<string> visibleNodeIds)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            visibleNodeIds.Add(nodeId);

            foreach (BoardMapNodeDefinition neighbor in _graphService.GetNeighbors(nodeId))
            {
                visibleNodeIds.Add(neighbor.NodeId);
            }
        }

        /// <summary>
        /// 查询节点主体的视觉半径
        /// 供边线从节点外缘开始连接
        /// </summary>
        private float GetNodeVisualRadius(string nodeId)
        {
            if (_nodeViewsById.TryGetValue(nodeId, out BoardGameNodeView nodeView))
            {
                return nodeView.GetVisualWorldRadius();
            }

            return 0.25f;
        }
    }
}
