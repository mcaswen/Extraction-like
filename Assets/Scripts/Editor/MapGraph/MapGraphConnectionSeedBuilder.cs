using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>确定性选边策略；只构造候选骨架，不解坐标或修改参考布局。</summary>
    internal static class MapGraphConnectionSeedBuilder
    {
        public const int StrategyCount = 4;
        public static string StrategyName(int strategy) => new[] { "Navigation", "AxisWeak", "AxisStrong", "Geographic" }[strategy];

        public static List<MapGraphEdgeDefinition> Build(MapGraphLayoutDraft reference, MapGraphConnectionCandidates candidates,
            int strategy, out string failure)
        {
            failure = null;
            var parent = reference.Nodes.ToDictionary(n => n.NodeId, n => n.NodeId, StringComparer.Ordinal);
            var degrees = reference.Nodes.ToDictionary(n => n.NodeId, n => 0, StringComparer.Ordinal);
            var result = new List<MapGraphEdgeDefinition>(); var chosen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in reference.Edges.Where(MapGraphLayoutIntentValidation.IsPinnedEdge).OrderBy(e => e.EdgeId, StringComparer.Ordinal))
            {
                if (!candidates.TryGet(edge.FromNodeId, edge.ToNodeId, out _)) { failure = "PinnedConnectionUnavailable:" + edge.EdgeId; return null; }
                if (degrees[edge.FromNodeId] >= 4 || degrees[edge.ToNodeId] >= 4) { failure = "PinnedConnectionsExceedPorts:" + edge.EdgeId; return null; }
                Add(edge);
            }
            foreach (var candidate in candidates.Ordered.OrderBy(c => Rank(c, strategy)).ThenBy(c => c.Edge.EdgeId, StringComparer.Ordinal))
            {
                var edge = candidate.Edge;
                if (chosen.Contains(edge.EdgeId) || Root(edge.FromNodeId) == Root(edge.ToNodeId) ||
                    degrees[edge.FromNodeId] >= 4 || degrees[edge.ToNodeId] >= 4) continue;
                Add(edge);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.EdgeId, b.EdgeId));
            return result;

            string Root(string node)
            {
                while (parent[node] != node) { parent[node] = parent[parent[node]]; node = parent[node]; }
                return node;
            }
            void Add(MapGraphEdgeDefinition edge)
            {
                parent[Root(edge.FromNodeId)] = Root(edge.ToNodeId); degrees[edge.FromNodeId]++; degrees[edge.ToNodeId]++;
                chosen.Add(edge.EdgeId); result.Add(edge);
            }
        }

        private static double Rank(MapGraphConnectionCandidates.Candidate candidate, int strategy)
        {
            switch (strategy)
            {
                case 0: return candidate.NavigationLength;
                case 1: return candidate.NavigationLength + candidate.MinorAxisDistance * 0.5;
                case 2: return candidate.NavigationLength + candidate.MinorAxisDistance * 2;
                case 3: return candidate.NavigationLength * 0.4 + candidate.WorldDistance * 0.6 + candidate.MinorAxisDistance;
                default: throw new ArgumentOutOfRangeException(nameof(strategy));
            }
        }
    }
}
