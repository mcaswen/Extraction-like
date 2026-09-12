using System;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphLayoutSolverTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Pure orthogonal solver."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");

        private static MapGraphLayoutDraft Reference(Vector2[] points, bool locked = false, bool twoZones = false, bool emptyZone = false)
        {
            var zones = new List<MapGraphZoneDefinition>
            {
                new MapGraphZoneDefinition("west", "区域甲", new Rect(-200, -200, 400, 400), new Vector2(100, 28), false, "zone-source-west")
            };
            if (twoZones) zones.Add(new MapGraphZoneDefinition("east", "区域乙", new Rect(200, -100, 400, 400), new Vector2(100, 28), false, "zone-source-east"));
            if (emptyZone) zones.Add(new MapGraphZoneDefinition("empty", "空区域", new Rect(100, 100, 180, 120), new Vector2(100, 28)));
            var nodes = new List<MapGraphNodeDefinition>(); var lines = new List<MapGraphAlignmentConstraint>();
            for (int i = 0; i < points.Length; i++)
            {
                string id = ((char)('A' + i)).ToString();
                var zone = zones[twoZones && i >= points.Length / 2 ? 1 : 0];
                nodes.Add(new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, points[i] - zone.Bounds.center, id,
                    zoneId: zone.ZoneId, rowId: "reference-row-" + id, columnId: "reference-column-" + id,
                    footprint: new Vector2(28, 28), positionLocked: locked, sourceObjectId: "source-" + id));
                lines.Add(new MapGraphAlignmentConstraint("reference-row-" + id, MapGraphAxis.Horizontal, points[i].y));
                lines.Add(new MapGraphAlignmentConstraint("reference-column-" + id, MapGraphAxis.Vertical, points[i].x));
            }
            return new MapGraphLayoutDraft(zones, nodes, Array.Empty<MapGraphEdgeDefinition>(), new MapGraphLayoutConstraints(lines, null));
        }
        private static MapGraphEdgeDefinition Edge(string a, string b, MapGraphAxis axis = MapGraphAxis.Unspecified,
            MapGraphEdgeOrigin origin = MapGraphEdgeOrigin.Generated) => new MapGraphEdgeDefinition(a + b, a, b, 1, axis, origin);
        private static MapGraphOrthogonalLayoutSolver Run(MapGraphLayoutDraft reference, MapGraphEdgeDefinition[] edges,
            int advance = 32, int states = 12000)
        {
            var solver = new MapGraphOrthogonalLayoutSolver(reference, edges, new MapGraphGenerationSettings(), states);
            for (int i = 0; !solver.IsComplete && i < states + 5; i++)
            {
                int before = solver.SearchStates;
                Assert.That(solver.Advance(advance), Is.LessThanOrEqualTo(advance));
                Assert.That(solver.SearchStates - before, Is.LessThanOrEqualTo(advance));
            }
            Assert.That(solver.IsComplete, Is.True);
            CaseArtifactWriter.Trace("solver", "states=" + solver.SearchStates + "; valid=" + solver.FeasibleLayouts +
                "; ms=" + solver.ElapsedMilliseconds + "; failures=" + string.Join(";", solver.FailureCounts.Select(p => p.Key + "=" + p.Value)));
            return solver;
        }
        private static void Valid(MapGraphOrthogonalLayoutSolver solver, int nodes, int edges)
        {
            Assert.That(solver.Result, Is.Not.Null, string.Join("\n", solver.FailureCounts.Keys));
            Assert.That(solver.Result.Nodes.Count, Is.EqualTo(nodes)); Assert.That(solver.Result.Edges.Count, Is.EqualTo(edges));
            var report = MapGraphValidation.Validate(solver.Result);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
        }

        [Test] public void ThreeClusterRouteHasSingleSegmentsAndCenteredNames()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 10), new Vector2(110, 100) });
            var solver = Run(reference, new[] { Edge("A", "B"), Edge("B", "C") });
            Valid(solver, 3, 2);
            foreach (var zone in solver.Result.Zones) Assert.That(zone.NameSafeBounds.center, Is.EqualTo(zone.Bounds.center));
            Assert.That(solver.Result.Edges.Select(e => e.Axis).Distinct().Count(), Is.EqualTo(2));
        }

        [Test] public void OverlappingReferencePointsRemainDistinctRealClusters()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.zero, Vector2.zero });
            var solver = Run(reference, new[] { Edge("A", "B"), Edge("B", "C") });
            Valid(solver, 3, 2);
            Assert.That(solver.Result.Nodes.Select(n => n.SourceObjectId), Is.EqualTo(reference.Nodes.Select(n => n.SourceObjectId)));
            Assert.That(solver.Result.Nodes.Select(n => solver.Result.Graph.GetNodePosition(n.NodeId)).Distinct().Count(), Is.EqualTo(3));
        }

        [Test] public void FourPortsCanFitButFiveCannotBeInvented()
        {
            var points = new[] { Vector2.zero, Vector2.right * 100, Vector2.left * 100, Vector2.up * 100, Vector2.down * 100 };
            var edges = new[] { Edge("A", "B"), Edge("A", "C"), Edge("A", "D"), Edge("A", "E") };
            Valid(Run(Reference(points), edges), 5, 4);
            var five = Run(Reference(points.Concat(new[] { Vector2.one * 100 }).ToArray()), edges.Concat(new[] { Edge("A", "F") }).ToArray());
            Assert.That(five.Result, Is.Null);
            Assert.That(five.FailureCounts.ContainsKey("MoreThanFourPorts:A"), Is.True);
            Assert.That(five.SearchStates, Is.Zero);
        }

        [Test] public void CrossZoneEdgesUseGlobalAlignmentCoordinates()
        {
            var reference = Reference(new[] { new Vector2(-80, 50), new Vector2(80, 50), new Vector2(260, 140), new Vector2(400, 140) }, twoZones: true);
            var solver = Run(reference, new[] { Edge("A", "B"), Edge("B", "C"), Edge("C", "D") });
            Valid(solver, 4, 3);
            var edge = solver.Result.Edges.Single(e => e.EdgeId == "BC");
            solver.Result.Graph.TryGetNode("B", out var first); solver.Result.Graph.TryGetNode("C", out var second);
            Assert.That(first.ZoneId, Is.Not.EqualTo(second.ZoneId));
            Assert.That(edge.Axis == MapGraphAxis.Horizontal ? first.RowId == second.RowId : first.ColumnId == second.ColumnId, Is.True);
        }

        [Test] public void ConflictingLockedPositionsNeverModifyTheInput()
        {
            var points = new[] { Vector2.zero, new Vector2(100, 40) };
            var reference = Reference(points, locked: true);
            var solver = Run(reference, new[] { Edge("A", "B", MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual) });
            Assert.That(solver.Result, Is.Null);
            Assert.That(solver.FailureCounts.Keys.Any(k => k.StartsWith("LockedAlignmentConflict")), Is.True);
            Assert.That(reference.Graph.GetNodePosition("A"), Is.EqualTo(points[0]));
            Assert.That(reference.Graph.GetNodePosition("B"), Is.EqualTo(points[1]));
            Assert.That(reference.Edges, Is.Empty);
        }

        [Test] public void BudgetExhaustionAndCancellationNeverPublishPartialGeometry()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.one * 100, Vector2.right * 200 });
            var edges = new[] { Edge("A", "B"), Edge("B", "C") };
            var limited = Run(reference, edges, states: 1);
            Assert.That(limited.BudgetExhausted, Is.True); Assert.That(limited.Result, Is.Null); Assert.That(limited.SearchStates, Is.EqualTo(1));
            var cancelled = new MapGraphOrthogonalLayoutSolver(reference, edges, new MapGraphGenerationSettings());
            cancelled.Advance(1); cancelled.Cancel();
            Assert.That(cancelled.IsCancelled && !cancelled.IsComplete, Is.True);
            Assert.That(cancelled.Advance(100), Is.Zero); Assert.That(cancelled.Result, Is.Null);
        }

        [Test] public void AdvancingWithDifferentBudgetsProducesTheSameLayout()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 10), new Vector2(110, 100) });
            var edges = new[] { Edge("A", "B"), Edge("B", "C") };
            var first = Run(reference, edges, 1); var second = Run(reference, edges, 100);
            Valid(first, 3, 2); Valid(second, 3, 2);
            Assert.That(first.SearchStates, Is.EqualTo(second.SearchStates)); Assert.That(first.BestScore, Is.EqualTo(second.BestScore));
            Assert.That(first.Result.Nodes.Select(n => first.Result.Graph.GetNodePosition(n.NodeId)),
                Is.EqualTo(second.Result.Nodes.Select(n => second.Result.Graph.GetNodePosition(n.NodeId))));
        }

        [Test] public void EmptyAuthoredZoneSurvivesWithoutObscuringTheRoute()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 10), new Vector2(110, 100) }, emptyZone: true);
            var solver = Run(reference, new[] { Edge("A", "B"), Edge("B", "C") });
            Valid(solver, 3, 2);
            Assert.That(solver.Result.Zones.Any(z => z.ZoneId == "empty"), Is.True);
            Assert.That(solver.Result.Nodes.Any(n => n.ZoneId == "empty"), Is.False);
        }

        [Test] public void StationaryEdgeThroughNodeDoesNotRepeatTheIterationBudget()
        {
            var reference = Reference(new[] { new Vector2(-150, 80), new Vector2(150, 80), new Vector2(0, 80) }, locked: true);
            var solver = Run(reference, new[] { Edge("A", "B") });
            Assert.That(solver.Result, Is.Null);
            Assert.That(solver.FailureCounts.Keys.Any(k => k.StartsWith("RepeatedCoordinateState:EdgeThroughNode")), Is.True);
            Assert.That(solver.CoordinateIterations, Is.LessThan(10), "The unchanged invalid layout must not consume 128 iterations.");
            var limited = new MapGraphOrthogonalLayoutSolver(reference, new[] { Edge("A", "B") },
                new MapGraphGenerationSettings(), maximumCoordinateIterations: 1);
            while (!limited.IsComplete) limited.Advance(1);
            Assert.That(limited.CoordinateIterations, Is.EqualTo(1)); Assert.That(limited.BudgetExhausted, Is.True);
            Assert.That(limited.Result, Is.Null);
        }

        [Test] public void CoordinateBudgetRetainsAnAlreadyValidatedCandidate()
        {
            var reference = Reference(new[] { new Vector2(-120, 80), new Vector2(120, 80) }, locked: true);
            var solver = new MapGraphOrthogonalLayoutSolver(reference, new[] { Edge("A", "B") },
                new MapGraphGenerationSettings(), maximumCoordinateIterations: 1);
            while (!solver.IsComplete) solver.Advance(1);
            Assert.That(solver.CoordinateIterations, Is.EqualTo(1)); Assert.That(solver.BudgetExhausted, Is.True);
            Valid(solver, 2, 1);
        }

        [Test] public void WorldReferenceKeepsIdentityAndPreviousLockedLayout()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 10) }, locked: true);
            var query = new AgentNavigationProfile(0, -1, 1, 1);
            var nodes = reference.Nodes.Select(n => new MapGraphSceneNode(n.NodeId, n.SourceObjectId, n.ZoneId, n.DisplayName,
                n.NodeId, "", null, new Vector3(300, 0, 400), new Rect(250, 350, 100, 100), n.NodeKind, Array.Empty<MapGraphAnchorCandidate>()));
            var scene = new MapGraphSceneSnapshot("fixture", "scene-guid", "scene", "navigation",
                new[] { new MapGraphSceneZone("west", "zone-source-west", "Zone-区域甲", null, new Rect(200, 300, 200, 200)) }, nodes,
                new[] { new MapGraphSceneProfile(query, MapGraphNavigationCostService.CaptureProfile(query), Array.Empty<string>(), Array.Empty<Vector3>()) }, Array.Empty<string>());
            var created = MapGraphLayoutGenerator.CreateReference(scene, new MapGraphGenerationSettings(), reference);
            Assert.That(created.Graph.GetNodePosition("A"), Is.EqualTo(reference.Graph.GetNodePosition("A")));
            Assert.That(created.Nodes.All(n => n.PositionLocked), Is.True);
            Assert.That(created.Nodes.Select(n => n.SourceObjectId), Is.EqualTo(reference.Nodes.Select(n => n.SourceObjectId)));
            var fresh = MapGraphLayoutGenerator.CreateReference(scene, new MapGraphGenerationSettings());
            Assert.That(fresh.Zones[0].DisplayName, Is.EqualTo("区域甲"));
            Assert.That(fresh.Graph.GetNodePosition("A"), Is.EqualTo(new Vector2(300, 400)));
        }
    }
}
