using System.Collections.Generic;
using Gameplay.Agent.Data;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>
    /// 自主决策用的只读路线成本估算
    /// 按作者图和已烘焙的边长计算到目标群的路程与途经节点，不查询 NavMesh、不规划或提交路线
    /// </summary>
    public sealed class AgentRouteCostEstimator
    {
        // 角色离上一处理节点超过该距离时，改用最近锚点作为起点
        private const float LastNodeMaxDistance = 40f;

        private MapGraphPathfindingService _pathfinding;
        private MapGraphService _pathfindingGraph;

        /// <summary>
        /// 估算从角色当前位置到目标的路线长度
        /// 起点优先使用路线最后所在节点，否则使用直线最近的可通行锚点
        /// </summary>
        /// <param name="environment">路线环境</param>
        /// <param name="position">角色当前位置</param>
        /// <param name="lastNodeId">路线最后所在节点，可为空</param>
        /// <param name="target">目标引用</param>
        /// <param name="nodes">输出途经节点，含起点和终点；可为空</param>
        /// <param name="length">输出估算路程</param>
        /// <returns>目标可解析且图上连通时返回 true</returns>
        public bool TryEstimate(AgentRouteEnvironment environment, Vector3 position, string lastNodeId,
            AgentTargetRef target, List<string> nodes, out float length)
        {
            length = float.PositiveInfinity;
            nodes?.Clear();
            if (environment == null || !environment.IsReady ||
                !environment.Targets.TryResolveNode(target, out string goal) ||
                !environment.Targets.TryGetFacts(goal, out var goalFacts) || !goalFacts.CanTraverse)
                return false;

            if (!TryResolveStart(environment, position, lastNodeId, out string start, out float entryLength))
                return false;

            if (_pathfinding == null || !ReferenceEquals(_pathfindingGraph, environment.Graph))
            {
                _pathfinding = new MapGraphPathfindingService(environment.Graph);
                _pathfindingGraph = environment.Graph;
            }

            nodes?.Add(start);
            if (start == goal)
            {
                length = entryLength;
                return true;
            }

            var plan = _pathfinding.ResolveFromNode(start, goal, new TraversableCosts(environment));
            if (!plan.IsValid)
            {
                nodes?.Clear();
                return false;
            }

            if (nodes != null)
                for (int index = 0; index < plan.RemainingNodeIds.Count; index++)
                    nodes.Add(plan.RemainingNodeIds[index]);
            length = entryLength + plan.TotalEstimatedLengthUnits;
            return true;
        }

        private static bool TryResolveStart(AgentRouteEnvironment environment, Vector3 position, string lastNodeId,
            out string start, out float entryLength)
        {
            start = null;
            entryLength = float.PositiveInfinity;
            if (!string.IsNullOrEmpty(lastNodeId) &&
                environment.Targets.TryGetFacts(lastNodeId, out var lastFacts) && lastFacts.CanTraverse)
            {
                float distance = Vector3.Distance(lastFacts.Anchor, position);
                if (distance <= LastNodeMaxDistance)
                {
                    start = lastNodeId;
                    entryLength = distance;
                    return true;
                }
            }

            float bestSqr = float.PositiveInfinity;
            IReadOnlyList<string> ordered = environment.Graph.OrderedNodeIds;
            for (int index = 0; index < ordered.Count; index++)
            {
                if (!environment.Targets.TryGetFacts(ordered[index], out var facts) || !facts.CanTraverse)
                    continue;

                float distanceSqr = (facts.Anchor - position).sqrMagnitude;
                if (distanceSqr >= bestSqr)
                    continue;

                bestSqr = distanceSqr;
                start = ordered[index];
            }

            if (start == null)
                return false;

            entryLength = Mathf.Sqrt(bestSqr);
            return true;
        }

        // 与路线规划一致：两端都可通行的边才计入成本
        private readonly struct TraversableCosts : IMapGraphCostProvider
        {
            private readonly AgentRouteEnvironment _environment;

            public TraversableCosts(AgentRouteEnvironment environment)
            {
                _environment = environment;
            }

            public bool TryGetCost(MapGraphEdgeDefinition edge, string fromNodeId, out float cost)
            {
                cost = 0f;
                return _environment.Targets.TryGetFacts(edge.FromNodeId, out var from) && from.CanTraverse &&
                       _environment.Targets.TryGetFacts(edge.ToNodeId, out var to) && to.CanTraverse &&
                       _environment.Costs.TryGetCost(edge, fromNodeId, out cost);
            }
        }
    }
}
