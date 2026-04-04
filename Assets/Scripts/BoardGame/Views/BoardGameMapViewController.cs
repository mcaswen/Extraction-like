using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime;
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
        private readonly Dictionary<string, BoardGameAgentView> _agentViewsById =
            new Dictionary<string, BoardGameAgentView>();

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

            foreach (BoardAgentState agentState in _runtimeQueryController.GetAgentStates())
            {
                if (agentState == null)
                {
                    continue;
                }

                BoardGameAgentView agentView = Instantiate(agentViewPrefab, mapRoot);
                agentView.Initialize(agentState.AgentId);
                _agentViewsById[agentState.AgentId] = agentView;
            }
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
            BoardAgentState focusedAgentState = _runtimeQueryController.GetFocusedAgentState();
            HashSet<string> runtimeInfoVisibleNodeIds = new HashSet<string>(
                _runtimeQueryController.GetFocusedRuntimeInfoVisibleNodeIds());
            Dictionary<string, Vector3> agentDisplayPositions = BuildAgentDisplayPositions(_runtimeQueryController.GetAgentStates());

            foreach (KeyValuePair<string, BoardGameEdgeView> edgeViewPair in _edgeViewsById)
            {
                edgeViewPair.Value.Refresh(highlightedEdgeIds.Contains(edgeViewPair.Key));
            }

            foreach (KeyValuePair<string, BoardGameNodeView> nodeViewPair in _nodeViewsById)
            {
                BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(nodeViewPair.Key);
                BuildFocusedRuntimeOverride(
                    nodeViewPair.Key,
                    nodeState,
                    focusedAgentState,
                    out string runtimeInfoOverrideText,
                    out float? progressOverride01);
                nodeViewPair.Value.Refresh(
                    nodeState,
                    focusedAgentState != null && nodeViewPair.Key == focusedAgentState.CurrentTargetNodeId,
                    nodeViewPair.Key == _selectionStateController.SelectedNodeId,
                    false,
                    runtimeInfoVisibleNodeIds.Contains(nodeViewPair.Key),
                    runtimeInfoOverrideText,
                    progressOverride01);
            }

            foreach (KeyValuePair<string, BoardGameAgentView> agentViewPair in _agentViewsById)
            {
                BoardAgentState agentState = _runtimeQueryController.GetAgentState(agentViewPair.Key);

                if (agentState == null)
                {
                    continue;
                }

                Vector3 displayPosition = agentDisplayPositions.TryGetValue(agentState.AgentId, out Vector3 resolvedPosition)
                    ? resolvedPosition
                    : agentState.WorldPosition;
                agentViewPair.Value.Refresh(
                    agentState,
                    displayPosition,
                    focusedAgentState != null && agentState.AgentId == focusedAgentState.AgentId);
            }
        }

        /// <summary>
        /// 为重叠在同一位置的多个 Agent 生成轻微扇形偏移
        /// 这样四个 AI 同时待在同一点时仍然能被看清和点击
        /// </summary>
        private Dictionary<string, Vector3> BuildAgentDisplayPositions(IReadOnlyList<BoardAgentState> agentStates)
        {
            Dictionary<string, List<BoardAgentState>> agentsByPositionKey =
                new Dictionary<string, List<BoardAgentState>>();

            foreach (BoardAgentState agentState in agentStates)
            {
                if (agentState == null)
                {
                    continue;
                }

                string positionKey = $"{agentState.WorldPosition.x:0.###}_{agentState.WorldPosition.y:0.###}";

                if (!agentsByPositionKey.TryGetValue(positionKey, out List<BoardAgentState> clusteredAgents))
                {
                    clusteredAgents = new List<BoardAgentState>();
                    agentsByPositionKey[positionKey] = clusteredAgents;
                }

                clusteredAgents.Add(agentState);
            }

            Dictionary<string, Vector3> displayPositions = new Dictionary<string, Vector3>();

            foreach (List<BoardAgentState> clusteredAgents in agentsByPositionKey.Values)
            {
                for (int index = 0; index < clusteredAgents.Count; index++)
                {
                    BoardAgentState agentState = clusteredAgents[index];
                    displayPositions[agentState.AgentId] = (Vector3)agentState.WorldPosition + ResolveClusterOffset(index, clusteredAgents.Count);
                }
            }

            return displayPositions;
        }

        /// <summary>
        /// 为同位置 Agent 生成稳定的圆周偏移
        /// </summary>
        private static Vector3 ResolveClusterOffset(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.zero;
            }

            float radius = count == 2 ? 0.18f : 0.28f;
            float angleRadians = (Mathf.PI * 2f * index / count) + Mathf.PI * 0.5f;
            return new Vector3(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius, 0f);
        }

        /// <summary>
        /// 只为当前焦点 Agent 覆写它所在节点的实时文本和进度
        /// 目前主要用于每个 Agent 独立的撤离进度展示
        /// </summary>
        private static void BuildFocusedRuntimeOverride(
            string nodeId,
            BoardNodeRuntimeState nodeState,
            BoardAgentState focusedAgentState,
            out string runtimeInfoOverrideText,
            out float? progressOverride01)
        {
            runtimeInfoOverrideText = null;
            progressOverride01 = null;

            if (nodeState == null ||
                focusedAgentState == null ||
                focusedAgentState.CurrentNodeId != nodeId ||
                focusedAgentState.CurrentActionType != BoardActionType.Extracting)
            {
                return;
            }

            float durationSeconds = Mathf.Max(0.01f, focusedAgentState.CurrentActionDuration);
            progressOverride01 = focusedAgentState.ExtractProgressSeconds / durationSeconds;
            runtimeInfoOverrideText = $"Extract {Mathf.Clamp01(progressOverride01.Value):P0}";
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
