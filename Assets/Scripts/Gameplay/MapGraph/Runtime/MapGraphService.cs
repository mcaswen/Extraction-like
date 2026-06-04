using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>
    /// 抽象地图图结构查询服务
    /// 负责把 SO 配置预构建为节点索引、边索引和邻接表
    /// </summary>
    public sealed class MapGraphService
    {
        private readonly Dictionary<string, MapGraphNodeDefinition> _nodesById =
            new Dictionary<string, MapGraphNodeDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, MapGraphEdgeDefinition> _edgesById =
            new Dictionary<string, MapGraphEdgeDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<MapGraphEdgeDefinition>> _edgesByNodeId =
            new Dictionary<string, List<MapGraphEdgeDefinition>>(StringComparer.Ordinal);

        private readonly SO_MapGraphDefinition _mapDefinition;

        public MapGraphService(SO_MapGraphDefinition mapDefinition)
        {
            _mapDefinition = mapDefinition;
            if (_mapDefinition == null)
                return;

            BuildNodeIndexes(_mapDefinition.Nodes);
            BuildEdgeIndexes(_mapDefinition.Edges);
        }

        /// <summary>
        /// 当前服务是否持有有效图配置
        /// </summary>
        public bool IsValid => _mapDefinition != null && _nodesById.Count > 0;

        /// <summary>
        /// 返回图中全部节点 ID
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetNodeIds()
        {
            return _nodesById.Keys;
        }

        /// <summary>
        /// 解析图默认起点
        /// 优先使用配置中的 StartNodeId，其次回退到第一个 Start 类型节点
        /// </summary>
        /// <returns></returns>
        public string GetStartNodeId()
        {
            if (_mapDefinition != null &&
                !string.IsNullOrWhiteSpace(_mapDefinition.StartNodeId) &&
                _nodesById.ContainsKey(_mapDefinition.StartNodeId))
            {
                return _mapDefinition.StartNodeId;
            }

            foreach (MapGraphNodeDefinition node in _nodesById.Values)
            {
                if (node.NodeKind == MapGraphNodeKind.Start)
                    return node.NodeId;
            }

            foreach (string nodeId in _nodesById.Keys)
                return nodeId;

            return string.Empty;
        }

        /// <summary>
        /// 按节点 ID 查询节点定义
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="nodeDefinition"></param>
        /// <returns></returns>
        public bool TryGetNode(string nodeId, out MapGraphNodeDefinition nodeDefinition)
        {
            return _nodesById.TryGetValue(NormalizeId(nodeId), out nodeDefinition);
        }

        /// <summary>
        /// 按边 ID 查询边定义
        /// </summary>
        /// <param name="edgeId"></param>
        /// <param name="edgeDefinition"></param>
        /// <returns></returns>
        public bool TryGetEdge(string edgeId, out MapGraphEdgeDefinition edgeDefinition)
        {
            return _edgesById.TryGetValue(NormalizeId(edgeId), out edgeDefinition);
        }

        /// <summary>
        /// 查询两个节点之间是否存在边
        /// 当前 MapGraph 按双向边处理
        /// </summary>
        /// <param name="firstNodeId"></param>
        /// <param name="secondNodeId"></param>
        /// <param name="edgeDefinition"></param>
        /// <returns></returns>
        public bool TryGetEdgeBetween(
            string firstNodeId,
            string secondNodeId,
            out MapGraphEdgeDefinition edgeDefinition)
        {
            edgeDefinition = null;
            string normalizedFirstId = NormalizeId(firstNodeId);
            string normalizedSecondId = NormalizeId(secondNodeId);

            if (!_edgesByNodeId.TryGetValue(normalizedFirstId, out List<MapGraphEdgeDefinition> edges))
                return false;

            for (int index = 0; index < edges.Count; index++)
            {
                MapGraphEdgeDefinition edge = edges[index];
                bool matchesForward = edge.FromNodeId == normalizedFirstId && edge.ToNodeId == normalizedSecondId;
                bool matchesBackward = edge.FromNodeId == normalizedSecondId && edge.ToNodeId == normalizedFirstId;

                if (!matchesForward && !matchesBackward)
                    continue;

                edgeDefinition = edge;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取连接指定节点的全部边
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public IReadOnlyList<MapGraphEdgeDefinition> GetConnectedEdges(string nodeId)
        {
            return _edgesByNodeId.TryGetValue(NormalizeId(nodeId), out List<MapGraphEdgeDefinition> edges)
                ? edges
                : Array.Empty<MapGraphEdgeDefinition>();
        }

        /// <summary>
        /// 在一条边上，给定一端节点 ID，返回另一端节点 ID
        /// </summary>
        /// <param name="edgeDefinition"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public string GetOtherNodeId(MapGraphEdgeDefinition edgeDefinition, string nodeId)
        {
            if (edgeDefinition == null)
                return string.Empty;

            string normalizedNodeId = NormalizeId(nodeId);
            if (edgeDefinition.FromNodeId == normalizedNodeId)
                return edgeDefinition.ToNodeId;

            if (edgeDefinition.ToNodeId == normalizedNodeId)
                return edgeDefinition.FromNodeId;

            return string.Empty;
        }

        /// <summary>
        /// 获取节点在抽象图 UI 平面中的坐标
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public Vector2 GetNodePosition(string nodeId)
        {
            return TryGetNode(nodeId, out MapGraphNodeDefinition nodeDefinition)
                ? nodeDefinition.Position
                : Vector2.zero;
        }

        /// <summary>
        /// 根据边和进度插值出图上的当前位置
        /// </summary>
        /// <param name="edgeDefinition"></param>
        /// <param name="progress01"></param>
        /// <returns></returns>
        public Vector2 GetPositionOnEdge(MapGraphEdgeDefinition edgeDefinition, float progress01)
        {
            if (edgeDefinition == null)
                return Vector2.zero;

            Vector2 from = GetNodePosition(edgeDefinition.FromNodeId);
            Vector2 to = GetNodePosition(edgeDefinition.ToNodeId);
            return Vector2.Lerp(from, to, Mathf.Clamp01(progress01));
        }

        private void BuildNodeIndexes(IReadOnlyList<MapGraphNodeDefinition> nodes)
        {
            if (nodes == null)
                return;

            for (int index = 0; index < nodes.Count; index++)
            {
                MapGraphNodeDefinition node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    continue;

                _nodesById[node.NodeId] = node;
                if (!_edgesByNodeId.ContainsKey(node.NodeId))
                    _edgesByNodeId.Add(node.NodeId, new List<MapGraphEdgeDefinition>());
            }
        }

        private void BuildEdgeIndexes(IReadOnlyList<MapGraphEdgeDefinition> edges)
        {
            if (edges == null)
                return;

            for (int index = 0; index < edges.Count; index++)
            {
                MapGraphEdgeDefinition edge = edges[index];
                if (edge == null ||
                    string.IsNullOrWhiteSpace(edge.EdgeId) ||
                    !_nodesById.ContainsKey(edge.FromNodeId) ||
                    !_nodesById.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                _edgesById[edge.EdgeId] = edge;
                _edgesByNodeId[edge.FromNodeId].Add(edge);
                _edgesByNodeId[edge.ToNodeId].Add(edge);
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
