using System;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphEditorCanvasTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Viewport and domain-reload recovery."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        private static MapGraphLayoutDraft Fixture() => new MapGraphLayoutDraft(
            new[] { new MapGraphZoneDefinition("z", "区域", new Rect(-300, -200, 600, 400), new Vector2(100, 28)) },
            new[] { new MapGraphNodeDefinition("a", MapGraphNodeKind.Resource, new Vector2(-100, 80), zoneId: "z", footprint: Vector2.one * 30),
                new MapGraphNodeDefinition("b", MapGraphNodeKind.Extraction, new Vector2(100, 80), zoneId: "z", footprint: Vector2.one * 30) },
            new[] { new MapGraphEdgeDefinition("ab", "a", "b", 1, MapGraphAxis.Horizontal) });
        [Test] public void ViewportRoundTripAndCursorZoomKeepTheMapPointFixed()
        {
            var canvas = new MapGraphEditorCanvas(); var rect = new Rect(40, 23, 900, 600); canvas.Fit(Fixture(), rect);
            var point = new Vector2(71, -96); Assert.That(Vector2.Distance(canvas.ToMap(canvas.ToScreen(point, rect), rect), point), Is.LessThan(0.001));
            var cursor = new Vector2(250, 390); var before = canvas.ToMap(cursor, rect);
            canvas.ZoomAt(cursor, rect, 1.75f); Assert.That(Vector2.Distance(canvas.ToMap(cursor, rect), before), Is.LessThan(0.001));
            Assert.That(canvas.ToScreen(Vector2.up * 10, rect).y, Is.LessThan(canvas.ToScreen(Vector2.zero, rect).y));
        }
        [Test] public void HitTestingPrefersRealNodesThenEdgesThenZones()
        {
            var layout = Fixture(); var canvas = new MapGraphEditorCanvas(); var rect = new Rect(0, 0, 900, 600); canvas.Fit(layout, rect);
            Assert.That(canvas.Hit(layout, canvas.ToScreen(new Vector2(-100, 80), rect), rect).id, Is.EqualTo("a"));
            Assert.That(canvas.Hit(layout, canvas.ToScreen(new Vector2(0, 80), rect), rect).kind, Is.EqualTo(MapGraphSelectionKind.Edge));
            Assert.That(canvas.Hit(layout, canvas.ToScreen(Vector2.zero, rect), rect).kind, Is.EqualTo(MapGraphSelectionKind.Zone));
            Assert.That(canvas.Hit(layout, Vector2.zero, rect).kind, Is.EqualTo(MapGraphSelectionKind.None));
        }
        [Test] public void RecoveryPreservesUncommittedDataButRequiresFreshNavigationEvidence()
        {
            string saved;
            using (var first = new MapGraphEditorDocument()) { first.SetDisplayName("待保存的作者地图"); saved = first.ExportWorkingCopy(); }
            using var restored = new MapGraphEditorDocument(recoveryJson: saved);
            Assert.That(restored.WorkingDefinition.DisplayName, Is.EqualTo("待保存的作者地图")); Assert.That(restored.IsDirty, Is.True);
            Assert.That(restored.PendingGeneration, Is.Null); Assert.That(restored.LastVerifiedInput, Is.Null);
            Assert.That(restored.TryVerifyForSave(out _, out string failure), Is.False); Assert.That(failure, Is.EqualTo("MapNeedsValidation"));
        }
        [Test] public void RecoveryDoesNotEraseAnExternalSourceConflict()
        {
            var source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            try
            {
                var fixture = Fixture(); source.ApplyCommandData("fixture", "原名称", "", fixture.Zones, fixture.Nodes, fixture.Edges, fixture.Constraints, new MapGraphNavigationBakeData());
                string saved, baseline;
                using (var first = new MapGraphEditorDocument(source)) { first.SetDisplayName("作者未保存"); saved = first.ExportWorkingCopy(); baseline = first.SourceBaseline; }
                JsonUtility.FromJsonOverwrite("{\"_displayName\":\"外部新名称\"}", source);
                using var restored = new MapGraphEditorDocument(source, saved, baseline);
                Assert.That(restored.HasSourceConflict, Is.True); Assert.That(restored.WorkingDefinition.DisplayName, Is.EqualTo("作者未保存"));
                Assert.That(source.DisplayName, Is.EqualTo("外部新名称"));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test] public void ExplicitResetUnlocksIntentWithoutChangingGraphIdentityOrCoordinates()
        {
            var original = Fixture(); var locked = MapGraphEditOperations.LockNode(original, "a", true);
            var deleted = MapGraphEditOperations.DeleteEdge(locked, "ab"); var reset = MapGraphEditOperations.ResetOverrides(deleted);
            Assert.That(reset.Nodes[0].PositionLocked, Is.False); Assert.That(reset.Constraints.ExcludedConnections, Is.Empty);
            Assert.That(reset.Edges, Is.Empty); Assert.That(reset.Graph.GetNodePosition("a"), Is.EqualTo(original.Graph.GetNodePosition("a")));
            Assert.Throws<ArgumentException>(() => MapGraphEditOperations.LockNode(original, "missing", true));
            Assert.Throws<ArgumentException>(() => MapGraphEditOperations.StyleEdge(original, "missing", 0, 0, 0, false, Color.white));
        }
    }
}
