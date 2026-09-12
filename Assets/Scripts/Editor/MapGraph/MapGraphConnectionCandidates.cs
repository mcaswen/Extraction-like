using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>已测量候选的只读索引；接受完整矩阵，不执行任何导航查询。</summary>
    internal sealed class MapGraphConnectionCandidates
    {
        internal sealed class Candidate
        {
            public MapGraphEdgeDefinition Edge { get; }
            public double NavigationLength { get; }
            public double WorldDistance { get; }
            public double MinorAxisDistance { get; }
            public Candidate(MapGraphEdgeDefinition edge, double length, Vector2 delta)
            {
                Edge = edge; NavigationLength = length;
                WorldDistance = Math.Sqrt((double)delta.x * delta.x + (double)delta.y * delta.y);
                MinorAxisDistance = Math.Min(Math.Abs(delta.x), Math.Abs(delta.y));
            }
        }

        private readonly Dictionary<string, MapGraphNavigationEdgeBake[]> _profiles = new Dictionary<string, MapGraphNavigationEdgeBake[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, MapGraphNavigationEdgeBake>> _profilePairs = new Dictionary<string, Dictionary<string, MapGraphNavigationEdgeBake>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Candidate> _byPair = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        public IReadOnlyList<Candidate> Ordered { get; private set; } = Array.Empty<Candidate>();
        public MapGraphValidationResult Validation { get; private set; }
        public IEnumerable<string> ProfileIds => _profiles.Keys;

        public MapGraphConnectionCandidates(MapGraphLayoutDraft reference, IEnumerable<MapGraphScannedConnection> measurements,
            IEnumerable<string> profileIds)
        {
            var issues = new List<MapGraphValidationIssue>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in profileIds)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) issues.Add(new MapGraphValidationIssue("InvalidCandidateProfile", id ?? "null"));
            if (ids.Count == 0) issues.Add(new MapGraphValidationIssue("MissingCandidateProfiles", "graph"));
            var samples = measurements.ToArray();
            foreach (var sample in samples)
                if (sample == null || !ids.Contains(sample.ProfileId)) issues.Add(new MapGraphValidationIssue("UnknownCandidateProfile", sample?.ProfileId ?? "null"));
            if (!reference.Graph.IsValid) issues.Add(new MapGraphValidationIssue("InvalidCandidateReference", "graph"));
            if (issues.Count > 0) { Validation = new MapGraphValidationResult(issues); return; }
            var empty = new MapGraphLayoutDraft(reference.Zones, reference.Nodes, Array.Empty<MapGraphEdgeDefinition>(), reference.Constraints, reference.StartNodeId);
            foreach (string id in ids.OrderBy(x => x, StringComparer.Ordinal))
            {
                var matrix = samples.Where(s => s.ProfileId == id).Select(s => s.Edge).ToArray();
                _profiles.Add(id, matrix);
                var report = MapGraphNavigationValidation.Validate(empty, matrix, false);
                // 此处故意使用空边集核验物理矩阵，不是作者最终图；连通性在生成结果上单独验收。
                issues.AddRange(report.Issues.Where(issue => issue.Code != "AuthoredGraphDisconnected"));
                if (report.IsValid) _profilePairs.Add(id, matrix.ToDictionary(e => MapGraphGeometry.PairKey(e.FromNodeId, e.ToNodeId), StringComparer.Ordinal));
            }
            Validation = new MapGraphValidationResult(issues);
            if (!Validation.IsValid) return;
            var candidates = new List<Candidate>();
            foreach (var pair in samples.GroupBy(s => MapGraphGeometry.PairKey(s.Edge.FromNodeId, s.Edge.ToNodeId)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                if (pair.Count() != ids.Count || !pair.All(s => MapGraphNavigationValidation.Usable(s.Edge))) continue;
                var sample = pair.First().Edge;
                string a = sample.FromNodeId, b = sample.ToNodeId;
                if (string.CompareOrdinal(a, b) > 0) { string swap = a; a = b; b = swap; }
                if (reference.Constraints.IsExcluded(a, b)) continue;
                MapGraphEdgeDefinition edge;
                if (reference.Graph.TryGetEdgeBetween(a, b, out var saved))
                    edge = MapGraphLayoutIntentValidation.IsPinnedEdge(saved) ? saved :
                        new MapGraphEdgeDefinition(saved.EdgeId, a, b, 1, origin: MapGraphEdgeOrigin.Generated);
                else edge = new MapGraphEdgeDefinition(MapGraphSceneNavigationScan.ConnectionId(a, b), a, b, 1, origin: MapGraphEdgeOrigin.Generated);
                double length = pair.Max(s => ((double)s.Edge.ForwardLength + s.Edge.ReverseLength) * 0.5);
                var candidate = new Candidate(edge, length, reference.Graph.GetNodePosition(b) - reference.Graph.GetNodePosition(a));
                candidates.Add(candidate); _byPair.Add(pair.Key, candidate);
            }
            candidates.Sort((a, b) => string.CompareOrdinal(a.Edge.EdgeId, b.Edge.EdgeId)); Ordered = candidates.AsReadOnly();
            var common = new MapGraphLayoutDraft(reference.Zones, reference.Nodes, candidates.Select(c => c.Edge), reference.Constraints, reference.StartNodeId);
            var commonValidation = Validate(common);
            if (!commonValidation.IsValid)
                issues.Add(new MapGraphValidationIssue("SharedProfileConnectivityConflict", "graph",
                    detail: "所有 profile 共同可用的连接无法保持各自的可达分量。"));
            foreach (var edge in reference.Edges)
                if (MapGraphLayoutIntentValidation.IsPinnedEdge(edge) && !TryGet(edge.FromNodeId, edge.ToNodeId, out _))
                    issues.Add(new MapGraphValidationIssue("PinnedConnectionUnavailable", edge.EdgeId));
            Validation = new MapGraphValidationResult(issues);
        }

        public bool TryGet(string from, string to, out Candidate candidate)
            => _byPair.TryGetValue(MapGraphGeometry.PairKey(from, to), out candidate);

        public bool TryGetLength(string profile, string from, string to, out float length)
        {
            length = float.PositiveInfinity;
            if (!_profilePairs.TryGetValue(profile, out var pairs) || !pairs.TryGetValue(MapGraphGeometry.PairKey(from, to), out var sample)) return false;
            length = sample.FromNodeId == from ? sample.ForwardLength : sample.ReverseLength;
            return MapGraphGeometry.Finite(length) && length >= 0;
        }

        public MapGraphCostSnapshot CreateCostSnapshot(MapGraphLayoutDraft draft, string profile)
        {
            var costs = new List<MapGraphEdgeCost>();
            foreach (var edge in draft.Edges)
            {
                TryGetLength(profile, edge.FromNodeId, edge.ToNodeId, out float forward);
                TryGetLength(profile, edge.ToNodeId, edge.FromNodeId, out float reverse);
                costs.Add(new MapGraphEdgeCost(edge.EdgeId, forward, reverse));
            }
            return new MapGraphCostSnapshot(profile, 0, costs);
        }

        public MapGraphValidationResult Validate(MapGraphLayoutDraft draft)
        {
            var issues = new List<MapGraphValidationIssue>();
            foreach (var profile in _profiles) issues.AddRange(MapGraphNavigationValidation.Validate(draft, profile.Value).Issues);
            return new MapGraphValidationResult(issues);
        }
    }
}
