using System;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphConnectionGeneratorTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Measured candidate fixtures; no scene or NavMesh queries."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");

        private static MapGraphLayoutDraft Reference(Vector2[] points, bool locked = false,
            IEnumerable<MapGraphEdgeDefinition> edges = null, IEnumerable<MapGraphConnectionExclusion> exclusions = null)
        {
            var zone = new MapGraphZoneDefinition("zone", "构造区域", new Rect(-350, -350, 700, 700), new Vector2(100, 28), sourceObjectId: "zone-source");
            var nodes = new List<MapGraphNodeDefinition>(); var lines = new List<MapGraphAlignmentConstraint>();
            for (int i = 0; i < points.Length; i++)
            {
                string id = ((char)('A' + i)).ToString();
                nodes.Add(new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, points[i], id, zoneId: "zone",
                    footprint: Vector2.one * 28, rowId: "r" + id, columnId: "c" + id, positionLocked: locked, sourceObjectId: "source" + id));
                lines.Add(new MapGraphAlignmentConstraint("r" + id, MapGraphAxis.Horizontal, points[i].y));
                lines.Add(new MapGraphAlignmentConstraint("c" + id, MapGraphAxis.Vertical, points[i].x));
            }
            return new MapGraphLayoutDraft(new[] { zone }, nodes, edges ?? Array.Empty<MapGraphEdgeDefinition>(), new MapGraphLayoutConstraints(lines, exclusions));
        }

        private static List<MapGraphScannedConnection> Matrix(MapGraphLayoutDraft reference, string profile = "profile",
            Func<string, string, float> cost = null)
        {
            var result = new List<MapGraphScannedConnection>();
            for (int i = 0; i < reference.Nodes.Count; i++)
                for (int j = i + 1; j < reference.Nodes.Count; j++)
                {
                    string a = reference.Nodes[i].NodeId, b = reference.Nodes[j].NodeId;
                    float distance = cost != null ? cost(a, b) : Vector2.Distance(reference.Graph.GetNodePosition(a), reference.Graph.GetNodePosition(b));
                    var sample = new MapGraphNavigationEdgeBake(MapGraphSceneNavigationScan.ConnectionId(a, b), a, b,
                        Vector3.zero, Vector3.one, distance, distance);
                    result.Add(new MapGraphScannedConnection(profile, sample));
                }
            return result;
        }

        private static MapGraphConnectionGenerator Run(MapGraphLayoutDraft reference, IEnumerable<MapGraphScannedConnection> samples,
            int advance = 31, MapGraphGenerationSettings settings = null, string[] profiles = null)
        {
            settings ??= new MapGraphGenerationSettings();
            var generator = new MapGraphConnectionGenerator(reference, samples, profiles ?? new[] { "profile" }, settings);
            int ticks = 0;
            while (!generator.IsComplete && ticks++ < 15000)
            {
                int before = generator.SearchStates, coordinateBefore = generator.CoordinateIterations;
                Assert.That(generator.Advance(advance), Is.LessThanOrEqualTo(advance));
                Assert.That(generator.SearchStates - before, Is.LessThanOrEqualTo(advance));
                Assert.That(generator.SearchStates - before + generator.CoordinateIterations - coordinateBefore, Is.LessThanOrEqualTo(advance));
                if (!generator.IsComplete) Assert.That(generator.Result, Is.Null);
            }
            Assert.That(generator.IsComplete, Is.True);
            Assert.That(generator.SearchStates, Is.LessThanOrEqualTo(settings.MaximumSearchStates));
            Assert.That(generator.CoordinateIterations, Is.LessThanOrEqualTo(4 * Math.Max(settings.MaximumLayoutIterations, reference.Nodes.Count * 32)));
            CaseArtifactWriter.Trace("joint", "strategy=" + generator.SelectedStrategy + "; states=" + generator.SearchStates +
                "; ms=" + generator.ElapsedMilliseconds + "; attempts=" + string.Join(";", generator.Attempts.Select(a => a.Strategy + ":" + a.Outcome + ":" + string.Join(",", a.Failures))));
            return generator;
        }

        private static void Valid(MapGraphConnectionGenerator generator, MapGraphLayoutDraft reference, IEnumerable<MapGraphScannedConnection> matrix)
        {
            Assert.That(generator.Result, Is.Not.Null, string.Join("\n", generator.Diagnostics) + "\n" + string.Join("\n", generator.Attempts.SelectMany(a => a.Failures)));
            Assert.That(MapGraphValidation.Validate(generator.Result, reference).IsValid, Is.True);
            foreach (var profile in matrix.GroupBy(m => m.ProfileId))
                Assert.That(MapGraphNavigationValidation.Validate(generator.Result, profile.Select(m => m.Edge).ToArray()).IsValid, Is.True);
            Assert.That(generator.Result.Nodes.Select(n => n.SourceObjectId), Is.EquivalentTo(reference.Nodes.Select(n => n.SourceObjectId)));
            Assert.That(generator.Result.Zones.Select(z => z.SourceObjectId), Is.EquivalentTo(reference.Zones.Select(z => z.SourceObjectId)));
        }

        [Test] public void InputOrderingAndAdvanceBudgetsProduceTheSameGraph()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 10), new Vector2(110, 120), new Vector2(240, 130) });
            var matrix = Matrix(reference);
            var first = Run(reference, matrix, 1);
            var reversed = new MapGraphLayoutDraft(reference.Zones.Reverse(), reference.Nodes.Reverse(), reference.Edges, reference.Constraints);
            var second = Run(reversed, matrix.AsEnumerable().Reverse(), 73);
            Valid(first, reference, matrix); Valid(second, reversed, matrix);
            Assert.That(first.SelectedStrategy, Is.EqualTo(second.SelectedStrategy));
            Assert.That(first.SearchStates, Is.EqualTo(second.SearchStates));
            Assert.That(first.Result.Edges.Select(e => e.EdgeId), Is.EqualTo(second.Result.Edges.Select(e => e.EdgeId)));
            foreach (string node in reference.Graph.OrderedNodeIds)
                Assert.That(first.Result.Graph.GetNodePosition(node), Is.EqualTo(second.Result.Graph.GetNodePosition(node)));
        }

        [Test] public void FiveDegreeNavigationMstCanUseAnotherRealConnection()
        {
            var points = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < 5; i++) points.Add(new Vector2(Mathf.Cos(i * Mathf.PI * 2 / 5), Mathf.Sin(i * Mathf.PI * 2 / 5)) * 150);
            var reference = Reference(points.ToArray()); var matrix = Matrix(reference);
            var generator = Run(reference, matrix); Valid(generator, reference, matrix);
            Assert.That(generator.Result.Edges.Count, Is.EqualTo(5));
            Assert.That(generator.Result.Edges.Any(e => e.FromNodeId != "A" && e.ToNodeId != "A"), Is.True);
            foreach (string node in reference.Graph.OrderedNodeIds) Assert.That(generator.Result.Graph.GetConnectedEdges(node).Count, Is.LessThanOrEqualTo(4));
        }

        [Test] public void ManualStylePositionLocksAndExclusionsSurviveJointGeneration()
        {
            var manual = new MapGraphEdgeDefinition("manual", "B", "A", 9999, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual, 3, 5, 2, true, Color.cyan);
            var reference = Reference(new[] { new Vector2(-120, 80), new Vector2(120, 80), new Vector2(120, 240) }, true,
                new[] { manual }, new[] { new MapGraphConnectionExclusion("A", "C") });
            var matrix = Matrix(reference); var generator = Run(reference, matrix); Valid(generator, reference, matrix);
            Assert.That(generator.Result.Graph.TryGetEdge("manual", out var saved), Is.True);
            Assert.That(saved.FromNodeId, Is.EqualTo("B")); Assert.That(saved.ToNodeId, Is.EqualTo("A"));
            Assert.That(saved.WidthOverride, Is.EqualTo(2)); Assert.That(saved.ColorOverride, Is.EqualTo(Color.cyan));
            Assert.That(saved.FromInset, Is.EqualTo(3)); Assert.That(saved.ToInset, Is.EqualTo(5));
            Assert.That(generator.Result.Graph.TryGetEdgeBetween("A", "C", out _), Is.False);
            Assert.That(reference.Edges.Count, Is.EqualTo(1)); Assert.That(reference.Edges[0].Axis, Is.EqualTo(MapGraphAxis.Horizontal));
        }

        [Test] public void APhysicalIslandStaysVisibleWithoutAnInventedBridge()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 100, Vector2.up * 200 });
            var matrix = Matrix(reference, cost: (a, b) => b == "C" ? float.PositiveInfinity : 100);
            var generator = Run(reference, matrix); Valid(generator, reference, matrix);
            Assert.That(generator.Result.Nodes.Count, Is.EqualTo(3)); Assert.That(generator.Result.Edges.Count, Is.EqualTo(1));
            Assert.That(generator.Diagnostics.Any(d => d.Code == "NavigationDisconnected" && !d.IsError), Is.True);
            Assert.That(generator.Result.Graph.GetConnectedEdges("C"), Is.Empty);
        }

        [Test] public void IncompleteMeasurementsCannotMasqueradeAsDisconnectedNavigation()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 100, Vector2.up * 200 });
            var generator = Run(reference, Matrix(reference).Take(2));
            Assert.That(generator.Result, Is.Null); Assert.That(generator.SearchStates, Is.Zero);
            Assert.That(generator.Diagnostics.Any(d => d.Code == "IncompleteNavigationMatrix"), Is.True);
        }

        [Test] public void IncompatibleProfileConnectivityIsReportedBeforeSolving()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 100, Vector2.up * 200 });
            var first = Matrix(reference, "first", (a, b) => a == "A" && b == "C" ? float.PositiveInfinity : 100);
            var second = Matrix(reference, "second", (a, b) => a == "A" && b == "B" ? float.PositiveInfinity : 100);
            var generator = Run(reference, first.Concat(second), profiles: new[] { "second", "first" });
            Assert.That(generator.Result, Is.Null); Assert.That(generator.SearchStates, Is.Zero);
            Assert.That(generator.Diagnostics.Any(d => d.Code == "SharedProfileConnectivityConflict"), Is.True);
        }

        [Test] public void UnreachableManualConnectionIsNotSilentlyDeleted()
        {
            var manual = new MapGraphEdgeDefinition("manual", "A", "B", 1, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual);
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 100 }, edges: new[] { manual });
            var generator = Run(reference, Matrix(reference, cost: (a, b) => float.PositiveInfinity));
            Assert.That(generator.Result, Is.Null); Assert.That(generator.Diagnostics.Any(d => d.Code == "PinnedConnectionUnavailable"), Is.True);
            Assert.That(reference.Edges[0], Is.SameAs(manual));
        }

        [Test] public void BudgetAndCancellationDoNotPublishAPartialCandidate()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 100, Vector2.up * 200 }); var matrix = Matrix(reference);
            var settings = new MapGraphGenerationSettings(); JsonUtility.FromJsonOverwrite("{\"_maximumSearchStates\":1}", settings);
            var limited = Run(reference, matrix, settings: settings);
            Assert.That(limited.Result, Is.Null); Assert.That(limited.SearchStates, Is.EqualTo(1));
            Assert.That(limited.Attempts.Any(a => a.BudgetExhausted), Is.True);
            var cancelled = new MapGraphConnectionGenerator(reference, matrix, new[] { "profile" }, new MapGraphGenerationSettings());
            cancelled.Advance(3); cancelled.Cancel();
            Assert.That(cancelled.IsCancelled, Is.True); Assert.That(cancelled.IsComplete, Is.False);
            Assert.That(cancelled.Advance(100), Is.Zero); Assert.That(cancelled.Result, Is.Null);
        }

        [Test] public void PortFilteringMustNotLoseAnEssentialNavigationBridge()
        {
            var reference = Reference(new[] { Vector2.zero, Vector2.right * 120, Vector2.left * 120,
                Vector2.up * 120, Vector2.down * 120, Vector2.one * 120 });
            var matrix = Matrix(reference, cost: (a, b) => a == "A" ? 120 : float.PositiveInfinity);
            var generator = Run(reference, matrix);
            Assert.That(generator.Result, Is.Null); Assert.That(generator.SearchStates, Is.Zero);
            Assert.That(generator.Attempts.All(a => a.Outcome == "CONNECTIVITY_REJECTED"), Is.True);
            Assert.That(generator.Attempts.SelectMany(a => a.Failures).Any(f => f.StartsWith("LostReachableConnectivity")), Is.True);
        }

        [Test] public void LockedDiagonalCannotBeReplacedWithAVirtualTurn()
        {
            var reference = Reference(new[] { Vector2.zero, new Vector2(100, 40) }, locked: true);
            var generator = Run(reference, Matrix(reference));
            Assert.That(generator.Result, Is.Null);
            Assert.That(generator.Diagnostics.Any(d => d.Code == "NoFeasibleConnectionLayout"), Is.True);
            Assert.That(reference.Nodes.Count, Is.EqualTo(2)); Assert.That(reference.Graph.GetNodePosition("B"), Is.EqualTo(new Vector2(100, 40)));
        }
    }
}
