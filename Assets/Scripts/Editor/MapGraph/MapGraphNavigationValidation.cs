using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>独立对照完整导航矩阵和图连接；一次校验一个 profile，不查询 NavMesh。</summary>
    public static class MapGraphNavigationValidation
    {
        public static MapGraphValidationResult Validate(MapGraphLayoutDraft draft,
            IReadOnlyList<MapGraphNavigationEdgeBake> measurements, bool requireCandidateConnectivity = true)
        {
            if (draft == null || measurements == null) throw new ArgumentNullException(draft == null ? nameof(draft) : nameof(measurements));
            var issues = new List<MapGraphValidationIssue>();
            if (!draft.Graph.IsValid)
                return new MapGraphValidationResult(new[] { new MapGraphValidationIssue("InvalidNavigationGraph", "graph") });
            var byPair = new Dictionary<string, MapGraphNavigationEdgeBake>(StringComparer.Ordinal);
            var physical = Adjacency(draft); var eligible = Adjacency(draft); var actual = Adjacency(draft);
            foreach (var sample in measurements)
            {
                if (sample == null || sample.FromNodeId == sample.ToNodeId ||
                    !physical.ContainsKey(sample.FromNodeId) || !physical.ContainsKey(sample.ToNodeId))
                { issues.Add(new MapGraphValidationIssue("InvalidNavigationMeasurement", sample?.EdgeId ?? "null")); continue; }
                if (!byPair.TryAdd(MapGraphGeometry.PairKey(sample.FromNodeId, sample.ToNodeId), sample))
                    issues.Add(new MapGraphValidationIssue("DuplicateNavigationMeasurement", sample.EdgeId));
                if (!Usable(sample)) continue;
                Connect(physical, sample.FromNodeId, sample.ToNodeId);
                if (!draft.Constraints.IsExcluded(sample.FromNodeId, sample.ToNodeId)) Connect(eligible, sample.FromNodeId, sample.ToNodeId);
            }
            int count = draft.Nodes.Count;
            if (byPair.Count != (long)count * (count - 1) / 2)
                issues.Add(new MapGraphValidationIssue("IncompleteNavigationMatrix", "graph", detail: "尚未测完所有群对。"));
            foreach (var edge in draft.Edges)
            {
                if (!byPair.TryGetValue(MapGraphGeometry.PairKey(edge.FromNodeId, edge.ToNodeId), out var sample))
                { issues.Add(new MapGraphValidationIssue("MissingEdgeNavigation", edge.EdgeId)); continue; }
                if (!Usable(sample))
                    issues.Add(new MapGraphValidationIssue("UnavailableEdgeNavigation", edge.EdgeId,
                        detail: sample.ForwardFailure + "/" + sample.ReverseFailure));
                else if (!draft.Constraints.IsExcluded(edge.FromNodeId, edge.ToNodeId)) Connect(actual, edge.FromNodeId, edge.ToNodeId);
            }
            var physicalComponents = Components(physical, out int physicalCount);
            var allowedComponents = Components(eligible, out int eligibleCount);
            var actualComponents = Components(actual, out _);
            if (physicalCount > 1)
                issues.Add(new MapGraphValidationIssue("NavigationDisconnected", "graph", detail: physicalCount + " 个物理分量。", error: false));
            if (eligibleCount > physicalCount)
                issues.Add(new MapGraphValidationIssue("ExclusionsSplitNavigation", "graph", detail: eligibleCount + " 个允许连接分量。", error: false));
            if (requireCandidateConnectivity)
            {
                var owner = new Dictionary<int, string>();
                foreach (string node in draft.Graph.OrderedNodeIds)
                {
                    int component = allowedComponents[node];
                    if (!owner.TryGetValue(component, out string first)) owner.Add(component, node);
                    else if (actualComponents[node] != actualComponents[first])
                        issues.Add(new MapGraphValidationIssue("LostReachableConnectivity", first, node));
                }
            }
            return new MapGraphValidationResult(issues);
        }

        public static bool Usable(MapGraphNavigationEdgeBake sample)
            => sample.ForwardAvailable && sample.ReverseAvailable &&
               MapGraphGeometry.Finite(sample.ForwardLength) && sample.ForwardLength >= 0 &&
               MapGraphGeometry.Finite(sample.ReverseLength) && sample.ReverseLength >= 0;

        private static Dictionary<string, List<string>> Adjacency(MapGraphLayoutDraft draft)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string id in draft.Graph.OrderedNodeIds) result.Add(id, new List<string>());
            return result;
        }
        private static void Connect(Dictionary<string, List<string>> adjacency, string a, string b)
        { adjacency[a].Add(b); adjacency[b].Add(a); }
        private static Dictionary<string, int> Components(Dictionary<string, List<string>> adjacency, out int count)
        {
            count = 0;
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (string node in adjacency.Keys)
            {
                if (result.ContainsKey(node)) continue;
                result.Add(node, count); pending.Enqueue(node);
                while (pending.Count > 0)
                    foreach (string neighbor in adjacency[pending.Dequeue()])
                        if (!result.ContainsKey(neighbor)) { result.Add(neighbor, count); pending.Enqueue(neighbor); }
                count++;
            }
            return result;
        }
    }
}
