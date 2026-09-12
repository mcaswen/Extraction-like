using System;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphEditOperationTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Independent authoring edit fixtures."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        private static MapGraphLayoutDraft Fixture(bool locked = false, bool third = false)
        {
            var nodes = new[]
            {
                new MapGraphNodeDefinition("A", MapGraphNodeKind.Resource, new Vector2(-100, 80), zoneId: "zone", rowId: "top", columnId: "left", positionLocked: locked, sourceObjectId: "a-source"),
                new MapGraphNodeDefinition("B", MapGraphNodeKind.Resource, new Vector2(100, 80), zoneId: "zone", rowId: "top", columnId: "right", positionLocked: locked, sourceObjectId: "b-source"),
                new MapGraphNodeDefinition("C", MapGraphNodeKind.Extraction, new Vector2(0, -80), zoneId: "zone", rowId: "bottom", columnId: "middle", sourceObjectId: "c-source")
            }.Take(third ? 3 : 2);
            return new MapGraphLayoutDraft(new[] { new MapGraphZoneDefinition("zone", "区域", new Rect(-320, -200, 640, 400), new Vector2(100, 28), sourceObjectId: "zone-source") }, nodes,
                new[] { new MapGraphEdgeDefinition("AB", "A", "B", 999, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual, 3, 5, 2, true, Color.cyan) },
                new MapGraphLayoutConstraints(new[] { new MapGraphAlignmentConstraint("top", MapGraphAxis.Horizontal, 80), new MapGraphAlignmentConstraint("bottom", MapGraphAxis.Horizontal, -80),
                    new MapGraphAlignmentConstraint("left", MapGraphAxis.Vertical, -100), new MapGraphAlignmentConstraint("right", MapGraphAxis.Vertical, 100), new MapGraphAlignmentConstraint("middle", MapGraphAxis.Vertical, 0) }, null));
        }
        private static MapGraphEditOperation Run(MapGraphLayoutDraft original, MapGraphLayoutDraft intent)
        {
            var edit = new MapGraphEditOperation(original, intent, new MapGraphGenerationSettings(), 7, "test edit");
            for (int i = 0; !edit.IsComplete && i < 1000; i++) Assert.That(edit.Advance(4, 1000), Is.LessThanOrEqualTo(4));
            Assert.That(edit.IsComplete, Is.True);
            if (edit.Result != null) Assert.That(MapGraphValidation.Validate(edit.Result, intent, MapGraphIntentPreservation.AllIntent | MapGraphIntentPreservation.Topology).IsValid, Is.True);
            return edit;
        }
        [Test] public void DragLocksTheRequestedPositionAndAlignsTheOtherEndpoint()
        {
            var original = Fixture(); string before = original.ContentFingerprint; var point = new Vector2(-80, 120);
            var edit = Run(original, MapGraphEditOperations.MoveNode(original, "A", point));
            Assert.That(edit.Result, Is.Not.Null, string.Join("\n", edit.Failures));
            Assert.That(edit.Result.Graph.GetNodePosition("A"), Is.EqualTo(point)); Assert.That(edit.Result.Graph.GetNodePosition("B").y, Is.EqualTo(120).Within(0.001));
            Assert.That(original.ContentFingerprint, Is.EqualTo(before)); Assert.That(edit.Result.Edges.Single().EdgeId, Is.EqualTo("AB"));
        }
        [Test] public void ConflictingLockedEndpointRejectsTheDrag()
        {
            var original = Fixture(true); var edit = Run(original, MapGraphEditOperations.MoveNode(original, "A", new Vector2(-80, 120)));
            Assert.That(edit.Result, Is.Null); Assert.That(edit.Failures, Is.Not.Empty); Assert.That(original.Graph.GetNodePosition("A"), Is.EqualTo(new Vector2(-100, 80)));
        }
        [Test] public void ZoneTranslationCarriesLockedMembersAsOneAuthoredMove()
        {
            var original = Fixture(true); var oldZone = original.Zones.Single(); var delta = new Vector2(200, 150);
            var edit = Run(original, MapGraphEditOperations.MoveZone(original, "zone", new Rect(oldZone.Bounds.position + delta, oldZone.Bounds.size)));
            Assert.That(edit.Result, Is.Not.Null, string.Join("\n", edit.Failures));
            foreach (var node in original.Nodes) Assert.That(edit.Result.Graph.GetNodePosition(node.NodeId), Is.EqualTo(original.Graph.GetNodePosition(node.NodeId) + delta));
            Assert.That(edit.Result.Zones.Single().LayoutLocked, Is.True);
        }
        [Test] public void DeleteAndExplicitReaddRespectTheConnectionTombstone()
        {
            var original = Fixture(); var deleted = Run(original, MapGraphEditOperations.DeleteEdge(original, "AB")).Result;
            Assert.That(deleted.Edges, Is.Empty); Assert.That(deleted.Constraints.IsExcluded("A", "B"), Is.True);
            var restored = Run(deleted, MapGraphEditOperations.AddEdge(deleted, "B", "A", MapGraphAxis.Horizontal)).Result;
            Assert.That(restored.Edges.Count, Is.EqualTo(1)); Assert.That(restored.Constraints.IsExcluded("A", "B"), Is.False);
            Assert.That(restored.Edges[0].Origin, Is.EqualTo(MapGraphEdgeOrigin.Manual));
            Assert.Throws<ArgumentException>(() => MapGraphEditOperations.AddEdge(restored, "A", "B", MapGraphAxis.Horizontal));
        }
        [Test] public void RebindingKeepsTheEdgeIdentityAndItsStyle()
        {
            var original = Fixture(third: true); var edit = Run(original, MapGraphEditOperations.RebindEdge(original, "AB", "A", "C", MapGraphAxis.Horizontal));
            Assert.That(edit.Result, Is.Not.Null, string.Join("\n", edit.Failures)); var edge = edit.Result.Edges.Single();
            Assert.That(edge.EdgeId, Is.EqualTo("AB")); Assert.That(edge.ToNodeId, Is.EqualTo("C")); Assert.That(edge.FromInset, Is.EqualTo(3));
            Assert.That(edge.ToInset, Is.EqualTo(5)); Assert.That(edge.ColorOverride, Is.EqualTo(Color.cyan)); Assert.That(edit.Result.Constraints.IsExcluded("A", "B"), Is.True);
        }
        [Test] public void OversizedLineIsRejectedAndPendingPreviewCanBeCancelled()
        {
            var original = Fixture(); var edit = Run(original, MapGraphEditOperations.StyleEdge(original, "AB", 3, 5, 200, true, Color.red));
            Assert.That(edit.Result, Is.Null);
            var pending = new MapGraphEditOperation(original, MapGraphEditOperations.MoveNode(original, "A", new Vector2(-80, 120)), new MapGraphGenerationSettings(), 1, "move");
            pending.Advance(1); pending.Cancel(); Assert.That(pending.Advance(100), Is.Zero); Assert.That(pending.Result, Is.Null);
        }
        [Test] public void ExplicitDisconnectedGraphIsValidButGenerationStillRequiresConnectivity()
        {
            var original = Fixture(third: true); var layout = MapGraphEditOperations.DeleteEdge(original, "AB");
            var matrix = new[] { new MapGraphNavigationEdgeBake("AB", "A", "B", Vector3.zero, Vector3.right, 10, 10),
                new MapGraphNavigationEdgeBake("AC", "A", "C", Vector3.zero, Vector3.forward, 12, 12), new MapGraphNavigationEdgeBake("BC", "B", "C", Vector3.right, Vector3.forward, 11, 11) };
            var authored = MapGraphNavigationValidation.Validate(layout, matrix, false);
            Assert.That(authored.IsValid, Is.True); Assert.That(authored.Issues.Any(i => i.Code == "AuthoredGraphDisconnected"), Is.True);
            Assert.That(MapGraphNavigationValidation.Validate(layout, matrix).IsValid, Is.False);
        }
    }
}
