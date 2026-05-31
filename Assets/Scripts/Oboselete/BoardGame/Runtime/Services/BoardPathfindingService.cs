using System.Collections.Generic;
using System.Linq;
using BoardGame.Config;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 最短路径查询服务，同时处理角色位于边上的改道情况
    /// </summary>
    public sealed class BoardPathfindingService
    {
        private readonly BoardGraphService _graphService;

        public BoardPathfindingService(BoardGraphService graphService)
        {
            _graphService = graphService;
        }

        /// <summary>
        /// 从一个节点出发，计算到目标节点的最短路径
        /// 返回结果中的 RemainingNodeIds 不包含起点本身
        /// </summary>
        public BoardResolvedPathPlan ResolveFromNode(string startNodeId, string targetNodeId)
        {
            List<string> pathNodes = FindShortestPath(startNodeId, targetNodeId, out float totalLength);

            if (pathNodes.Count == 0)
            {
                return BoardResolvedPathPlan.Invalid(targetNodeId);
            }

            return BoardResolvedPathPlan.FromNodePath(
                targetNodeId,
                pathNodes.Skip(1).ToList(),
                totalLength);
        }

        /// <summary>
        /// 从角色当前真实位置出发计算路径
        /// 若角色位于边上，则会同时评估继续前进和原路返回两种出口方案
        /// </summary>
        public BoardResolvedPathPlan ResolveFromAgent(BoardAgentState agentState, string targetNodeId)
        {
            if (!agentState.IsOnEdge)
            {
                return ResolveFromNode(agentState.CurrentNodeId, targetNodeId);
            }

            if (!_graphService.TryGetEdge(agentState.CurrentEdgeId, out BoardMapEdgeDefinition currentEdge))
            {
                return BoardResolvedPathPlan.Invalid(targetNodeId);
            }

            BoardResolvedPathPlan bestPlan = BoardResolvedPathPlan.Invalid(targetNodeId);

            EvaluateEdgeExitCandidate(
                currentEdge,
                currentEdge.FromNodeId,
                agentState.CurrentEdgeProgress01 * currentEdge.LengthUnits,
                0f,
                targetNodeId,
                ref bestPlan);

            EvaluateEdgeExitCandidate(
                currentEdge,
                currentEdge.ToNodeId,
                (1f - agentState.CurrentEdgeProgress01) * currentEdge.LengthUnits,
                1f,
                targetNodeId,
                ref bestPlan);

            return bestPlan;
        }

        /// <summary>
        /// 评估当前边的某一个出口节点是否更优
        /// 总成本 = 先走到出口节点的边上剩余距离 + 出口节点到目标节点的最短路
        /// </summary>
        private void EvaluateEdgeExitCandidate(
            BoardMapEdgeDefinition currentEdge,
            string exitNodeId,
            float edgeCost,
            float edgeTargetProgress01,
            string targetNodeId,
            ref BoardResolvedPathPlan bestPlan)
        {
            List<string> pathNodes = FindShortestPath(exitNodeId, targetNodeId, out float pathCost);

            if (pathNodes.Count == 0)
            {
                return;
            }

            float totalCost = edgeCost + pathCost;

            if (bestPlan.IsValid && totalCost >= bestPlan.TotalEstimatedLengthUnits)
            {
                return;
            }

            // 保留出口节点本身，方便状态机在抵达出口后正确消费第一段路径
            bestPlan = BoardResolvedPathPlan.FromCurrentEdge(
                targetNodeId,
                pathNodes,
                currentEdge.EdgeId,
                edgeTargetProgress01,
                totalCost);
        }

        /// <summary>
        /// 使用 Dijkstra 计算两点之间的最短路径
        /// 返回结果包含起点和终点
        /// </summary>
        private List<string> FindShortestPath(string startNodeId, string targetNodeId, out float totalLength)
        {
            totalLength = float.MaxValue;

            if (string.IsNullOrEmpty(startNodeId) || string.IsNullOrEmpty(targetNodeId))
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
            {
                distances[nodeId] = float.MaxValue;
            }

            distances[startNodeId] = 0f;

            while (unvisitedNodeIds.Count > 0)
            {
                // 每轮取当前未访问节点中距离最短的一个继续向外扩张
                string currentNodeId = GetLowestDistanceNode(unvisitedNodeIds, distances);

                if (string.IsNullOrEmpty(currentNodeId))
                {
                    break;
                }

                if (currentNodeId == targetNodeId)
                {
                    break;
                }

                unvisitedNodeIds.Remove(currentNodeId);

                foreach (BoardMapEdgeDefinition edge in _graphService.GetConnectedEdges(currentNodeId))
                {
                    // 当前原型中的边全部视为双向可达
                    string neighborNodeId = ResolveReachableNeighbor(edge, currentNodeId);

                    if (string.IsNullOrEmpty(neighborNodeId) || !unvisitedNodeIds.Contains(neighborNodeId))
                    {
                        continue;
                    }

                    float candidateDistance = distances[currentNodeId] + edge.LengthUnits;

                    if (candidateDistance < distances[neighborNodeId])
                    {
                        distances[neighborNodeId] = candidateDistance;
                        previousByNodeId[neighborNodeId] = currentNodeId;
                    }
                }
            }

            if (!distances.TryGetValue(targetNodeId, out totalLength) || totalLength == float.MaxValue)
            {
                return new List<string>();
            }

            List<string> pathNodes = new List<string>();
            string walkNodeId = targetNodeId;

            while (!string.IsNullOrEmpty(walkNodeId))
            {
                pathNodes.Add(walkNodeId);

                if (!previousByNodeId.TryGetValue(walkNodeId, out walkNodeId))
                {
                    break;
                }
            }

            pathNodes.Reverse();
            return pathNodes;
        }

        /// <summary>
        /// 从候选节点中选出当前累计距离最短的节点
        /// </summary>
        private static string GetLowestDistanceNode(HashSet<string> candidateNodeIds, Dictionary<string, float> distances)
        {
            string bestNodeId = string.Empty;
            float bestDistance = float.MaxValue;

            foreach (string nodeId in candidateNodeIds)
            {
                if (!distances.TryGetValue(nodeId, out float distance))
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNodeId = nodeId;
                }
            }

            return bestNodeId;
        }

        /// <summary>
        /// 获取当前边相对于某个节点的另一端
        /// </summary>
        private static string ResolveReachableNeighbor(BoardMapEdgeDefinition edgeDefinition, string currentNodeId)
        {
            if (edgeDefinition.FromNodeId == currentNodeId)
            {
                return edgeDefinition.ToNodeId;
            }

            if (edgeDefinition.ToNodeId == currentNodeId)
            {
                return edgeDefinition.FromNodeId;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// 状态机消费的寻路结果
    /// UsesCurrentEdge 为 true 时，表示第一段路程仍然落在当前边上
    /// </summary>
    public sealed class BoardResolvedPathPlan
    {
        private BoardResolvedPathPlan() { }

        public string TargetNodeId { get; private set; }
        public List<string> RemainingNodeIds { get; private set; } = new List<string>();
        public bool UsesCurrentEdge { get; private set; }
        public string CurrentEdgeId { get; private set; }
        public float EdgeTargetProgress01 { get; private set; }
        public float TotalEstimatedLengthUnits { get; private set; }
        public bool IsValid { get; private set; }

        public static BoardResolvedPathPlan Invalid(string targetNodeId)
        {
            return new BoardResolvedPathPlan
            {
                TargetNodeId = targetNodeId,
                IsValid = false
            };
        }

        public static BoardResolvedPathPlan FromNodePath(string targetNodeId, List<string> remainingNodeIds, float totalEstimatedLengthUnits)
        {
            return new BoardResolvedPathPlan
            {
                TargetNodeId = targetNodeId,
                RemainingNodeIds = remainingNodeIds ?? new List<string>(),
                TotalEstimatedLengthUnits = Mathf.Max(0f, totalEstimatedLengthUnits),
                UsesCurrentEdge = false,
                IsValid = true
            };
        }

        public static BoardResolvedPathPlan FromCurrentEdge(
            string targetNodeId,
            List<string> remainingNodeIds,
            string currentEdgeId,
            float edgeTargetProgress01,
            float totalEstimatedLengthUnits)
        {
            return new BoardResolvedPathPlan
            {
                TargetNodeId = targetNodeId,
                RemainingNodeIds = remainingNodeIds ?? new List<string>(),
                CurrentEdgeId = currentEdgeId,
                EdgeTargetProgress01 = edgeTargetProgress01,
                TotalEstimatedLengthUnits = Mathf.Max(0f, totalEstimatedLengthUnits),
                UsesCurrentEdge = true,
                IsValid = true
            };
        }
    }
}
