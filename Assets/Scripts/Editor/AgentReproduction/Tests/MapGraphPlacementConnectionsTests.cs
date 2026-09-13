using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphPlacementConnectionsTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Fixed placement with frozen bidirectional navigation measurements."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        private static List<MapGraphScannedConnection> Matrix(MapGraphLayoutDraft draft, Func<string, string, float> cost = null)
        {
            var result = new List<MapGraphScannedConnection>();
            for (int i = 0; i < draft.Nodes.Count; i++) for (int j = i + 1; j < draft.Nodes.Count; j++)
            {
                string a = draft.Nodes[i].NodeId, b = draft.Nodes[j].NodeId; float length = cost?.Invoke(a, b) ?? 10;
                result.Add(new MapGraphScannedConnection("profile", new MapGraphNavigationEdgeBake(MapGraphSceneNavigationScan.ConnectionId(a, b), a, b, Vector3.zero, Vector3.one, length, length)));
            }
            return result;
        }
        private static MapGraphPlacementConnectionPlanner Run(MapGraphLayoutDraft draft, int cells = 0, List<MapGraphScannedConnection> matrix = null, int budget = 512)
        {
            var settings = new MapGraphGenerationSettings(); JsonUtility.FromJsonOverwrite("{\"_maximumSearchStates\":" + budget + "}", settings);
            var planner = new MapGraphPlacementConnectionPlanner(draft, matrix ?? Matrix(draft), new[] { "profile" }, settings, 80, cells);
            var watch = Stopwatch.StartNew(); double max = 0;
            while (!planner.IsComplete && watch.Elapsed.TotalSeconds < 10)
            {
                var step = Stopwatch.StartNew(); Assert.That(planner.Advance(1), Is.LessThanOrEqualTo(1)); max = Math.Max(max, step.Elapsed.TotalMilliseconds);
            }
            Assert.That(planner.IsComplete, Is.True); Assert.That(planner.SearchStates, Is.LessThanOrEqualTo(budget));
            CaseArtifactWriter.Trace("planner-cost", $"states={planner.SearchStates}; work={planner.WorkItems}; ms={watch.Elapsed.TotalMilliseconds}; max-step-ms={max}");
            return planner;
        }
        [Test] public void FixedConnectionsKeepCoordinatesAndPreferUnobstructedNeighbours()
        {
            var draft = MapGraphGridPlacementTests.Fixture(); string before = draft.ContentFingerprint;
            var result = Run(draft).Result;
            Assert.That(result, Is.Not.Null); Assert.That(MapGraphValidation.Validate(result).IsValid, Is.True);
            foreach (var node in draft.Nodes) Assert.That(result.Graph.GetNodePosition(node.NodeId), Is.EqualTo(draft.Graph.GetNodePosition(node.NodeId)));
            Assert.That(result.Edges.Count, Is.EqualTo(2)); Assert.That(result.Graph.TryGetEdgeBetween("a", "c", out _), Is.False);
            Assert.That(result.Edges.All(e => e.Axis == MapGraphAxis.Horizontal), Is.True); Assert.That(draft.ContentFingerprint, Is.EqualTo(before));
        }
        [Test] public void PhysicalIslandsAndManualExclusionsDoNotInventConnections()
        {
            var original = MapGraphGridPlacementTests.Fixture(); var matrix = Matrix(original, (a, b) => b == "c" ? float.PositiveInfinity : 10);
            var island = Run(original, matrix: matrix); Assert.That(island.Result, Is.Not.Null); Assert.That(island.Result.Graph.GetConnectedEdges("c"), Is.Empty);
            Assert.That(island.Diagnostics.Any(d => d.Code == "NavigationDisconnected"), Is.True);
            var excluded = MapGraphEditOperations.DeleteEdge(original, "ab");
            var failure = Run(excluded); Assert.That(failure.Result, Is.Null);
            Assert.That(failure.Diagnostics.Any(d => d.Code == "LostReachableConnectivity"), Is.True);
            Assert.That(excluded.Constraints.IsExcluded("a", "b"), Is.True);
        }
        [Test] public void ManualIdentityDirectionAndStyleSurviveFixedGeneration()
        {
            var original = MapGraphGridPlacementTests.Fixture();
            var manual = new MapGraphEdgeDefinition("manual", "b", "a", 10, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual, 2, 3, 4, true, Color.cyan);
            var draft = new MapGraphLayoutDraft(original.Zones, original.Nodes, new[] { manual }, original.Constraints);
            var result = Run(draft).Result; Assert.That(result, Is.Not.Null); Assert.That(result.Graph.TryGetEdge("manual", out var edge), Is.True);
            Assert.That(edge.FromNodeId, Is.EqualTo("b")); Assert.That(edge.ToInset, Is.EqualTo(3)); Assert.That(edge.ColorOverride, Is.EqualTo(Color.cyan));
            Assert.That(MapGraphLayoutIntentValidation.Validate(result, draft).IsValid, Is.True);
        }
        [Test] public void DiagonalPlacementRequiresExplicitBoundedAdjustment()
        {
            var original = MapGraphGridPlacementTests.Fixture(); var draft = MapGraphGridPlacement.MoveNode(original, "b", new Vector2(0, 160), 80);
            Assert.That(Run(draft).Result, Is.Null);
            var result = Run(draft, 1).Result; Assert.That(result, Is.Not.Null);
            foreach (var node in draft.Nodes)
            {
                var delta = result.Graph.GetNodePosition(node.NodeId) - draft.Graph.GetNodePosition(node.NodeId);
                Assert.That(Mathf.Abs(delta.x), Is.LessThanOrEqualTo(80.001f)); Assert.That(Mathf.Abs(delta.y), Is.LessThanOrEqualTo(80.001f));
            }
            Assert.That(MapGraphValidation.Validate(result).IsValid, Is.True);
            Assert.That(MapGraphNavigationValidation.Validate(result, Matrix(draft).Select(m => m.Edge).ToArray()).IsValid, Is.True);
        }
        [Test] public void PinnedNodesAndZonesCannotMoveToRepairADiagonal()
        {
            var draft = MapGraphGridPlacement.MoveNode(MapGraphGridPlacementTests.Fixture(), "b", new Vector2(0, 160), 80);
            foreach (var node in draft.Nodes.ToArray()) draft = MapGraphEditOperations.LockNode(draft, node.NodeId, true);
            Assert.That(Run(draft, 1).Result, Is.Null);
            draft = MapGraphEditOperations.ResetOverrides(draft); draft = MapGraphEditOperations.LockZone(draft, "z", true);
            Assert.That(Run(draft, 1).Result, Is.Null);
        }
        [Test] public void NameCrossingIsAllowedButUnavailableManualEdgesAreRejected()
        {
            var draft = MapGraphGridPlacementTests.Fixture();
            var points = new Dictionary<string, Vector2> { ["a"] = new Vector2(-160, 0), ["b"] = new Vector2(160, 0), ["c"] = new Vector2(160, 80) };
            draft = MapGraphGridPlacement.WithPositions(draft, points);
            var manual = new MapGraphEdgeDefinition("manual", "a", "b", 10, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual);
            draft = new MapGraphLayoutDraft(draft.Zones, draft.Nodes, new[] { manual }, draft.Constraints);
            foreach (bool keepManual in new[] { true, false })
            {
                var input = keepManual ? draft : new MapGraphLayoutDraft(draft.Zones, draft.Nodes, Array.Empty<MapGraphEdgeDefinition>(), draft.Constraints);
                var result = Run(input).Result; Assert.That(result, Is.Not.Null);
                Assert.That(result.Graph.TryGetEdgeBetween("a", "b", out var crossing), Is.True);
                if (keepManual) Assert.That(crossing.EdgeId, Is.EqualTo("manual"));
                Assert.That(MapGraphGeometry.TryGetVisibleSegment(result, crossing, out var from, out var to), Is.True);
                Assert.That(MapGraphGeometry.SegmentIntersectsRect(from, to, result.Zones[0].NameSafeBounds, 2), Is.True);
                Assert.That(MapGraphValidation.Validate(result).IsValid, Is.True);
                foreach (var point in points) Assert.That(result.Graph.GetNodePosition(point.Key), Is.EqualTo(point.Value));
            }
            var unavailable = Run(draft, matrix: Matrix(draft, (a, b) => float.PositiveInfinity));
            Assert.That(unavailable.Result, Is.Null); Assert.That(unavailable.Diagnostics.Any(d => d.Code == "PinnedConnectionUnavailable"), Is.True);
        }
        [Test] public void BudgetAndCancellationNeverPublishPartialSuggestions()
        {
            var draft = MapGraphGridPlacement.MoveNode(MapGraphGridPlacementTests.Fixture(), "b", new Vector2(0, 160), 80);
            var limited = Run(draft, 1, budget: 1); Assert.That(limited.Result, Is.Null); Assert.That(limited.SearchStates, Is.EqualTo(1));
            Assert.That(limited.Diagnostics.Any(d => d.Code == "PlacementAdjustmentNotFound"), Is.True);
            var cancelled = new MapGraphPlacementConnectionPlanner(draft, Matrix(draft), new[] { "profile" }, new MapGraphGenerationSettings(), 80, 1);
            cancelled.Advance(1); cancelled.Cancel(); Assert.That(cancelled.Advance(100), Is.Zero); Assert.That(cancelled.Result, Is.Null);
            Assert.That(cancelled.IsCancelled, Is.True);
        }
        [Test] public void TwentyEightNodeGridKeepsIncrementalWorkWithinAnInteractiveBudget()
        {
            var zones = new List<MapGraphZoneDefinition>(); var nodes = new List<MapGraphNodeDefinition>();
            for (int z = 0; z < 7; z++)
            {
                string zoneId = "z" + z; zones.Add(new MapGraphZoneDefinition(zoneId, zoneId, new Rect(z * 480 - 160, -160, 320, 320), new Vector2(60, 20)));
                for (int n = 0; n < 4; n++) nodes.Add(new MapGraphNodeDefinition(zoneId + "n" + n, MapGraphNodeKind.Resource,
                    new Vector2(n % 2 == 0 ? -80 : 80, n < 2 ? -80 : 80), zoneId: zoneId));
            }
            var draft = MapGraphGridPlacement.WithPositions(new MapGraphLayoutDraft(zones, nodes, Array.Empty<MapGraphEdgeDefinition>()), null);
            var watch = Stopwatch.StartNew(); var planner = Run(draft, matrix: Matrix(draft, (a, b) => Vector2.Distance(draft.Graph.GetNodePosition(a), draft.Graph.GetNodePosition(b))));
            Assert.That(planner.Result, Is.Not.Null, string.Join(";", planner.Diagnostics)); Assert.That(planner.SearchStates, Is.EqualTo(1));
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(5)); Assert.That(planner.Result.Edges.Count, Is.GreaterThanOrEqualTo(27));
        }

        [Test] public void IncompleteMeasurementsFailBeforeAnyLayoutSearch()
        {
            var draft = MapGraphGridPlacementTests.Fixture(); var planner = Run(draft, matrix: Matrix(draft).Take(1).ToList());
            Assert.That(planner.Result, Is.Null); Assert.That(planner.SearchStates, Is.Zero);
            Assert.That(planner.Diagnostics.Any(d => d.Code == "IncompleteNavigationMatrix"), Is.True);
        }
        [Test] public void FailedAdjustmentReportsTheVisibleDraftInsteadOfAnUnacceptedCandidate()
        {
            var draft = MapGraphGridPlacement.MoveNode(MapGraphGridPlacementTests.Fixture(), "b", Vector2.zero, 80);
            draft = MapGraphEditOperations.LockNode(draft, "a", true); draft = MapGraphEditOperations.LockNode(draft, "c", true);
            // 锁定端点上的错误方向永远不可修，搜索仍能把 b 从名称上挪开，形成更好但无效的候选。
            draft = new MapGraphLayoutDraft(draft.Zones, draft.Nodes,
                new[] { new MapGraphEdgeDefinition("pinned_wrong_axis", "a", "c", 10, MapGraphAxis.Vertical, MapGraphEdgeOrigin.Manual) }, draft.Constraints);
            string before = draft.ContentFingerprint;
            var fixedReport = Run(draft); var adjusted = Run(draft, 1);
            Assert.That(adjusted.Result, Is.Null); Assert.That(adjusted.SearchStates, Is.GreaterThan(1));
            var originalIssues = fixedReport.Diagnostics.Where(i => i.Code != "PlacementConnectionsIncomplete").Select(i => i.ToString());
            Assert.That(adjusted.Diagnostics.Where(i => i.Code != "PlacementAdjustmentNotFound").Select(i => i.ToString()), Is.EquivalentTo(originalIssues));
            Assert.That(adjusted.Diagnostics.Any(i => i.Code == "NodeOverName" && i.SubjectId == "b"), Is.True);
            Assert.That(draft.ContentFingerprint, Is.EqualTo(before));
            CaseArtifactWriter.Trace("failed-adjustment-context", string.Join("\n", adjusted.Diagnostics));
        }
    }
}
