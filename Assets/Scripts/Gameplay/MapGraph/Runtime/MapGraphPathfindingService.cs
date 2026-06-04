using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>
    /// 抽象地图最短路径服务
    /// 使用 Dijkstra 计算节点路径，供 UI 路径高亮和 Agent 图上移动表现复用
    /// </summary>
    public sealed class MapGraphPathfindingService
    {
        private readonly MapGraphService _graphService;

        public MapGraphPathfindingService(MapGraphService graphService)
        {
            _graphService = graphService;
        }

        /// <summary>
        /// 从一个节点出发，计算到目标节点的最短路径
        /// 返回结果中的 RemainingNodeIds 不包含起点本身
        /// </summary>
        /// <param name="startNodeId"></param>
        /// <param name="targetNodeId"></param>
        /// <returns></returns>
        public MapGraphResolvedPathPlan ResolveFromNode(string startNodeId, string targetNodeId)
        {
            List<string> pathNodes = FindShortestPath(startNodeId, targetNodeId, out float totalLength);

            if (pathNodes.Count == 0)
                return MapGraphResolvedPathPlan.Invalid(targetNodeId);

            List<string> remainingNodeIds = new List<string>();
            for (int index = 1; index < pathNodes.Count; index++)
                remainingNodeIds.Add(pathNodes[index]);

            return MapGraphResolvedPathPlan.FromNodePath(
                targetNodeId,
                remainingNodeIds,
                totalLength);
        }

        private List<string> FindShortestPath(string startNodeId, string targetNodeId, out float totalLength)
        {
            totalLength = float.MaxValue;

            if (_graphService == null ||
                string.IsNullOrWhiteSpace(startNodeId) ||
                string.IsNullOrWhiteSpace(targetNodeId))
            {
                return new List<string>();
            }

            if (startNodeId == targetNodeId)
            {
                totalLength = 0f;
                return new List<string> { startNodeId };
            }

            Dictionary<string, float> distances = new Dictionary<string, float>();
            Dictionary<string, string> previousByNodeId = new Dictionary<string, string>();
            HashSet<string> unvisitedNodeIds = new HashSet<string>(_graphService.GetNodeIds());

            foreach (string nodeId in unvisitedNodeIds)
                distances[nodeId] = float.MaxValue;

            if (!distances.ContainsKey(startNodeId) || !distances.ContainsKey(targetNodeId))
                return new List<string>();

            distances[startNodeId] = 0f;

            while (unvisitedNodeIds.Count > 0)
            {
                string currentNodeId = GetLowestDistanceNode(unvisitedNodeIds, distances);
                if (string.IsNullOrEmpty(currentNodeId))
                    break;

                if (currentNodeId == targetNodeId)
                    break;

                unvisitedNodeIds.Remove(currentNodeId);

                IReadOnlyList<MapGraphEdgeDefinition> connectedEdges =
                    _graphService.GetConnectedEdges(currentNodeId);
                for (int index = 0; index < connectedEdges.Count; index++)
                {
                    MapGraphEdgeDefinition edge = connectedEdges[index];
                    string neighborNodeId = _graphService.GetOtherNodeId(edge, currentNodeId);
                    if (string.IsNullOrEmpty(neighborNodeId) || !unvisitedNodeIds.Contains(neighborNodeId))
                        continue;

                    float candidateDistance = distances[currentNodeId] + edge.LengthUnits;
                    if (candidateDistance >= distances[neighborNodeId])
                        continue;

                    distances[neighborNodeId] = candidateDistance;
                    previousByNodeId[neighborNodeId] = currentNodeId;
                }
            }

            if (!distances.TryGetValue(targetNodeId, out totalLength) || totalLength == float.MaxValue)
                return new List<string>();

            List<string> path = new List<string>();
            string walkNodeId = targetNodeId;
            while (!string.IsNullOrEmpty(walkNodeId))
            {
                path.Add(walkNodeId);
                if (!previousByNodeId.TryGetValue(walkNodeId, out walkNodeId))
                    break;
            }

            path.Reverse();
            return path;
        }

        private static string GetLowestDistanceNode(
            HashSet<string> candidateNodeIds,
            Dictionary<string, float> distances)
        {
            string bestNodeId = string.Empty;
            float bestDistance = float.MaxValue;

            foreach (string nodeId in candidateNodeIds)
            {
                if (!distances.TryGetValue(nodeId, out float distance))
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestNodeId = nodeId;
            }

            return bestNodeId;
        }
    }

    /// <summary>
    /// 抽象图寻路结果
    /// RemainingNodeIds 是状态机或表现层接下来需要依次抵达的节点序列
    /// </summary>
    public sealed class MapGraphResolvedPathPlan
    {
        private MapGraphResolvedPathPlan() { }

        /// <summary>
        /// 本次寻路的目标节点
        /// </summary>
        public string TargetNodeId { get; private set; }

        /// <summary>
        /// 起点之后需要依次抵达的节点
        /// </summary>
        public List<string> RemainingNodeIds { get; private set; } = new List<string>();

        /// <summary>
        /// 路径估算总长度
        /// </summary>
        public float TotalEstimatedLengthUnits { get; private set; }

        /// <summary>
        /// 寻路是否成功
        /// </summary>
        public bool IsValid { get; private set; }

        public static MapGraphResolvedPathPlan Invalid(string targetNodeId)
        {
            return new MapGraphResolvedPathPlan
            {
                TargetNodeId = targetNodeId ?? string.Empty,
                IsValid = false
            };
        }

        public static MapGraphResolvedPathPlan FromNodePath(
            string targetNodeId,
            List<string> remainingNodeIds,
            float totalEstimatedLengthUnits)
        {
            return new MapGraphResolvedPathPlan
            {
                TargetNodeId = targetNodeId ?? string.Empty,
                RemainingNodeIds = remainingNodeIds ?? new List<string>(),
                TotalEstimatedLengthUnits = Mathf.Max(0f, totalEstimatedLengthUnits),
                IsValid = true
            };
        }
    }
}
