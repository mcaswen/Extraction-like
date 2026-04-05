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
        [SerializeField] private float _nodeClusterOffsetScale = 0.88f;
        [SerializeField] private float _edgeClusterLaneSpacingScale = 0.72f;
        [SerializeField] private float _fallbackClusterOffsetScale = 1.12f;
        [SerializeField] private float _defaultWorldClusterRadius = 0.22f;

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

        private enum AgentClusterLayoutType
        {
            Node,
            Edge,
            World
        }

        private struct AgentClusterAnchor
        {
            public Vector3 Center;
            public float BaseRadius;
            public Vector3 Direction;
            public AgentClusterLayoutType LayoutType;
        }

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
        /// 为重叠 Agent 生成稳定偏移
        /// 同节点时围绕节点中心排布，同边移动时沿边法线排成多条 lane
        /// </summary>
        private Dictionary<string, Vector3> BuildAgentDisplayPositions(IReadOnlyList<BoardAgentState> agentStates)
        {
            Dictionary<string, List<BoardAgentState>> agentsByClusterKey =
                new Dictionary<string, List<BoardAgentState>>();
            Dictionary<string, AgentClusterAnchor> clusterAnchorsByKey =
                new Dictionary<string, AgentClusterAnchor>();

            foreach (BoardAgentState agentState in agentStates)
            {
                if (agentState == null)
                {
                    continue;
                }

                string clusterKey = BuildClusterKey(agentState);

                if (!agentsByClusterKey.TryGetValue(clusterKey, out List<BoardAgentState> clusteredAgents))
                {
                    clusteredAgents = new List<BoardAgentState>();
                    agentsByClusterKey[clusterKey] = clusteredAgents;
                    clusterAnchorsByKey[clusterKey] = ResolveClusterAnchor(agentState);
                }

                clusteredAgents.Add(agentState);
            }

            Dictionary<string, Vector3> displayPositions = new Dictionary<string, Vector3>();

            foreach (KeyValuePair<string, List<BoardAgentState>> clusterPair in agentsByClusterKey)
            {
                List<BoardAgentState> clusteredAgents = clusterPair.Value;
                clusteredAgents.Sort(CompareAgentClusterOrder);
                AgentClusterAnchor clusterAnchor = clusterAnchorsByKey[clusterPair.Key];

                for (int index = 0; index < clusteredAgents.Count; index++)
                {
                    BoardAgentState agentState = clusteredAgents[index];
                    displayPositions[agentState.AgentId] =
                        clusterAnchor.Center + ResolveClusterOffset(clusterAnchor, index, clusteredAgents.Count);
                }
            }

            return displayPositions;
        }

        // 优先按边和节点聚类，让边上移动和节点驻留有各自独立的重叠排布规则
        private static string BuildClusterKey(BoardAgentState agentState)
        {
            if (agentState.IsOnEdge && !string.IsNullOrWhiteSpace(agentState.CurrentEdgeId))
            {
                return $"edge_{agentState.CurrentEdgeId}_{agentState.CurrentEdgeProgress01:0.###}";
            }

            if (!string.IsNullOrWhiteSpace(agentState.CurrentNodeId))
            {
                return $"node_{agentState.CurrentNodeId}";
            }

            return $"world_{agentState.WorldPosition.x:0.###}_{agentState.WorldPosition.y:0.###}";
        }

        // 为每一组重叠 Agent 解析统一的排布锚点
        // 节点用中心点，边上移动用边方向，其他情况回退到当前世界位置
        private AgentClusterAnchor ResolveClusterAnchor(BoardAgentState agentState)
        {
            if (agentState.IsOnEdge &&
                !string.IsNullOrWhiteSpace(agentState.CurrentEdgeId) &&
                _graphService.TryGetEdge(agentState.CurrentEdgeId, out BoardMapEdgeDefinition edgeDefinition))
            {
                Vector3 fromPosition = _graphService.GetNodePosition(edgeDefinition.FromNodeId);
                Vector3 toPosition = _graphService.GetNodePosition(edgeDefinition.ToNodeId);
                Vector3 direction = (toPosition - fromPosition).normalized;

                if (direction.sqrMagnitude <= Mathf.Epsilon)
                {
                    direction = Vector3.right;
                }

                float averageNodeRadius =
                    (GetNodeVisualRadius(edgeDefinition.FromNodeId) + GetNodeVisualRadius(edgeDefinition.ToNodeId)) * 0.5f;

                return new AgentClusterAnchor
                {
                    Center = _graphService.GetPositionOnEdge(edgeDefinition, agentState.CurrentEdgeProgress01),
                    BaseRadius = Mathf.Max(_defaultWorldClusterRadius, averageNodeRadius),
                    Direction = direction,
                    LayoutType = AgentClusterLayoutType.Edge
                };
            }

            if (!string.IsNullOrWhiteSpace(agentState.CurrentNodeId) &&
                _graphService.TryGetNode(agentState.CurrentNodeId, out BoardMapNodeDefinition nodeDefinition))
            {
                return new AgentClusterAnchor
                {
                    Center = nodeDefinition.Position,
                    BaseRadius = GetNodeVisualRadius(nodeDefinition.NodeId),
                    Direction = Vector3.up,
                    LayoutType = AgentClusterLayoutType.Node
                };
            }

            return new AgentClusterAnchor
            {
                Center = agentState.WorldPosition,
                BaseRadius = _defaultWorldClusterRadius,
                Direction = Vector3.up,
                LayoutType = AgentClusterLayoutType.World
            };
        }

        // 保证同一组 Agent 的显示槽位稳定
        // 避免每帧刷新时因为遍历顺序不同而交换位置
        private static int CompareAgentClusterOrder(BoardAgentState left, BoardAgentState right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return string.CompareOrdinal(left.AgentId, right.AgentId);
        }

        // 节点和边使用不同的排布方式
        // 节点围绕中心散开，边上则沿法线分 lane
        private Vector3 ResolveClusterOffset(AgentClusterAnchor clusterAnchor, int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.zero;
            }

            switch (clusterAnchor.LayoutType)
            {
                case AgentClusterLayoutType.Edge:
                    return ResolveEdgeLaneOffset(clusterAnchor, index, count);

                case AgentClusterLayoutType.Node:
                    return ResolveNodeClusterOffset(clusterAnchor.BaseRadius * _nodeClusterOffsetScale, index, count);

                default:
                    return ResolveNodeClusterOffset(_defaultWorldClusterRadius * _fallbackClusterOffsetScale, index, count);
            }
        }

        // 边上移动时按边法线展开
        // 这样多个 AI 同线移动时会像并排的 lane，而不是绕成一圈
        private Vector3 ResolveEdgeLaneOffset(AgentClusterAnchor clusterAnchor, int index, int count)
        {
            Vector3 normal = new Vector3(-clusterAnchor.Direction.y, clusterAnchor.Direction.x, 0f).normalized;

            if (normal.sqrMagnitude <= Mathf.Epsilon)
            {
                normal = Vector3.up;
            }

            float laneIndex = index - ((count - 1) * 0.5f);
            float laneSpacing = clusterAnchor.BaseRadius * _edgeClusterLaneSpacingScale;
            return normal * (laneIndex * laneSpacing);
        }

        // 节点驻留时围绕节点中心排布
        // 优先照顾 4 个 AI 的可读性，超过 4 个再退回圆周分布
        private static Vector3 ResolveNodeClusterOffset(float radius, int index, int count)
        {
            switch (count)
            {
                case 2:
                    return ResolvePolarOffset(index == 0 ? 180f : 0f, radius);

                case 3:
                    return ResolvePolarOffset(90f + (120f * index), radius);

                case 4:
                    return ResolvePolarOffset(135f - (90f * index), radius);

                default:
                    return ResolvePolarOffset(90f + ((360f * index) / count), radius);
            }
        }

        // 统一处理极坐标偏移换算
        private static Vector3 ResolvePolarOffset(float angleDegrees, float radius)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
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
