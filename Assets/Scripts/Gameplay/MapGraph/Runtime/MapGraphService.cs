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
        private readonly Dictionary<string, MapGraphZoneDefinition> _zonesById =
            new Dictionary<string, MapGraphZoneDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<MapGraphEdgeDefinition>> _edgesByNodeId =
            new Dictionary<string, List<MapGraphEdgeDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<MapGraphEdgeDefinition>> _edgeViews =
            new Dictionary<string, IReadOnlyList<MapGraphEdgeDefinition>>(StringComparer.Ordinal);

        private readonly string _startNodeId;
        private readonly List<string> _validationErrors = new List<string>();
        private readonly IReadOnlyList<string> _validationErrorsView;
        private readonly IReadOnlyList<string> _orderedNodeIds;
        public IReadOnlyList<string> ValidationErrors => _validationErrorsView;
        public IReadOnlyList<string> OrderedNodeIds => _orderedNodeIds;

        public MapGraphService(SO_MapGraphDefinition mapDefinition)
            : this(mapDefinition != null ? mapDefinition.Nodes : null,
                mapDefinition != null ? mapDefinition.Edges : null,
                mapDefinition != null ? mapDefinition.StartNodeId : string.Empty,
                mapDefinition != null ? mapDefinition.Zones : null,
                mapDefinition != null && mapDefinition.IsCommandGraph)
        {
            if (mapDefinition != null && mapDefinition.SchemaVersion != 0 && !mapDefinition.IsCommandGraph)
                _validationErrors.Add("UnsupportedSchema:" + mapDefinition.SchemaVersion);
        }

        /// <summary>为场景生成和构造验证建立图索引，输入列表之后的修改不会改变索引。</summary>
        public MapGraphService(IReadOnlyList<MapGraphNodeDefinition> nodes,
            IReadOnlyList<MapGraphEdgeDefinition> edges, string startNodeId = "",
            IReadOnlyList<MapGraphZoneDefinition> zones = null, bool requireZones = false)
        {
            _startNodeId = NormalizeId(startNodeId);
            _validationErrorsView = _validationErrors.AsReadOnly();
            BuildZoneIndexes(zones);
            BuildNodeIndexes(nodes);
            foreach (var node in _nodesById.Values)
            {
                if (node.ZoneId.Length > 0 && !_zonesById.ContainsKey(node.ZoneId))
                    _validationErrors.Add("UnknownZone:" + node.NodeId + ":" + node.ZoneId);
                if (requireZones && node.ZoneId.Length == 0) _validationErrors.Add("MissingNodeZone:" + node.NodeId);
            }
            BuildEdgeIndexes(edges);
            foreach (var pair in _edgesByNodeId) _edgeViews.Add(pair.Key, pair.Value.AsReadOnly());
            var ordered = new List<string>(_nodesById.Keys);
            ordered.Sort(StringComparer.Ordinal);
            _orderedNodeIds = ordered.AsReadOnly();
            if (ordered.Count == 0) _validationErrors.Add("EmptyGraph");
            if (_startNodeId.Length > 0 && !_nodesById.ContainsKey(_startNodeId))
                _validationErrors.Add("UnknownStartNode:" + _startNodeId);
        }

        /// <summary>
        /// 当前服务是否持有有效图配置
        /// </summary>
        public bool IsValid => _nodesById.Count > 0 && _validationErrors.Count == 0;

        /// <summary>
        /// 返回图中全部节点 ID
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetNodeIds()
        {
            return _orderedNodeIds;
        }

        /// <summary>
        /// 解析图默认起点
        /// 优先使用配置中的 StartNodeId，其次回退到第一个 Start 类型节点
        /// </summary>
        /// <returns></returns>
        public string GetStartNodeId()
        {
            if (_startNodeId.Length > 0 && _nodesById.ContainsKey(_startNodeId))
            {
                return _startNodeId;
            }

            foreach (string nodeId in _orderedNodeIds)
            {
                MapGraphNodeDefinition node = _nodesById[nodeId];
                if (node.NodeKind == MapGraphNodeKind.Start)
                    return node.NodeId;
            }

            return _orderedNodeIds.Count > 0 ? _orderedNodeIds[0] : string.Empty;
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

        public bool TryGetZone(string zoneId, out MapGraphZoneDefinition zone)
            => _zonesById.TryGetValue(NormalizeId(zoneId), out zone);

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
            return _edgeViews.TryGetValue(NormalizeId(nodeId), out IReadOnlyList<MapGraphEdgeDefinition> edges)
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
                ? nodeDefinition.Position + (_zonesById.TryGetValue(nodeDefinition.ZoneId, out var zone)
                    ? zone.Bounds.center : Vector2.zero)
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

        private void BuildZoneIndexes(IReadOnlyList<MapGraphZoneDefinition> zones)
        {
            if (zones == null) return;
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || string.IsNullOrWhiteSpace(zone.ZoneId) || zone.ZoneId != zone.ZoneId.Trim())
                    _validationErrors.Add("InvalidZoneId:" + i);
                else if (_zonesById.ContainsKey(zone.ZoneId)) _validationErrors.Add("DuplicateZone:" + zone.ZoneId);
                else _zonesById.Add(zone.ZoneId, zone);
            }
        }

        private void BuildNodeIndexes(IReadOnlyList<MapGraphNodeDefinition> nodes)
        {
            if (nodes == null)
                return;

            for (int index = 0; index < nodes.Count; index++)
            {
                MapGraphNodeDefinition node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId) || node.NodeId != node.NodeId.Trim())
                {
                    _validationErrors.Add("InvalidNodeId:" + index);
                    continue;
                }
                if (_nodesById.ContainsKey(node.NodeId))
                {
                    _validationErrors.Add("DuplicateNode:" + node.NodeId);
                    continue;
                }

                _nodesById.Add(node.NodeId, node);
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
                    edge.EdgeId != edge.EdgeId.Trim() ||
                    !_nodesById.ContainsKey(edge.FromNodeId) ||
                    !_nodesById.ContainsKey(edge.ToNodeId))
                {
                    _validationErrors.Add("InvalidEdge:" + index);
                    continue;
                }
                if (edge.FromNodeId == edge.ToNodeId)
                {
                    _validationErrors.Add("SelfEdge:" + edge.EdgeId);
                    continue;
                }
                if (_edgesById.ContainsKey(edge.EdgeId))
                {
                    _validationErrors.Add("DuplicateEdgeId:" + edge.EdgeId);
                    continue;
                }
                if (TryGetEdgeBetween(edge.FromNodeId, edge.ToNodeId, out _))
                {
                    _validationErrors.Add("DuplicateConnection:" + edge.EdgeId);
                    continue;
                }

                _edgesById.Add(edge.EdgeId, edge);
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
