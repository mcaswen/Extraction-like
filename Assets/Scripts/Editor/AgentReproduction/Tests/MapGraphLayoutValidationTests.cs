using System;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class MapGraphLayoutValidationTests
    {
        [SetUp] public void SetUp()
        {
            TestRunContext.Load();
            Assert.That(Application.isPlaying, Is.False);
            CaseArtifactWriter.Trace("setup", "Pure layout validation, no scene mutation or Play Mode.");
        }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");

        private static MapGraphEdgeDefinition Edge(string a, string b, MapGraphAxis axis = MapGraphAxis.Horizontal,
            MapGraphEdgeOrigin origin = MapGraphEdgeOrigin.Generated, float inset = 0, float width = 0)
            => new MapGraphEdgeDefinition(a + b, a, b, 9999, axis, origin, inset, inset, width);
        private static readonly Vector2[] DefaultPoints =
        { new Vector2(-120, 60), new Vector2(0, 60), new Vector2(120, 60), new Vector2(120, -70), new Vector2(300, -70) };
        private static MapGraphLayoutDraft Make(Vector2[] points = null, MapGraphEdgeDefinition[] edges = null,
            MapGraphZoneDefinition[] zones = null, bool locks = false, MapGraphConnectionExclusion[] excluded = null)
        {
            points ??= DefaultPoints;
            zones ??= new[]
            {
                new MapGraphZoneDefinition("west", "西区", new Rect(-200, -150, 400, 300), new Vector2(100, 28), false, "west-source"),
                new MapGraphZoneDefinition("east", "东区", new Rect(240, -150, 240, 300), new Vector2(100, 28), locks, "east-source")
            };
            edges ??= new[] { Edge("A", "B", origin: locks ? MapGraphEdgeOrigin.Manual : MapGraphEdgeOrigin.Generated),
                Edge("B", "C"), Edge("C", "D", MapGraphAxis.Vertical), Edge("D", "E") };
            var nodes = new List<MapGraphNodeDefinition>();
            var lines = new Dictionary<string, MapGraphAlignmentConstraint>();
            for (int i = 0; i < points.Length; i++)
            {
                string id = ((char)('A' + i)).ToString();
                var zone = zones[i == 4 ? 1 : 0];
                string row = "row:" + points[i].y.ToString("R", CultureInfo.InvariantCulture);
                string column = "column:" + points[i].x.ToString("R", CultureInfo.InvariantCulture);
                lines[row] = new MapGraphAlignmentConstraint(row, MapGraphAxis.Horizontal, points[i].y, locks && points[i].y == 60);
                lines[column] = new MapGraphAlignmentConstraint(column, MapGraphAxis.Vertical, points[i].x);
                nodes.Add(new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, points[i] - zone.Bounds.center,
                    zoneId: zone.ZoneId, footprint: new Vector2(28, 28), rowId: row, columnId: column,
                    positionLocked: locks && i == 0, sourceObjectId: id + "-source"));
            }
            return new MapGraphLayoutDraft(zones, nodes, edges, new MapGraphLayoutConstraints(lines.Values, excluded), "A");
        }
        private static void Has(MapGraphValidationResult report, string code)
        {
            Assert.That(report.IsValid, Is.False, string.Join("\n", report.Issues));
            Assert.That(report.Issues.Any(i => i.Code == code && i.IsError), Is.True, string.Join("\n", report.Issues));
            CaseArtifactWriter.Trace("expected-rejection", code);
        }
        private static List<MapGraphNavigationEdgeBake> Matrix(MapGraphLayoutDraft draft, Func<string, string, bool> reachable = null)
        {
            var result = new List<MapGraphNavigationEdgeBake>();
            for (int i = 0; i < draft.Nodes.Count; i++)
                for (int j = i + 1; j < draft.Nodes.Count; j++)
                {
                    string a = draft.Nodes[i].NodeId, b = draft.Nodes[j].NodeId;
                    bool usable = reachable == null || reachable(a, b);
                    result.Add(new MapGraphNavigationEdgeBake("scan-" + a + b, a, b, Vector3.zero, Vector3.right,
                        usable ? 12 : float.PositiveInfinity, usable ? 15 : float.PositiveInfinity,
                        usable ? "" : "PathPartial", usable ? "" : "PathPartial"));
                }
            return result;
        }

        [Test] public void ValidCrossZoneRouteTurnsOnlyAtRealNodes()
        {
            var draft = Make();
            var report = MapGraphValidation.Validate(draft);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            Assert.That(MapGraphNavigationValidation.Validate(draft, Matrix(draft)).IsValid, Is.True);
            Assert.That(MapGraphGeometry.TryGetVisibleSegment(draft, draft.Edges[3], out var from, out var to), Is.True);
            Assert.That(from, Is.EqualTo(new Vector2(134, -70)));
            Assert.That(to, Is.EqualTo(new Vector2(286, -70)));
            Assert.That(draft.Graph.GetConnectedEdges("C").Count, Is.EqualTo(2));
            Assert.That(draft.Nodes.Count, Is.EqualTo(5));
        }

        [TestCase("Diagonal", "NonOrthogonalEdge")]
        [TestCase("NodeOverlap", "NodeOverlap")]
        [TestCase("NodeOverName", "NodeOverName")]
        [TestCase("ThroughNode", "EdgeThroughNode")]
        [TestCase("ThroughName", "EdgeThroughName")]
        [TestCase("PortOverlap", "PortOverlap")]
        [TestCase("Insets", "InvalidEdgeSpan")]
        [TestCase("WidthNaN", "InvalidEdgeStyle")]
        [TestCase("WidthTooLarge", "EdgeWiderThanPort")]
        [TestCase("PositionNaN", "InvalidNodeGeometry")]
        [TestCase("OutsideZone", "NodeOutsideZone")]
        public void InvalidGeometryHasSpecificIdentity(string scenario, string code)
        {
            var points = (Vector2[])DefaultPoints.Clone();
            MapGraphEdgeDefinition[] edges = null;
            switch (scenario)
            {
                case "Diagonal": points[1].y += 10; break;
                case "NodeOverlap": points[1] = points[0]; break;
                case "NodeOverName": points[1] = Vector2.zero; break;
                case "ThroughNode": edges = new[] { Edge("A", "C") }; break;
                case "ThroughName": points[0].y = 0; points[2].y = 0; edges = new[] { Edge("A", "C") }; break;
                case "PortOverlap": edges = new[] { Edge("A", "B"), Edge("A", "C") }; break;
                case "Insets": edges = new[] { Edge("A", "B", inset: 100) }; break;
                case "WidthNaN": edges = new[] { Edge("A", "B", width: float.NaN) }; break;
                case "WidthTooLarge": edges = new[] { Edge("A", "B", width: 40) }; break;
                case "PositionNaN": points[0].x = float.NaN; break;
                case "OutsideZone": points[0].x = -300; break;
            }
            var report = MapGraphValidation.Validate(Make(points, edges));
            Has(report, code);
            Assert.That(report.Issues.Where(i => i.Code == code).All(i => !string.IsNullOrEmpty(i.SubjectId)), Is.True);
            if (scenario == "PortOverlap") Has(report, "CollinearEdges");
        }

        [Test] public void ZonesNamesAndAlignmentCoordinatesCannotSilentlyDrift()
        {
            var good = Make(); var zones = good.Zones.ToArray();
            zones[1] = zones[1].WithLayout(new Rect(160, -150, 240, 300), false);
            Has(MapGraphValidation.Validate(Make(zones: zones)), "ZoneOverlap");
            zones[1] = new MapGraphZoneDefinition("east", "东区", good.Zones[1].Bounds, new Vector2(1000, 28));
            Has(MapGraphValidation.Validate(Make(zones: zones)), "NameOutsideZone");
            var shifted = good.Constraints.Alignments.Select(line => new MapGraphAlignmentConstraint(line.Id, line.Axis, line.Coordinate + 1)).ToArray();
            Has(MapGraphValidation.Validate(new MapGraphLayoutDraft(good.Zones, good.Nodes, good.Edges,
                new MapGraphLayoutConstraints(shifted, null))), "AlignmentMismatch");
            var split = good.Nodes.ToArray(); split[1] = split[1].WithLayout(split[1].Position, "independent-row", split[1].ColumnId, false);
            var lines = good.Constraints.Alignments.ToList(); lines.Add(new MapGraphAlignmentConstraint("independent-row", MapGraphAxis.Horizontal, 60));
            Has(MapGraphValidation.Validate(new MapGraphLayoutDraft(good.Zones, split, good.Edges,
                new MapGraphLayoutConstraints(lines, null))), "EdgeAlignmentNotShared");
        }

        [Test] public void RegenerationPreservesLocksManualEdgesAndDeletedConnections()
        {
            var original = Make(locks: true, excluded: new[] { new MapGraphConnectionExclusion("A", "D") });
            var points = (Vector2[])DefaultPoints.Clone(); points[0].x += 10;
            var zones = original.Zones.ToArray(); zones[1] = zones[1].WithLayout(new Rect(260, -150, 240, 300), false);
            var changed = Make(points, original.Edges.Skip(1).ToArray(), zones);
            var report = MapGraphLayoutIntentValidation.Validate(changed, original);
            Has(report, "LockedNodeChanged"); Has(report, "LockedZoneChanged"); Has(report, "LockedAlignmentChanged");
            Has(report, "ManualEdgeChanged"); Has(report, "ExclusionRemoved");
            Assert.That(original.Graph.GetNodePosition("A"), Is.EqualTo(DefaultPoints[0]));
            Assert.That(original.Edges.Count, Is.EqualTo(4));
            Assert.That(original.Constraints.IsExcluded("D", "A"), Is.True);
            Assert.That(MapGraphLayoutIntentValidation.Validate(changed, original, MapGraphIntentPreservation.None).IsValid, Is.True);
        }

        [Test] public void LayoutOnlyModeAndExplicitEdgeEditsHaveDifferentIntentRules()
        {
            var original = Make();
            var edited = Make(edges: original.Edges.Skip(1).ToArray());
            Has(MapGraphLayoutIntentValidation.Validate(edited, original, MapGraphIntentPreservation.Topology), "TopologyEdgeChanged");
            Assert.That(MapGraphLayoutIntentValidation.Validate(edited, original, MapGraphIntentPreservation.Locks).IsValid, Is.True);
            var styled = Make(edges: new[] { Edge("A", "B", width: 3) });
            Has(MapGraphLayoutIntentValidation.Validate(Make(edges: Array.Empty<MapGraphEdgeDefinition>()), styled), "ManualEdgeChanged");
            var forbidden = Make(excluded: new[] { new MapGraphConnectionExclusion("A", "B") });
            Has(MapGraphValidation.Validate(forbidden), "ExcludedConnection");
        }

        [Test] public void IncompleteOrOneWayMeasurementsCannotUseLegacyDrawingLengths()
        {
            var draft = Make(); var matrix = Matrix(draft); matrix.RemoveAt(0);
            Has(MapGraphNavigationValidation.Validate(draft, matrix), "IncompleteNavigationMatrix");
            Has(MapGraphNavigationValidation.Validate(draft, matrix), "MissingEdgeNavigation");
            matrix = Matrix(draft);
            matrix[0] = new MapGraphNavigationEdgeBake("scan-AB", "A", "B", Vector3.zero, Vector3.right,
                12, float.PositiveInfinity, reverseFailure: "PathPartial");
            Has(MapGraphNavigationValidation.Validate(draft, matrix), "UnavailableEdgeNavigation");
        }

        [Test] public void PhysicalIslandsAndDroppedReachableBridgesAreDistinguished()
        {
            var draft = Make(edges: new[] { Edge("A", "B"), Edge("B", "C"), Edge("D", "E") });
            Has(MapGraphNavigationValidation.Validate(draft, Matrix(draft)), "LostReachableConnectivity");
            var disconnected = MapGraphNavigationValidation.Validate(draft, Matrix(draft, (a, b) => (a[0] <= 'C') == (b[0] <= 'C')));
            Assert.That(disconnected.IsValid, Is.True, string.Join("\n", disconnected.Issues));
            Assert.That(disconnected.Issues.Single().Code, Is.EqualTo("NavigationDisconnected"));
            Assert.That(disconnected.Issues.Single().IsError, Is.False);
        }

        [Test] public void ExplicitExclusionsArePreservedWithoutInventingAForbiddenBridge()
        {
            var excluded = new List<MapGraphConnectionExclusion>();
            foreach (string a in new[] { "A", "B", "C" }) foreach (string b in new[] { "D", "E" }) excluded.Add(new MapGraphConnectionExclusion(a, b));
            var draft = Make(edges: new[] { Edge("A", "B"), Edge("B", "C"), Edge("D", "E") }, excluded: excluded.ToArray());
            Assert.That(MapGraphValidation.Validate(draft).IsValid, Is.True);
            var report = MapGraphNavigationValidation.Validate(draft, Matrix(draft));
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues.Single().Code, Is.EqualTo("ExclusionsSplitNavigation"));
        }

        [Test] public void AGeometricCrossingNeverCreatesAnInterchange()
        {
            var points = new[] { new Vector2(-120, 80), new Vector2(120, 80), new Vector2(0, 30), new Vector2(0, 160) };
            var zones = new[] { new MapGraphZoneDefinition("west", "区域", new Rect(-220, -230, 440, 440), new Vector2(100, 28)) };
            var draft = Make(points, new[] { Edge("A", "B"), Edge("C", "D", MapGraphAxis.Vertical) }, zones);
            var report = MapGraphValidation.Validate(draft);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            Assert.That(MapGraphLayoutScore.Evaluate(draft, draft, new MapGraphGenerationSettings()).Crossings, Is.EqualTo(1));
            Assert.That(new MapGraphPathfindingService(draft.Graph).ResolveFromNode("A", "D").IsValid, Is.False);
            Assert.That(MapGraphGeometry.ProperCrossing(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero), Is.False);
        }

        [Test] public void ScoringPenalizesDisplacementReversalAndDistanceWithoutChangingPaths()
        {
            var original = Make(); var settings = new MapGraphGenerationSettings();
            var same = MapGraphLayoutScore.Evaluate(original, original, settings);
            var shifted = Make(DefaultPoints.Select(p => p + Vector2.right * 20).ToArray());
            var shiftScore = MapGraphLayoutScore.Evaluate(shifted, original, settings);
            var reversed = Make(DefaultPoints.Select(p => new Vector2(-p.x, p.y)).ToArray());
            var reverseScore = MapGraphLayoutScore.Evaluate(reversed, original, settings);
            var enlarged = Make(DefaultPoints.Select(p => p * 2).ToArray());
            var enlargedScore = MapGraphLayoutScore.Evaluate(enlarged, original, settings);
            Assert.That(same.IsFinite && shiftScore.IsFinite && reverseScore.IsFinite, Is.True);
            Assert.That(shiftScore.NormalizedDisplacement, Is.GreaterThan(0));
            Assert.That(shiftScore.DirectionReversals, Is.Zero);
            Assert.That(reverseScore.DirectionReversals, Is.GreaterThan(0));
            Assert.That(enlargedScore.DistanceDistortion, Is.GreaterThan(0));
            Assert.That(reverseScore.Total, Is.GreaterThan(same.Total));
            Assert.That(shiftScore.Total, Is.GreaterThan(same.Total));
            Assert.That(MapGraphLayoutScore.Evaluate(original, original, settings).Total, Is.EqualTo(same.Total));
            Assert.That(original.Edges.All(e => e.LengthUnits == 9999), Is.True);
            CaseArtifactWriter.Trace("score", "same=" + same.Total + "; shifted=" + shiftScore.Total + "; reversed=" + reverseScore.Total);
        }
    }
}
