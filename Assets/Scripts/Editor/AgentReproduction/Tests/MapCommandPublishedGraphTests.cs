using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AgentReproduction.Tests
{
    /// <summary>只读验收实际发布资产；独立测量和 Floyd 对照，不生成或保存地图。</summary>
    public sealed class MapCommandPublishedGraphTests
    {
        [Serializable] private sealed class EdgeEvidence
        { public string profile, edge, from, to, failure; public float measured, cached; public bool complete, cacheAvailable; }
        [Serializable] private sealed class RouteEvidence
        { public string profile, from, to; public bool reachable; public float length; public string[] steps; }
        [Serializable] private sealed class CacheEvidence
        { public string profile, savedFingerprint, actualFingerprint, initialReason; public int initiallyPending; public long fallbackQueries; }
        [Serializable] private sealed class Evidence
        {
            public long revision; public int zones, nodes, edges, nameCrossings, components;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public List<EdgeEvidence> directions = new List<EdgeEvidence>();
            public List<RouteEvidence> routes = new List<RouteEvidence>();
            public List<CacheEvidence> caches = new List<CacheEvidence>();
            public List<string> entryToExits = new List<string>();
        }

        [Test] public void PublishedConnectionsMatchNavigationAndAllPairShortestRoutes()
        {
            var run = TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity");
            var snapshot = MapGraphSceneCollector.Capture(scene);
            Assert.That(snapshot.IsValid, Is.True, string.Join(";", snapshot.Diagnostics));
            var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
            Assert.That(binding.IsValid, Is.True, string.Join(";", binding.ValidationErrors));
            var definition = binding.MapDefinition; var layout = MapGraphLayoutDraft.FromDefinition(definition);
            var evidence = new Evidence { revision = definition.Revision, zones = layout.Zones.Count, nodes = layout.Nodes.Count, edges = layout.Edges.Count };
            evidence.errors.AddRange(MapGraphValidation.Validate(layout).Issues.Where(i => i.IsError).Select(i => i.ToString()));
            foreach (var edge in layout.Edges)
                if (MapGraphGeometry.TryGetVisibleSegment(layout, edge, out var from, out var to) &&
                    layout.Zones.Any(z => MapGraphGeometry.SegmentIntersectsRect(from, to, z.NameSafeBounds, edge.WidthOverride > 0 ? edge.WidthOverride : 2)))
                    evidence.nameCrossings++;
            string[] ids = layout.Graph.OrderedNodeIds.ToArray();
            var anchors = binding.TargetBindings.ToDictionary(x => x.NodeId, x =>
            { Assert.That(x.TryGetNavigationAnchor(out var point), Is.True, x.NodeId); return point; });
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            foreach (var profile in snapshot.Profiles)
            {
                var cache = new MapGraphNavigationCostService(definition, binding, profile.QueryProfile, "", "", snapshot.RuntimeNavigationFingerprint);
                var cacheEvidence = new CacheEvidence { profile = profile.Data.ProfileId, savedFingerprint = definition.NavigationBake.RuntimeNavigationFingerprint,
                    actualFingerprint = snapshot.RuntimeNavigationFingerprint, initialReason = cache.LastInvalidationReason, initiallyPending = cache.PendingEdgeCount };
                if (cache.PendingEdgeCount != 0) evidence.warnings.Add("CacheInvalid:" + cache.LastInvalidationReason);
                // 缓存命中由专用 RuntimeCache 用例独立裁决；此处保留诊断，补算后验证功能。
                for (int tick = 0; cache.PendingEdgeCount > 0 && tick <= layout.Edges.Count; tick++)
                    Assert.That(cache.ProcessPending(2), Is.LessThanOrEqualTo(2));
                cacheEvidence.fallbackQueries = cache.CalculationCount; evidence.caches.Add(cacheEvidence);
                var distances = new double[ids.Length, ids.Length];
                for (int a = 0; a < ids.Length; a++) for (int b = 0; b < ids.Length; b++) distances[a, b] = a == b ? 0 : double.PositiveInfinity;
                foreach (var edge in layout.Edges)
                    foreach (string start in new[] { edge.FromNodeId, edge.ToNodeId })
                    {
                        string end = layout.Graph.GetOtherNodeId(edge, start);
                        var measured = AgentNavigationSegmentQuery.Calculate(profile.QueryProfile, anchors[start], anchors[end], buffer);
                        bool available = cache.Snapshot.TryGetCost(edge, start, out float cost);
                        evidence.directions.Add(new EdgeEvidence { profile = profile.Data.ProfileId, edge = edge.EdgeId, from = start, to = end,
                            complete = measured.IsComplete, failure = measured.Failure, measured = measured.IsComplete ? measured.Length : -1, cached = available ? cost : -1, cacheAvailable = available });
                        if (!measured.IsComplete || !available || Math.Abs(cost - measured.Length) > Math.Max(.1, measured.Length * .0001))
                            evidence.errors.Add("EdgeNavigation:" + edge.EdgeId + ":" + start + ":" + measured.Failure);
                        if (measured.IsComplete) distances[Array.IndexOf(ids, start), Array.IndexOf(ids, end)] = measured.Length;
                    }
                // 用独立全对动态规划对照生产 Dijkstra，成本来自重新测得的真实导航边。
                for (int k = 0; k < ids.Length; k++) for (int a = 0; a < ids.Length; a++) for (int b = 0; b < ids.Length; b++)
                    distances[a, b] = Math.Min(distances[a, b], distances[a, k] + distances[k, b]);
                evidence.components = Enumerable.Range(0, ids.Length).Count(a => !Enumerable.Range(0, a).Any(b => !double.IsInfinity(distances[a, b])));
                var planner = new MapGraphPathfindingService(layout.Graph, cache.Snapshot);
                for (int a = 0; a < ids.Length; a++) for (int b = 0; b < ids.Length; b++)
                {
                    if (a == b) continue;
                    var route = planner.ResolveFromNode(ids[a], ids[b]); bool reachable = !double.IsInfinity(distances[a, b]);
                    evidence.routes.Add(new RouteEvidence { profile = profile.Data.ProfileId, from = ids[a], to = ids[b], reachable = route.IsValid,
                        length = route.TotalEstimatedLengthUnits, steps = route.RemainingNodeIds.ToArray() });
                    if (route.IsValid != reachable || reachable && Math.Abs(route.TotalEstimatedLengthUnits - distances[a, b]) > Math.Max(.2, distances[a, b] * .0002))
                        evidence.errors.Add("RouteMismatch:" + ids[a] + ":" + ids[b]);
                    string previous = ids[a];
                    foreach (string step in route.RemainingNodeIds)
                    { if (!layout.Graph.TryGetEdgeBetween(previous, step, out _)) evidence.errors.Add("MissingRouteEdge:" + previous + ":" + step); previous = step; }
                    if (route.IsValid && previous != ids[b]) evidence.errors.Add("WrongDestination:" + ids[b]);
                }
                foreach (var exit in layout.Nodes.Where(n => n.NodeKind == MapGraphNodeKind.Extraction))
                    for (int a = 0; a < profile.AgentOrigins.Count; a++)
                    {
                        var result = AgentNavigationSegmentQuery.Calculate(profile.QueryProfile, profile.AgentOrigins[a], anchors[exit.NodeId], buffer);
                        evidence.entryToExits.Add(profile.AgentSourceIds[a] + " → " + exit.NodeId + ":" + (result.IsComplete ? result.Length.ToString("F2") : result.Failure));
                        if (!result.IsComplete) evidence.errors.Add("UnreachableExtraction:" + exit.NodeId + ":" + result.Failure);
                    }
            }
            File.WriteAllText(Path.Combine(run.outputPath, "published-graph.json"), JsonUtility.ToJson(evidence, true));
            CaseArtifactWriter.Trace("published-graph", $"revision={evidence.revision}; zones={evidence.zones}; nodes={evidence.nodes}; edges={evidence.edges}; components={evidence.components}; directions={evidence.directions.Count}; routes={evidence.routes.Count}; errors={evidence.errors.Count}");
            Assert.That(evidence.errors, Is.Empty, string.Join(";", evidence.errors));
        }
        [TearDown] public void Cleanup()
        { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); CaseArtifactWriter.Complete("COMPLETED"); }
    }
}
