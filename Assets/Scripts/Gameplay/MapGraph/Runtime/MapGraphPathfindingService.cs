using System;
using System.Collections.Generic;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>基于固定图索引的 Dijkstra。查询工作区归该实例所有，不支持并行重入。</summary>
    public sealed class MapGraphPathfindingService
    {
        private readonly MapGraphService _graph;
        private readonly IMapGraphCostProvider _defaultCosts;
        private readonly IReadOnlyList<string> _nodes;
        private readonly Dictionary<string, int> _indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly double[] _distances;
        private readonly int[] _previous;
        private readonly bool[] _visited;
        private readonly List<string> _path = new List<string>();

        public MapGraphPathfindingService(MapGraphService graphService, IMapGraphCostProvider costs = null)
        {
            _graph = graphService;
            _defaultCosts = costs ?? ConfiguredMapGraphCostProvider.Instance;
            _nodes = graphService != null ? graphService.OrderedNodeIds : Array.Empty<string>();
            _distances = new double[_nodes.Count];
            _previous = new int[_nodes.Count];
            _visited = new bool[_nodes.Count];
            for (int i = 0; i < _nodes.Count; i++) _indexes.Add(_nodes[i], i);
        }

        /// <summary>使用构造时的成本策略。未显式传入时，仅兼容旧图的 LengthUnits。</summary>
        public MapGraphResolvedPathPlan ResolveFromNode(string startNodeId, string targetNodeId)
            => ResolveFromNode(startNodeId, targetNodeId, _defaultCosts);

        /// <summary>剩余节点不含起点；结果复制路径，后续查询或输入集合变化不会改写已返回的结果。</summary>
        public MapGraphResolvedPathPlan ResolveFromNode(string startNodeId, string targetNodeId,
            IMapGraphCostProvider costs)
        {
            if (_graph == null || !_graph.IsValid || costs == null ||
                string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(targetNodeId) ||
                !_indexes.TryGetValue(startNodeId, out int start) || !_indexes.TryGetValue(targetNodeId, out int target))
                return MapGraphResolvedPathPlan.Invalid(targetNodeId);

            for (int i = 0; i < _nodes.Count; i++)
            {
                _distances[i] = double.PositiveInfinity;
                _previous[i] = -1;
                _visited[i] = false;
            }
            _distances[start] = 0d;
            for (int pass = 0; pass < _nodes.Count; pass++)
            {
                int current = -1;
                double minimum = double.PositiveInfinity;
                // 节点索引按 Ordinal 排序，相同成本不依赖资产列表/HashSet 遍历顺序。
                for (int i = 0; i < _nodes.Count; i++)
                    if (!_visited[i] && _distances[i] < minimum) { current = i; minimum = _distances[i]; }
                if (current < 0 || current == target) break;
                _visited[current] = true;
                var edges = _graph.GetConnectedEdges(_nodes[current]);
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    int next = _indexes[_graph.GetOtherNodeId(edge, _nodes[current])];
                    if (_visited[next] || !costs.TryGetCost(edge, _nodes[current], out float cost) ||
                        cost < 0f || float.IsNaN(cost) || float.IsInfinity(cost)) continue;
                    double candidate = minimum + cost;
                    if (candidate >= _distances[next]) continue;
                    _distances[next] = candidate;
                    _previous[next] = current;
                }
            }

            if (double.IsInfinity(_distances[target]) || _distances[target] > float.MaxValue)
                return MapGraphResolvedPathPlan.Invalid(targetNodeId);
            _path.Clear();
            for (int current = target; current != start; current = _previous[current])
            {
                if (current < 0 || _path.Count >= _nodes.Count) return MapGraphResolvedPathPlan.Invalid(targetNodeId);
                _path.Add(_nodes[current]);
            }
            _path.Reverse();
            return MapGraphResolvedPathPlan.FromNodePath(targetNodeId, _path, (float)_distances[target]);
        }
    }

    /// <summary>与搜索工作区隔离的只读路径结果；失效结果不携带部分可执行路径。</summary>
    public sealed class MapGraphResolvedPathPlan
    {
        public string TargetNodeId { get; }
        public IReadOnlyList<string> RemainingNodeIds { get; }
        public float TotalEstimatedLengthUnits { get; }
        public bool IsValid { get; }

        private MapGraphResolvedPathPlan(string target, IReadOnlyList<string> nodes, float length, bool valid)
        {
            TargetNodeId = target ?? string.Empty;
            RemainingNodeIds = nodes;
            TotalEstimatedLengthUnits = length;
            IsValid = valid;
        }

        public static MapGraphResolvedPathPlan Invalid(string targetNodeId)
            => new MapGraphResolvedPathPlan(targetNodeId, Array.Empty<string>(), 0f, false);

        public static MapGraphResolvedPathPlan FromNodePath(string targetNodeId, List<string> remainingNodeIds,
            float totalEstimatedLengthUnits)
        {
            if (totalEstimatedLengthUnits < 0f || float.IsNaN(totalEstimatedLengthUnits) || float.IsInfinity(totalEstimatedLengthUnits))
                return Invalid(targetNodeId);
            var copy = remainingNodeIds != null ? new List<string>(remainingNodeIds) : new List<string>();
            return new MapGraphResolvedPathPlan(targetNodeId, copy.AsReadOnly(), totalEstimatedLengthUnits, true);
        }
    }
}
