using System.Collections.Generic;
using BoardGame.Config;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 固定地图图结构查询服务
    /// </summary>
    public sealed class BoardGraphService
    {
        private readonly Dictionary<string, BoardMapNodeDefinition> _nodesById =
            new Dictionary<string, BoardMapNodeDefinition>();

        private readonly Dictionary<string, BoardMapEdgeDefinition> _edgesById =
            new Dictionary<string, BoardMapEdgeDefinition>();

        private readonly Dictionary<string, List<BoardMapEdgeDefinition>> _edgesByNodeId =
            new Dictionary<string, List<BoardMapEdgeDefinition>>();

        private readonly SO_BoardGame_MapDefinition _mapDefinition;

        public BoardGraphService(SO_BoardGame_MapDefinition mapDefinition)
        {
            _mapDefinition = mapDefinition;

            // 预构建节点索引，避免运行时反复线性查找
            foreach (BoardMapNodeDefinition node in mapDefinition.Nodes)
            {
                _nodesById[node.NodeId] = node;
                _edgesByNodeId[node.NodeId] = new List<BoardMapEdgeDefinition>();
            }

            // 预构建边索引与邻接表，供寻路和视图高亮复用
            foreach (BoardMapEdgeDefinition edge in mapDefinition.Edges)
            {
                _edgesById[edge.EdgeId] = edge;

                if (_edgesByNodeId.TryGetValue(edge.FromNodeId, out List<BoardMapEdgeDefinition> fromEdges))
                {
                    fromEdges.Add(edge);
                }

                if (_edgesByNodeId.TryGetValue(edge.ToNodeId, out List<BoardMapEdgeDefinition> toEdges))
                {
                    toEdges.Add(edge);
                }
            }
        }

        /// <summary>
        /// 返回地图中全部节点 ID
        /// </summary>
        public IEnumerable<string> GetNodeIds()
        {
            return _nodesById.Keys;
        }

        /// <summary>
        /// 解析 AI 的起始节点
        /// 优先使用显式配置的 StartNodeId，找不到时退回到第一个 Start 类型节点
        /// </summary>
        public string GetStartNodeId()
        {
            if (!string.IsNullOrEmpty(_mapDefinition.StartNodeId) && _nodesById.ContainsKey(_mapDefinition.StartNodeId))
            {
                return _mapDefinition.StartNodeId;
            }

            foreach (BoardMapNodeDefinition node in _nodesById.Values)
            {
                if (node.NodeType == BoardNodeType.Start)
                {
                    return node.NodeId;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 按节点 ID 查询节点定义
        /// </summary>
        public bool TryGetNode(string nodeId, out BoardMapNodeDefinition nodeDefinition)
        {
            return _nodesById.TryGetValue(nodeId, out nodeDefinition);
        }

        /// <summary>
        /// 按边 ID 查询边定义
        /// </summary>
        public bool TryGetEdge(string edgeId, out BoardMapEdgeDefinition edgeDefinition)
        {
            return _edgesById.TryGetValue(edgeId, out edgeDefinition);
        }

        /// <summary>
        /// 查询两个节点之间是否存在可走的边
        /// </summary>
        public bool TryGetEdgeBetween(string firstNodeId, string secondNodeId, out BoardMapEdgeDefinition edgeDefinition)
        {
            edgeDefinition = null;

            if (!_edgesByNodeId.TryGetValue(firstNodeId, out List<BoardMapEdgeDefinition> edges))
            {
                return false;
            }

            foreach (BoardMapEdgeDefinition edge in edges)
            {
                bool matchesForward = edge.FromNodeId == firstNodeId && edge.ToNodeId == secondNodeId;
                bool matchesBackward = edge.FromNodeId == secondNodeId && edge.ToNodeId == firstNodeId;

                if (matchesForward || matchesBackward)
                {
                    edgeDefinition = edge;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取某个节点连出的全部边
        /// </summary>
        public IReadOnlyList<BoardMapEdgeDefinition> GetConnectedEdges(string nodeId)
        {
            if (_edgesByNodeId.TryGetValue(nodeId, out List<BoardMapEdgeDefinition> edges))
            {
                return edges;
            }

            return new List<BoardMapEdgeDefinition>();
        }

        /// <summary>
        /// 获取某个节点的全部相邻节点
        /// 当前原型中所有边都按双向边处理
        /// </summary>
        public List<BoardMapNodeDefinition> GetNeighbors(string nodeId)
        {
            List<BoardMapNodeDefinition> neighbors = new List<BoardMapNodeDefinition>();

            if (!_edgesByNodeId.TryGetValue(nodeId, out List<BoardMapEdgeDefinition> edges))
            {
                return neighbors;
            }

            foreach (BoardMapEdgeDefinition edge in edges)
            {
                if (edge.FromNodeId == nodeId && _nodesById.TryGetValue(edge.ToNodeId, out BoardMapNodeDefinition forwardNode))
                {
                    neighbors.Add(forwardNode);
                    continue;
                }

                if (edge.ToNodeId == nodeId &&
                    _nodesById.TryGetValue(edge.FromNodeId, out BoardMapNodeDefinition backwardNode))
                {
                    neighbors.Add(backwardNode);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 在一条边上，给定一端节点，返回另一端节点 ID
        /// </summary>
        public string GetOtherNodeId(BoardMapEdgeDefinition edgeDefinition, string nodeId)
        {
            if (edgeDefinition.FromNodeId == nodeId)
            {
                return edgeDefinition.ToNodeId;
            }

            if (edgeDefinition.ToNodeId == nodeId)
            {
                return edgeDefinition.FromNodeId;
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取节点的 2D 世界坐标
        /// </summary>
        public Vector2 GetNodePosition(string nodeId)
        {
            return _nodesById.TryGetValue(nodeId, out BoardMapNodeDefinition nodeDefinition)
                ? nodeDefinition.Position
                : Vector2.zero;
        }

        /// <summary>
        /// 根据边和进度插值出边上的当前位置
        /// </summary>
        public Vector2 GetPositionOnEdge(BoardMapEdgeDefinition edgeDefinition, float progress01)
        {
            Vector2 from = GetNodePosition(edgeDefinition.FromNodeId);
            Vector2 to = GetNodePosition(edgeDefinition.ToNodeId);
            return Vector2.Lerp(from, to, Mathf.Clamp01(progress01));
        }
    }
}
