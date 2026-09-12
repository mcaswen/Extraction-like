using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>一条图边的方向成本，正向对应配置的 From → To。无效值表示该方向不可用。</summary>
    public readonly struct MapGraphEdgeCost
    {
        public string EdgeId { get; }
        public float ForwardCost { get; }
        public float ReverseCost { get; }

        public MapGraphEdgeCost(string edgeId, float forwardCost, float reverseCost)
        {
            EdgeId = edgeId ?? string.Empty;
            ForwardCost = forwardCost;
            ReverseCost = reverseCost;
        }
    }

    /// <summary>复制输入后的只读成本快照；图形布局和运行时对象均不参与成本查询。</summary>
    public sealed class MapGraphCostSnapshot : IMapGraphCostProvider
    {
        private readonly Dictionary<string, MapGraphEdgeCost> _costs;
        public string ProfileId { get; }
        public long Revision { get; }
        public int Count => _costs.Count;

        public MapGraphCostSnapshot(string profileId, long revision, IEnumerable<MapGraphEdgeCost> costs)
        {
            ProfileId = profileId ?? string.Empty;
            Revision = revision;
            _costs = new Dictionary<string, MapGraphEdgeCost>(StringComparer.Ordinal);
            if (costs == null) throw new ArgumentNullException(nameof(costs));
            foreach (var entry in costs)
            {
                if (string.IsNullOrWhiteSpace(entry.EdgeId))
                    throw new ArgumentException("Cost entry requires an edge ID.", nameof(costs));
                if (_costs.ContainsKey(entry.EdgeId))
                    throw new ArgumentException("Duplicate edge cost: " + entry.EdgeId, nameof(costs));
                _costs.Add(entry.EdgeId, entry);
            }
        }

        public bool TryGetCost(MapGraphEdgeDefinition edge, string fromNodeId, out float cost)
        {
            cost = 0f;
            if (edge == null || !_costs.TryGetValue(edge.EdgeId, out var entry)) return false;
            if (fromNodeId == edge.FromNodeId) cost = entry.ForwardCost;
            else if (fromNodeId == edge.ToNodeId) cost = entry.ReverseCost;
            else return false;
            return cost >= 0f && !float.IsNaN(cost) && !float.IsInfinity(cost);
        }
    }
}
