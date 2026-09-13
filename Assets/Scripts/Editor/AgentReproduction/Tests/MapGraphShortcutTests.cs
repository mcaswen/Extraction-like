using System;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphShortcutTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Independent rectangle navigation fixtures."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");

        private static MapGraphLayoutDraft Rectangle(bool locked = false, bool manual = false, bool excluded = false,
            bool extended = false, string obstacle = "", bool lockedColumns = false)
        {
            Vector2 center = obstacle == "Name" ? new Vector2(-100, 0) : Vector2.zero;
            var zone = new MapGraphZoneDefinition("zone", "区域", new Rect(center - new Vector2(320, 220), new Vector2(640, 440)), new Vector2(100, 28), obstacle == "Name");
            var points = new List<Vector2> { new Vector2(-100, 100), new Vector2(100, 100), new Vector2(100, -100), new Vector2(-100, -100) };
            if (extended) points.Add(new Vector2(-240, -100));
            if (obstacle == "Node") points.Add(new Vector2(-100, 0));
            string[] rows = { "top", "top", "bottom", "bottom", extended ? "bottom" : "middle" };
            string[] cols = { "left-a", "right", "right", "left-d", "extra" };
            var nodes = new List<MapGraphNodeDefinition>(); var lines = new Dictionary<string, MapGraphAlignmentConstraint>();
            for (int i = 0; i < points.Count; i++)
            {
                string id = ((char)('A' + i)).ToString();
                nodes.Add(new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, points[i] - center, id, zoneId: "zone",
                    footprint: Vector2.one * 28, rowId: rows[i], columnId: cols[i], positionLocked: locked, sourceObjectId: "source-" + id));
                lines[rows[i]] = new MapGraphAlignmentConstraint(rows[i], MapGraphAxis.Horizontal, points[i].y);
                lines[cols[i]] = new MapGraphAlignmentConstraint(cols[i], MapGraphAxis.Vertical, points[i].x, lockedColumns && (i == 0 || i == 3));
            }
            var edges = new List<MapGraphEdgeDefinition>
            {
                new MapGraphEdgeDefinition("AB", "B", "A", 0.1f, MapGraphAxis.Horizontal, manual ? MapGraphEdgeOrigin.Manual : MapGraphEdgeOrigin.Generated,
                    manual ? 3 : 0, manual ? 5 : 0, manual ? 2 : 0, manual, Color.cyan),
                new MapGraphEdgeDefinition("BC", "B", "C", 0.1f, MapGraphAxis.Vertical, MapGraphEdgeOrigin.Generated),
                new MapGraphEdgeDefinition("CD", "C", "D", 0.1f, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Generated)
            };
            if (extended) edges.Add(new MapGraphEdgeDefinition("DE", "D", "E", 0.1f, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Generated));
            var draft = new MapGraphLayoutDraft(new[] { zone }, nodes, edges, new MapGraphLayoutConstraints(lines.Values,
                excluded ? new[] { new MapGraphConnectionExclusion("A", "D") } : Array.Empty<MapGraphConnectionExclusion>()));
            Assert.That(MapGraphValidation.Validate(draft).IsValid, Is.True, string.Join("\n", MapGraphValidation.Validate(draft).Issues));
            return draft;
        }

        private static List<MapGraphScannedConnection> Matrix(MapGraphLayoutDraft draft, float directLength = 200, bool reverseCheaper = false)
        {
            var result = new List<MapGraphScannedConnection>();
            for (int i = 0; i < draft.Nodes.Count; i++)
                for (int j = i + 1; j < draft.Nodes.Count; j++)
                {
                    string a = draft.Nodes[i].NodeId, b = draft.Nodes[j].NodeId;
                    float length = draft.Graph.TryGetEdgeBetween(a, b, out _) ? 200 : a == "A" && b == "D" ? directLength : 1000;
                    if (b == "E") length = draft.Edges.Count == 4 ? a == "D" ? 200 : a == "A" ? 300 : 1000 : float.PositiveInfinity;
                    var edge = reverseCheaper && a == "A" && b == "D"
                        ? new MapGraphNavigationEdgeBake("DA", "D", "A", Vector3.zero, Vector3.one, 100, directLength)
                        : new MapGraphNavigationEdgeBake(a + b, a, b, Vector3.zero, Vector3.one, length, length);
                    result.Add(new MapGraphScannedConnection("profile", edge));
                }
            return result;
        }

        private static MapGraphShortcutGenerator Run(MapGraphLayoutDraft basis, List<MapGraphScannedConnection> matrix, int advance = 8,
            int searchBudget = 2000, bool disabled = false)
        {
            var settings = new MapGraphGenerationSettings();
            JsonUtility.FromJsonOverwrite(disabled ? "{\"_extraConnectionRatio\":0}" : "{\"_extraConnectionRatio\":1}", settings);
            var catalog = new MapGraphConnectionCandidates(basis, matrix, new[] { "profile" });
            Assert.That(catalog.Validation.IsValid, Is.True);
            var generator = new MapGraphShortcutGenerator(basis, basis, catalog, settings, searchBudget);
            for (int i = 0; !generator.IsComplete && i < 6000; i++)
            {
                int states = generator.SearchStates, iterations = generator.CoordinateIterations;
                Assert.That(generator.Advance(advance), Is.LessThanOrEqualTo(advance));
                Assert.That(generator.SearchStates - states + generator.CoordinateIterations - iterations, Is.LessThanOrEqualTo(advance));
                if (!generator.IsComplete) Assert.That(generator.Result, Is.Null);
            }
            Assert.That(generator.IsComplete, Is.True); Assert.That(generator.Result, Is.Not.Null);
            Assert.That(generator.SearchStates, Is.LessThanOrEqualTo(searchBudget));
            Assert.That(generator.CoordinateIterations, Is.LessThanOrEqualTo(Math.Max(settings.MaximumLayoutIterations, basis.Nodes.Count * 32)));
            Assert.That(MapGraphValidation.Validate(generator.Result, basis).IsValid, Is.True);
            Assert.That(catalog.Validate(generator.Result).IsValid, Is.True);
            foreach (var edge in basis.Edges) Assert.That(generator.Result.Graph.TryGetEdge(edge.EdgeId, out _), Is.True);
            CaseArtifactWriter.Trace("shortcuts", "added=" + generator.AddedConnections + "; states=" + generator.SearchStates + "; ms=" + generator.ElapsedMilliseconds +
                "; attempts=" + string.Join(";", generator.Attempts.Select(a => a.FromNodeId + a.ToNodeId + ":" + a.Outcome + ":" + string.Join(",", a.Details))));
            return generator;
        }

        [Test] public void RectangleClosesARealThreeTimesDetourWithoutMovingIcons()
        {
            var basis = Rectangle(manual: true); var matrix = Matrix(basis); var generator = Run(basis, matrix);
            Assert.That(generator.AddedConnections, Is.EqualTo(1)); Assert.That(generator.SearchStates, Is.Zero);
            Assert.That(generator.Result.Graph.TryGetEdgeBetween("A", "D", out var edge), Is.True); Assert.That(edge.Axis, Is.EqualTo(MapGraphAxis.Vertical));
            Assert.That(generator.Attempts.Single(a => a.Outcome == "ADDED").DetourRatio, Is.EqualTo(3));
            foreach (string node in basis.Graph.OrderedNodeIds) Assert.That(generator.Result.Graph.GetNodePosition(node), Is.EqualTo(basis.Graph.GetNodePosition(node)));
            var catalog = new MapGraphConnectionCandidates(basis, matrix, new[] { "profile" });
            var before = new MapGraphPathfindingService(basis.Graph, catalog.CreateCostSnapshot(basis, "profile")).ResolveFromNode("A", "D");
            var after = new MapGraphPathfindingService(generator.Result.Graph, catalog.CreateCostSnapshot(generator.Result, "profile")).ResolveFromNode("A", "D");
            Assert.That(before.TotalEstimatedLengthUnits, Is.EqualTo(600)); Assert.That(after.TotalEstimatedLengthUnits, Is.EqualTo(200));
            Assert.That(after.RemainingNodeIds, Is.EqualTo(new[] { "D" })); Assert.That(basis.Edges.Count, Is.EqualTo(3));
        }

        [Test] public void LowNavigationBenefitDoesNotAddAVisuallyShortEdge()
        {
            var basis = Rectangle(); var generator = Run(basis, Matrix(basis, 500));
            Assert.That(generator.AddedConnections, Is.Zero); Assert.That(generator.Result, Is.SameAs(basis));
        }

        [Test] public void ReverseDirectionCostAndLegacyDisplayLengthAreHandledSeparately()
        {
            var basis = Rectangle(); var generator = Run(basis, Matrix(basis, 500, true));
            Assert.That(generator.AddedConnections, Is.EqualTo(1));
            Assert.That(generator.Attempts.Single(a => a.Outcome == "ADDED").DetourRatio, Is.EqualTo(6));
        }

        [Test] public void DeletedConnectionAndSeparateLockedColumnsRemainAuthoritative()
        {
            var excluded = Rectangle(excluded: true);
            Assert.That(Run(excluded, Matrix(excluded)).AddedConnections, Is.Zero);
            var locked = Rectangle(lockedColumns: true); var generator = Run(locked, Matrix(locked));
            Assert.That(generator.AddedConnections, Is.Zero);
            Assert.That(generator.Attempts.SelectMany(a => a.Details).Any(d => d.Contains("LockedAlignmentMembershipChanged")), Is.True);
        }

        [Test] public void AnAlignedShortcutCannotCrossLockedNodes()
        {
            var basis = Rectangle(locked: true, obstacle: "Node"); var generator = Run(basis, Matrix(basis));
            Assert.That(generator.AddedConnections, Is.Zero);
            Assert.That(generator.Attempts.SelectMany(a => a.Details).Any(d => d.Contains("EdgeThroughNode")), Is.True);
        }

        [Test] public void AnAlignedShortcutCanCrossALockedZoneName()
        {
            var basis = Rectangle(locked: true, obstacle: "Name"); var generator = Run(basis, Matrix(basis));
            Assert.That(generator.AddedConnections, Is.EqualTo(1));
            Assert.That(generator.Result.Graph.TryGetEdgeBetween("A", "D", out var edge), Is.True);
            Assert.That(MapGraphGeometry.TryGetVisibleSegment(generator.Result, edge, out var from, out var to), Is.True);
            Assert.That(MapGraphGeometry.SegmentIntersectsRect(from, to, basis.Zones[0].NameSafeBounds, 2), Is.True);
            Assert.That(generator.Result.Zones[0].Bounds, Is.EqualTo(basis.Zones[0].Bounds));
            foreach (string node in basis.Graph.OrderedNodeIds)
                Assert.That(generator.Result.Graph.GetNodePosition(node), Is.EqualTo(basis.Graph.GetNodePosition(node)));
        }

        [Test] public void BenefitIsRecheckedAfterAnEarlierShortcutChangesThePath()
        {
            var basis = Rectangle(extended: true); var generator = Run(basis, Matrix(basis));
            Assert.That(generator.AddedConnections, Is.EqualTo(1));
            Assert.That(generator.Attempts.Any(a => a.FromNodeId == "A" && a.ToNodeId == "E" && a.Outcome == "BENEFIT_DISAPPEARED"), Is.True);
        }

        [Test] public void BudgetsAndCancellationKeepTheValidatedBaseIsolated()
        {
            var basis = Rectangle(lockedColumns: true); var matrix = Matrix(basis);
            var limited = Run(basis, matrix, searchBudget: 0);
            Assert.That(limited.Result, Is.SameAs(basis)); Assert.That(limited.BudgetExhausted, Is.True);
            Assert.That(Run(basis, matrix, disabled: true).Attempts, Is.Empty);
            var catalog = new MapGraphConnectionCandidates(basis, matrix, new[] { "profile" });
            var cancelled = new MapGraphShortcutGenerator(basis, basis, catalog, new MapGraphGenerationSettings(), 2000);
            for (int tick = 0; cancelled.SearchStates == 0 && tick < 100; tick++) cancelled.Advance(1);
            Assert.That(cancelled.SearchStates, Is.GreaterThan(0)); int states = cancelled.SearchStates;
            cancelled.Cancel(); Assert.That(cancelled.Advance(100), Is.Zero); Assert.That(cancelled.Result, Is.Null);
            Assert.That(cancelled.SearchStates, Is.EqualTo(states)); Assert.That(basis.Edges.Count, Is.EqualTo(3));
        }

        [Test] public void ShortcutSelectionDoesNotDependOnAdvanceBatchSize()
        {
            var basis = Rectangle(extended: true); var matrix = Matrix(basis);
            var first = Run(basis, matrix, 1); var second = Run(basis, matrix, 64);
            Assert.That(first.Result.Edges.Select(e => e.EdgeId), Is.EqualTo(second.Result.Edges.Select(e => e.EdgeId)));
            Assert.That(first.Attempts.Select(a => a.Outcome), Is.EqualTo(second.Attempts.Select(a => a.Outcome)));
            Assert.That(first.SearchStates, Is.EqualTo(second.SearchStates)); Assert.That(first.CoordinateIterations, Is.EqualTo(second.CoordinateIterations));
        }
    }
}
