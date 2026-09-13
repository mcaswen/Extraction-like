using System;
using System.Diagnostics;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphGridPlacementTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); CaseArtifactWriter.Trace("setup", "Editor grid draft, no scene or navigation dependency."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        internal static MapGraphLayoutDraft Fixture()
        {
            var draft = new MapGraphLayoutDraft(new[] { new MapGraphZoneDefinition("z", "测试区域", new Rect(-300, -200, 600, 400), new Vector2(60, 20)) },
                new[] { new MapGraphNodeDefinition("a", MapGraphNodeKind.Resource, new Vector2(-160, 80), zoneId: "z"),
                    new MapGraphNodeDefinition("b", MapGraphNodeKind.Resource, new Vector2(0, 80), zoneId: "z"),
                    new MapGraphNodeDefinition("c", MapGraphNodeKind.Extraction, new Vector2(160, 80), zoneId: "z") },
                new[] { new MapGraphEdgeDefinition("ab", "a", "b", 1, MapGraphAxis.Horizontal), new MapGraphEdgeDefinition("bc", "b", "c", 1, MapGraphAxis.Horizontal) });
            return MapGraphGridPlacement.WithPositions(draft, null);
        }
        [Test] public void SnapHandlesNegativeHalfCellsAndViewportScaling()
        {
            Assert.That(MapGraphGridPlacement.Snap(new Vector2(-40, 120), 80), Is.EqualTo(new Vector2(-80, 160)));
            var canvas = new MapGraphEditorCanvas(); var rect = new Rect(20, 40, 900, 600); canvas.Fit(Fixture(), rect);
            var point = new Vector2(-121, 117); var screen = canvas.ToScreen(point, rect);
            Assert.That(MapGraphGridPlacement.Snap(canvas.ToMap(screen, rect), 80), Is.EqualTo(new Vector2(-160, 80)));
            canvas.ZoomAt(screen, rect, 2.7f);
            Assert.That(MapGraphGridPlacement.Snap(canvas.ToMap(screen, rect), 80), Is.EqualTo(new Vector2(-160, 80)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapGraphGridPlacement.Snap(point, 0));
            Assert.Throws<ArgumentException>(() => MapGraphGridPlacement.Snap(new Vector2(float.NaN, 0), 80));
        }
        [Test] public void MovingOneNodeDoesNotSolveOldEdgesOrPinOtherNodes()
        {
            var original = Fixture(); string fingerprint = original.ContentFingerprint;
            var moved = MapGraphGridPlacement.MoveNode(original, "b", new Vector2(78, -77), 80);
            Assert.That(moved.Graph.GetNodePosition("b"), Is.EqualTo(new Vector2(80, -80)));
            foreach (string id in new[] { "a", "c" }) Assert.That(moved.Graph.GetNodePosition(id), Is.EqualTo(original.Graph.GetNodePosition(id)));
            Assert.That(moved.Nodes.All(n => !n.PositionLocked), Is.True);
            Assert.That(moved.Edges.Select(e => e.EdgeId), Is.EqualTo(original.Edges.Select(e => e.EdgeId)));
            Assert.That(MapGraphValidation.Validate(moved).IsValid, Is.False, "草稿允许旧线暂时失效。");
            Assert.That(original.ContentFingerprint, Is.EqualTo(fingerprint));
        }
        [Test] public void ZoneTranslationCarriesMembersButResizingKeepsWorldMapPoints()
        {
            var original = Fixture(); var zone = original.Zones[0];
            var moved = MapGraphGridPlacement.MoveZone(original, "z", new Rect(zone.Bounds.position + new Vector2(77, -77), zone.Bounds.size), 80);
            foreach (var node in original.Nodes) Assert.That(moved.Graph.GetNodePosition(node.NodeId), Is.EqualTo(original.Graph.GetNodePosition(node.NodeId) + new Vector2(80, -80)));
            Assert.That(moved.Zones[0].LayoutLocked, Is.False);
            var resized = MapGraphGridPlacement.MoveZone(original, "z", new Rect(-320, -240, 720, 480), 80, true);
            foreach (var node in original.Nodes) Assert.That(resized.Graph.GetNodePosition(node.NodeId), Is.EqualTo(original.Graph.GetNodePosition(node.NodeId)));
        }
        [Test] public void DraftUndoRecoveryAndPublishGateDoNotWriteTheSource()
        {
            var source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>(); var fixture = Fixture();
            source.ApplyCommandData("fixture", "草稿测试", "", fixture.Zones, fixture.Nodes, fixture.Edges, fixture.Constraints, new MapGraphNavigationBakeData());
            try
            {
                string baseline = EditorJsonUtility.ToJson(source);
                using var document = new MapGraphEditorDocument(source);
                var original = document.Layout.ContentFingerprint;
                document.EditPlacement(g => MapGraphGridPlacement.MoveNode(g, "a", new Vector2(-80, -80), 80), "网格移动");
                Assert.That(document.HasPlacementDraft, Is.True); Assert.That(document.IsDirty, Is.True);
                Assert.That(document.PendingEdit, Is.Null); Assert.That(document.PendingGeneration, Is.Null); Assert.That(document.LastVerifiedInput, Is.Null);
                Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(original));
                Assert.That(document.TryVerifyForSave(out _, out string failure), Is.False); StringAssert.Contains("PlacementNeedsValidation", failure);
                string fingerprint = document.AuthoringLayout.ContentFingerprint;
                Undo.PerformUndo(); Assert.That(document.HasPlacementDraft, Is.False); Assert.That(document.IsDirty, Is.False);
                Undo.PerformRedo(); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(fingerprint));
                using var restored = new MapGraphEditorDocument(source, document.ExportWorkingCopy(), document.SourceBaseline, document.ExportPlacement());
                Assert.That(restored.AuthoringLayout.ContentFingerprint, Is.EqualTo(fingerprint));
                Assert.That(restored.TryVerifyForSave(out _, out _), Is.False);
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(baseline));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test] public void RepeatedGridMovesHaveBoundedCostWithoutNavigationOrSearch()
        {
            var draft = Fixture(); var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) draft = MapGraphGridPlacement.MoveNode(draft, "a", new Vector2(-160, i % 2 == 0 ? 80 : -80), 80);
            watch.Stop(); CaseArtifactWriter.Trace("grid-cost", "1000 moves ms=" + watch.Elapsed.TotalMilliseconds);
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(5));
            Assert.That(draft.Nodes.Count, Is.EqualTo(3));
        }
        [Test] public void UndoReturningToAnEarlierLayoutKeepsDraftAndWorkingGraphIndependent()
        {
            var source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>(); var fixture = Fixture();
            source.ApplyCommandData("fixture", "Undo 隔离", "", fixture.Zones, fixture.Nodes, fixture.Edges, fixture.Constraints, new MapGraphNavigationBakeData());
            try
            {
                using var document = new MapGraphEditorDocument(source);
                var original = document.Layout;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    document.EditPlacement(g => MapGraphGridPlacement.MoveNode(g, "b", Vector2.zero, 80), "进入冲突摆放");
                    document.EditPlacement(_ => original, "恢复原摆放");
                    Undo.PerformUndo();
                    Assert.That(document.AuthoringLayout.Graph.GetNodePosition("b"), Is.EqualTo(Vector2.zero), "撤销应回到冲突摆放，轮次 " + attempt);
                    Assert.That(document.Layout.Graph.GetNodePosition("b"), Is.EqualTo(new Vector2(0, 80)), "未发布工作图不应跟随草稿 Undo。");
                    Undo.PerformRedo();
                    Assert.That(document.AuthoringLayout.Graph.GetNodePosition("b"), Is.EqualTo(new Vector2(0, 80)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
