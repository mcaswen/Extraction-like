using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.TestTools;
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
        [Test] public void ReefConnectionReproducesHiddenOldAxisAfterVerticalPlacement()
        {
            var source = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>("Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset");
            string saved = EditorJsonUtility.ToJson(source);
            var original = MapGraphLayoutDraft.FromDefinition(source);
            string from = original.Nodes.Single(n => n.Description == "Zone-龙骨礁/EnemySourceCluster_A").NodeId;
            string to = original.Nodes.Single(n => n.Description == "Zone-龙骨礁/ExtractionCluster").NodeId;
            Assert.That(original.Graph.TryGetEdgeBetween(from, to, out var edge), Is.True);
            var assetAxis = edge.Axis;
            // 保存的作者图以后可以修正；复现明确构造截图中的旧水平方向，不要求源资产永久保留旧错误。
            original = new MapGraphLayoutDraft(original.Zones, original.Nodes, original.Edges.Select(e => e.EdgeId == edge.EdgeId
                ? e.WithPresentation(MapGraphAxis.Horizontal, 0, 0, e.WidthOverride, e.UseColorOverride, e.ColorOverride, e.Origin) : e), original.Constraints, original.StartNodeId);
            original.Graph.TryGetNode(from, out var node); original.Graph.TryGetZone(node.ZoneId, out var zone);
            var moved = MapGraphGridPlacement.WithPositions(original, new Dictionary<string, Vector2>
                { [from] = zone.Bounds.center + new Vector2(80, -80), [to] = zone.Bounds.center + new Vector2(80, 80) });
            Assert.That(moved.Graph.TryGetEdgeBetween(from, to, out var retained), Is.True);
            Assert.That(MapGraphGeometry.TryGetVisibleSegment(moved, retained, out _, out _), Is.False);
            StringAssert.Contains("ConnectionAlreadyExists", Assert.Throws<ArgumentException>(
                () => MapGraphEditOperations.AddEdge(moved, from, to, MapGraphAxis.Vertical)).Message);
            CaseArtifactWriter.Trace("hidden-edge-reproduction", retained.EdgeId + "; assetAxis=" + assetAxis + "; storedAxis=" + retained.Axis +
                "; from=" + moved.Graph.GetNodePosition(from).ToString("R") + "; to=" + moved.Graph.GetNodePosition(to).ToString("R") + "; strictSegment=false; duplicate=true");
            var presentation = MapGraphEditorEdgePresentation.Resolve(moved, retained);
            Assert.That(presentation.HasSegment && presentation.RequiresAttention && presentation.CanRepairDirection, Is.True);
            Assert.That(presentation.From.x, Is.EqualTo(presentation.To.x));
            var repaired = MapGraphEditOperations.AlignEdgeToNodes(moved, retained.EdgeId);
            repaired.Graph.TryGetEdge(retained.EdgeId, out var aligned);
            Assert.That(aligned.Axis, Is.EqualTo(MapGraphAxis.Vertical));
            Assert.That(MapGraphGeometry.TryGetVisibleSegment(repaired, aligned, out _, out _), Is.True);
            Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(saved));
        }
        [Test] public void WarningSegmentsAreSelectableAndDiagonalOrOverlappingEdgesStayUndrawn()
        {
            var original = Fixture();
            var axisMismatch = MapGraphGridPlacement.MoveNode(original, "b", new Vector2(-100, -80), 20);
            var largeInsets = MapGraphEditOperations.StyleEdge(original, "ab", 120, 120, 4, false, Color.white);
            var transparent = MapGraphEditOperations.StyleEdge(original, "ab", 0, 0, 3, true, Color.clear);
            foreach (var draft in new[] { axisMismatch, largeInsets, transparent })
            {
                string fingerprint = draft.ContentFingerprint;
                var presentation = MapGraphEditorEdgePresentation.Resolve(draft, draft.Edges[0]);
                Assert.That(presentation.HasSegment && presentation.RequiresAttention, Is.True);
                Assert.That(MapGraphGeometry.TryGetAxis(presentation.From, presentation.To, out _), Is.True);
                var canvas = new MapGraphEditorCanvas(); var rect = new Rect(0, 0, 900, 600); canvas.Fit(draft, rect);
                Assert.That(canvas.Hit(draft, canvas.ToScreen((presentation.From + presentation.To) * .5f, rect), rect).id, Is.EqualTo("ab"));
                Assert.That(draft.ContentFingerprint, Is.EqualTo(fingerprint));
            }
            foreach (var point in new[] { new Vector2(-40, -80), new Vector2(-100, 80), new Vector2(-100, 60) })
            {
                var draft = MapGraphGridPlacement.MoveNode(original, "b", point, 20);
                var presentation = MapGraphEditorEdgePresentation.Resolve(draft, draft.Edges[0]);
                Assert.That(presentation.RequiresAttention, Is.True); Assert.That(presentation.HasSegment, Is.False);
                Assert.That(presentation.CanRepairDirection, Is.False); Assert.That(draft.Edges.Count, Is.EqualTo(1));
            }
            CaseArtifactWriter.Trace("hidden-edge-variants", "Axis mismatch, oversized insets, transparent style: selectable orthogonal warnings; diagonal and overlap: explicit warning, no invented line.");
        }
        [Test] public void DirectionRepairIsUndoableAndPreservesAllOtherAuthorData()
        {
            var original = MapGraphGridPlacement.WithPositions(MapGraphEditOperations.StyleEdge(Fixture(), "ab", 4, 7, 3, true, Color.cyan), null);
            var source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            source.ApplyCommandData("fixture", "方向修正", "", original.Zones, original.Nodes, original.Edges, original.Constraints, new MapGraphNavigationBakeData());
            string saved = EditorJsonUtility.ToJson(source);
            try
            {
                using var document = new MapGraphEditorDocument(source);
                document.EditPlacement(g => MapGraphGridPlacement.MoveNode(g, "b", new Vector2(-100, -80), 20), "改为垂直摆放");
                var before = document.AuthoringLayout;
                document.EditPlacement(g => MapGraphEditOperations.AlignEdgeToNodes(g, "ab"), "修正方向");
                var after = document.AuthoringLayout;
                Assert.That(after.Edges.Count, Is.EqualTo(before.Edges.Count));
                Assert.That(after.Edges[0].Axis, Is.EqualTo(MapGraphAxis.Vertical));
                var repaired = after.Edges[0];
                Assert.That(JsonUtility.ToJson(repaired.WithPresentation(MapGraphAxis.Horizontal, repaired.FromInset, repaired.ToInset,
                    repaired.WidthOverride, repaired.UseColorOverride, repaired.ColorOverride, repaired.Origin)),
                    Is.EqualTo(JsonUtility.ToJson(before.Edges[0])), "除 Axis 外全部连接数据保持。");
                foreach (var node in before.Nodes) Assert.That(after.Graph.GetNodePosition(node.NodeId), Is.EqualTo(before.Graph.GetNodePosition(node.NodeId)));
                Assert.That(document.TryVerifyForSave(out _, out _), Is.False);
                Undo.PerformUndo(); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(before.ContentFingerprint));
                Undo.PerformRedo(); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(after.ContentFingerprint));
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(saved));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
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
        [UnityTest] public IEnumerator GridCanvasShiftPreviewAndPublishRoundTripRenderRealWindow()
        {
            Assert.That(TestRunContext.Load().graphics, Is.True);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            const string scenePath = "Assets/MapGridCanvas_Reproduction.unity", assetPath = "Assets/MapGridCanvas_Reproduction.asset", navPath = "Assets/MapGridCanvas_Nav_Reproduction.asset";
            Assert.That(File.Exists(scenePath) || File.Exists(assetPath) || File.Exists(navPath), Is.False);
            MapGraphEditorWindow window = null;
            using var world = new TestWorldBuilder();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var ground = world.Cube("Grid fixture ground", new Vector3(0, -0.1f, 0), new Vector3(60, 0.2f, 40));
                var surface = ground.AddComponent<NavMeshSurface>();
                var zone = world.Root("网格编辑验收区").AddComponent<TargetZoneAuthoring>();
                RuntimeFixtureAccess.Configure(zone, "_displayName", "网格编辑验收区");
                for (int i = 0; i < 3; i++)
                {
                    var cluster = TargetFactory.Extraction(world, new Vector3((i - 1) * 10, 0, 4));
                    cluster.transform.SetParent(zone.transform);
                    RuntimeFixtureAccess.Configure(cluster, "_zone", zone);
                    RuntimeFixtureAccess.Configure(cluster, "_displayName", new[] { "西侧群", "中央群", "东侧群" }[i]);
                    zone.RegisterCluster(cluster);
                }
                var actor = world.Root("Map navigation profile"); actor.AddComponent<NavMeshAgent>(); actor.AddComponent<AgentPawnRoot>();
                surface.BuildNavMesh(); Assert.That(surface.navMeshData, Is.Not.Null);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath); AssetDatabase.SaveAssetIfDirty(surface.navMeshData);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                yield return null;
                var sceneInput = MapGraphSceneCollector.Capture(scene); Assert.That(sceneInput.IsValid, Is.True, string.Join(";", sceneInput.Diagnostics));
                var poses = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToDictionary(t => t, t => t.position);
                window = ScriptableObject.CreateInstance<MapGraphEditorWindow>(); window.position = new Rect(80, 80, 1280, 820); window.ShowUtility(); window.Focus();
                window.BeginGeneration(); yield return Ready(window); Assert.That(window.ApplyPreview(), Is.True, window.Status);
                var document = window.Document;
                var ordered = document.Layout.Nodes.OrderBy(n => sceneInput.Nodes.Single(t => t.Id == n.NodeId).WorldCenter.x).ToArray();
                string a = ordered[0].NodeId, b = ordered[1].NodeId, c = ordered[2].NodeId;
                document.EditPlacement(g => MapGraphGridPlacement.MoveZone(g, g.Zones[0].ZoneId, new Rect(-320, -240, 640, 480), 80, true), "区域摆放");
                document.EditPlacement(g => MapGraphGridPlacement.WithPositions(g, new Dictionary<string, Vector2> { [a] = new Vector2(-160, 80), [b] = new Vector2(0, 80), [c] = new Vector2(160, 80) }), "群网格摆放");
                window.BeginGeneration(MapGraphGenerationMode.PlacementConnections); yield return Ready(window); Assert.That(window.ApplyPreview(), Is.True, window.Status);
                Assert.That(window.SaveTo(assetPath), Is.True, window.Status);
                string sourceBefore = EditorJsonUtility.ToJson(document.SourceDefinition);
                window.Canvas.Fit(document.AuthoringLayout, window.Canvas.ViewRect); window.Repaint(); yield return null;
                CaptureGrid(window, "01-grid-ready");
                var validPlacement = document.AuthoringLayout;
                var oldDirection = validPlacement.Edges.First();
                string other = validPlacement.Nodes.Single(n => n.NodeId != oldDirection.FromNodeId && n.NodeId != oldDirection.ToNodeId).NodeId;
                document.EditPlacement(g => MapGraphEditOperations.StyleEdge(MapGraphGridPlacement.WithPositions(g, new Dictionary<string, Vector2>
                    { [oldDirection.FromNodeId] = new Vector2(160,-80), [oldDirection.ToNodeId] = new Vector2(160,80), [other] = new Vector2(-160,80) }),
                    oldDirection.EdgeId, 4, 7, 3, true, new Color32(110,211,189,255)), "构造旧水平线的垂直摆放");
                long beforeExistingSelection = document.Revision; int retainedEdges = document.AuthoringLayout.Edges.Count;
                var evidenceBeforeSelection = document.LastVerifiedInput;
                window.Canvas.Fit(document.AuthoringLayout, window.Canvas.ViewRect); window.Repaint(); yield return null;
                ClickGrid(window, oldDirection.FromNodeId, false); ClickGrid(window, oldDirection.ToNodeId, true);
                Assert.That(window.Canvas.SelectionKind, Is.EqualTo(MapGraphSelectionKind.Edge)); Assert.That(window.Canvas.SelectionId, Is.EqualTo(oldDirection.EdgeId));
                Assert.That(window.Status, Does.Contain("已选中原连接").And.Contain("修正方向").And.Not.Contain("ConnectionAlreadyExists"));
                window.RequestConnection();
                Assert.That(document.Revision, Is.EqualTo(beforeExistingSelection)); Assert.That(document.AuthoringLayout.Edges.Count, Is.EqualTo(retainedEdges));
                Assert.That(document.PendingGeneration, Is.Null); Assert.That(document.LastVerifiedInput, Is.SameAs(evidenceBeforeSelection));
                CaptureGrid(window, "10-existing-hidden-connection");
                window.RequestEdit(g => MapGraphEditOperations.AlignEdgeToNodes(g, oldDirection.EdgeId), "按当前摆放修正连接方向", true);
                document.AuthoringLayout.Graph.TryGetEdge(oldDirection.EdgeId, out var repairedDirection);
                Assert.That(repairedDirection.Axis, Is.EqualTo(MapGraphAxis.Vertical));
                Assert.That(MapGraphGeometry.TryGetVisibleSegment(document.AuthoringLayout, repairedDirection, out _, out _), Is.True);
                Undo.PerformUndo(); Assert.That(MapGraphEditorEdgePresentation.Resolve(document.AuthoringLayout, document.AuthoringLayout.Edges.Single(e => e.EdgeId == oldDirection.EdgeId)).RequiresAttention, Is.True);
                Undo.PerformRedo(); Assert.That(EditorJsonUtility.ToJson(document.SourceDefinition), Is.EqualTo(sourceBefore));
                CaptureGrid(window, "11-repaired-existing-connection");
                document.EditPlacement(_ => validPlacement, "恢复后继续生成发布回归");
                document.EditPlacement(g =>
                {
                    var conflict = MapGraphGridPlacement.WithPositions(g, new Dictionary<string, Vector2>
                    { [a] = new Vector2(-160, 0), [b] = Vector2.zero, [c] = new Vector2(160, 0) });
                    foreach (var node in conflict.Nodes.ToArray()) conflict = MapGraphEditOperations.LockNode(conflict, node.NodeId, true);
                    return new MapGraphLayoutDraft(conflict.Zones, conflict.Nodes, new[]
                    {
                        new MapGraphEdgeDefinition("diagnostic_cross", a, c, 10, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual),
                        new MapGraphEdgeDefinition("diagnostic_axis", b, c, 10, MapGraphAxis.Vertical, MapGraphEdgeOrigin.Manual)
                    }, conflict.Constraints);
                }, "构造名称、图标和方向冲突");
                string conflictFingerprint = document.AuthoringLayout.ContentFingerprint;
                foreach (var mode in new[] { MapGraphGenerationMode.ValidateOnly, MapGraphGenerationMode.PlacementConnections, MapGraphGenerationMode.PlacementAdjustment })
                {
                    window.BeginGeneration(mode); var failed = document.PendingGeneration;
                    var deadline = System.Diagnostics.Stopwatch.StartNew();
                    while (failed.IsRunning && deadline.Elapsed.TotalSeconds < 30) { failed.Advance(32, 6); yield return null; }
                    yield return null;
                    Assert.That(failed.Stage, Is.EqualTo(MapGraphGenerationStage.Failed));
                    Assert.That(window.DiagnosticCount, Is.EqualTo(failed.Diagnostics.Count));
                    Assert.That(window.Status, Does.Contain("当前摆放保留").And.Not.Contain("cluster_"));
                    Assert.That(failed.Diagnostics.Any(i => i.Code == "EdgeThroughName"), Is.False);
                    foreach (string code in new[] { "NodeOverName", "EdgeThroughNode", "NonOrthogonalEdge" })
                        Assert.That(failed.Diagnostics.Any(i => i.Code == code), Is.True, mode + ":" + code);
                    Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(conflictFingerprint));
                    Assert.That(document.TryVerifyForSave(out _, out _), Is.False);
                }
                int focusIndex = document.PendingGeneration.Diagnostics.ToList().FindIndex(i => i.Code == "NodeOverName");
                Assert.That(window.FocusDiagnostic(focusIndex), Is.True); yield return null;
                CaptureGrid(window, "07-readable-conflicts");
                CaseArtifactWriter.Trace("conflict-before-undo", document.ExportPlacement());
                document.EditPlacement(_ => validPlacement, "恢复合法摆放");
                Assert.That(window.DiagnosticCount, Is.Zero); Assert.That(window.Canvas.FocusedIssue, Is.Null);
                Assert.That(window.Status, Does.Not.Contain("未通过"));
                Undo.PerformUndo(); CaseArtifactWriter.Trace("conflict-after-undo", document.ExportPlacement());
                Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(conflictFingerprint));
                Assert.That(window.DiagnosticCount, Is.Zero); Assert.That(window.Canvas.FocusedIssue, Is.Null);
                Undo.PerformRedo(); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(validPlacement.ContentFingerprint));
                window.Canvas.Fit(document.AuthoringLayout, window.Canvas.ViewRect); window.Repaint(); yield return null;
                var from = window.Canvas.ToScreen(document.AuthoringLayout.Graph.GetNodePosition(b), window.Canvas.ViewRect);
                var to = window.Canvas.ToScreen(new Vector2(0, 160), window.Canvas.ViewRect);
                long revision = document.Revision;
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = from });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = to, delta = to - from });
                Assert.That(document.Revision, Is.EqualTo(revision), "拖动期间不提交求解或 Undo。");
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = to });
                Assert.That(document.Revision, Is.EqualTo(revision + 1));
                Assert.That(document.AuthoringLayout.Graph.GetNodePosition(b), Is.EqualTo(new Vector2(0, 160)));
                Assert.That(document.AuthoringLayout.Graph.GetNodePosition(a), Is.EqualTo(new Vector2(-160, 80)));
                Assert.That(document.PendingGeneration, Is.Null); Assert.That(document.PendingEdit, Is.Null);
                ClickGrid(window, a, false); ClickGrid(window, b, true);
                Assert.That(window.Canvas.ConnectionSelection.HasPair, Is.True); StringAssert.Contains("同行", window.Status);
                CaptureGrid(window, "02-shift-alignment-feedback");
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                Assert.That(window.Canvas.ConnectionSelection.HasPair, Is.False);
                string storedDraft = document.AuthoringLayout.ContentFingerprint;
                window.KeepDraftAndClose(); window = null;
                window = ScriptableObject.CreateInstance<MapGraphEditorWindow>(); window.position = new Rect(80, 80, 1280, 820); window.ShowUtility(); window.Focus();
                document = window.Document;
                Assert.That(document.HasPlacementDraft, Is.True); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(storedDraft));
                Assert.That(document.PendingGeneration?.IsRunning, Is.True);
                Assert.That(window.CanEditPlacement, Is.True, "打开窗口的导航补证据不能阻塞网格摆放。");
                window.Cancel();
                window.BeginGeneration(MapGraphGenerationMode.PlacementAdjustment); yield return Ready(window);
                Assert.That(document.HasPlacementDraft, Is.True); CaptureGrid(window, "03-adjustment-preview");
                Assert.That(window.ApplyPreview(), Is.True, window.Status);
                var originalEdge = document.Layout.Edges.First();
                document.EditPlacement(g => MapGraphEditOperations.DeleteEdge(g, originalEdge.EdgeId), "删除连接");
                int beforeEdges = document.AuthoringLayout.Edges.Count;
                ClickGrid(window, originalEdge.ToNodeId, false); ClickGrid(window, originalEdge.FromNodeId, true);
                Assert.That(document.AuthoringLayout.Edges.Count, Is.EqualTo(beforeEdges + 1), window.Status);
                Assert.That(window.Canvas.SelectionKind, Is.EqualTo(MapGraphSelectionKind.Edge)); Assert.That(window.Canvas.ConnectionSelection.HasPair, Is.False);
                string manualId = window.Canvas.SelectionId;
                document.EditPlacement(g => MapGraphEditOperations.StyleEdge(g, manualId, 3, 6, 3, true, new Color32(110, 211, 189, 255)), "线样式");
                CaptureGrid(window, "04-shift-line-style");
                Assert.That(EditorJsonUtility.ToJson(document.SourceDefinition), Is.EqualTo(sourceBefore));
                window.BeginGeneration(MapGraphGenerationMode.ValidateOnly); yield return Ready(window); Assert.That(window.ApplyPreview(), Is.True, window.Status);
                Assert.That(window.SaveTo(), Is.True, window.Status); string final = document.Layout.ContentFingerprint;
                foreach (var pose in poses) Assert.That(pose.Key.position, Is.EqualTo(pose.Value), "编辑不移动世界对象。");
                window.DiscardChanges(); window.Close(); window = null;
                scene = EditorSceneManager.OpenScene(scenePath); yield return null;
                var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
                Assert.That(binding.IsValid, Is.True); Assert.That(MapGraphLayoutDraft.FromDefinition(binding.MapDefinition).ContentFingerprint, Is.EqualTo(final));
                window = ScriptableObject.CreateInstance<MapGraphEditorWindow>(); window.position = new Rect(80, 80, 1280, 820); window.ShowUtility(); window.Focus();
                for (int i = 0; window.Document.PendingGeneration?.IsRunning == true && i < 500; i++) yield return null;
                Assert.That(window.Document.HasPlacementDraft, Is.False); Assert.That(window.Document.Layout.ContentFingerprint, Is.EqualTo(final));
                CaptureGrid(window, "05-published-reopened");
                var beforeSceneEdit = MapGraphSceneCollector.Capture(scene);
                var surviving = beforeSceneEdit.Nodes.Single(n => n.Id == a);
                var transferred = beforeSceneEdit.Nodes.Single(n => n.Id == b);
                var deleted = beforeSceneEdit.Nodes.Single(n => n.Id == c);
                string publishedBeforeSync = EditorJsonUtility.ToJson(window.Document.SourceDefinition);
                var newZone = new GameObject("迁移验收区").AddComponent<TargetZoneAuthoring>();
                transferred.Target.Zone.UnregisterCluster(transferred.Target);
                transferred.Target.transform.SetParent(newZone.transform, true);
                RuntimeFixtureAccess.Configure(transferred.Target, "_zone", newZone); newZone.RegisterCluster(transferred.Target);
                UnityEngine.Object.DestroyImmediate(deleted.Target.gameObject);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                var synchronizationDeadline = System.Diagnostics.Stopwatch.StartNew();
                while (window.Document.AuthoringLayout.Nodes.Count == 3 && synchronizationDeadline.Elapsed.TotalSeconds < 5) yield return null;
                document = window.Document;
                Assert.That(document.AuthoringLayout.Nodes.Count, Is.EqualTo(2));
                Assert.That(window.LastSceneSynchronization.Changes.Any(i => i.Code == "SceneNodeRemoved"), Is.True);
                Assert.That(window.LastSceneSynchronization.Changes.Any(i => i.Code == "SceneNodeReassigned"), Is.True);
                Assert.That(EditorJsonUtility.ToJson(document.SourceDefinition), Is.EqualTo(publishedBeforeSync));
                yield return null; CaptureGrid(window, "08-scene-synchronized");
                string newZoneId = document.AuthoringLayout.Nodes.Single(n => n.NodeId == b).ZoneId;
                Assert.That(newZoneId, Is.Not.EqualTo(surviving.ZoneId));
                document.EditPlacement(g => MapGraphGridPlacement.MoveZone(g, newZoneId, new Rect(480,-240,640,480), 80, true), "摆放新区域");
                document.EditPlacement(g => MapGraphGridPlacement.WithPositions(g, new Dictionary<string, Vector2>
                    { [a] = new Vector2(-160,0), [b] = new Vector2(640,0) }), "对齐同步后的两群，连接穿过名称框");
                foreach (var edge in document.AuthoringLayout.Edges.ToArray())
                    document.EditPlacement(g => MapGraphEditOperations.DeleteEdge(g, edge.EdgeId), "准备重新手动连接");
                window.Canvas.Fit(document.AuthoringLayout, window.Canvas.ViewRect); window.Repaint(); yield return null;
                ClickGrid(window, a, false); ClickGrid(window, b, true);
                // 场景同步后的首次连线会异步补齐导航证据；等待同一用户意图完成再验收。
                var connectionDeadline = System.Diagnostics.Stopwatch.StartNew();
                while (!document.AuthoringLayout.Graph.TryGetEdgeBetween(a, b, out _) && connectionDeadline.Elapsed.TotalSeconds < 10)
                    yield return null;
                Assert.That(document.AuthoringLayout.Graph.TryGetEdgeBetween(a, b, out var nameCrossing), Is.True, window.Status);
                Assert.That(nameCrossing.Origin, Is.EqualTo(MapGraphEdgeOrigin.Manual));
                Assert.That(MapGraphGeometry.TryGetVisibleSegment(document.AuthoringLayout, nameCrossing, out var crossFrom, out var crossTo), Is.True);
                Assert.That(document.AuthoringLayout.Zones.Any(z => MapGraphGeometry.SegmentIntersectsRect(crossFrom, crossTo, z.NameSafeBounds, 2)), Is.True);
                Assert.That(MapGraphValidation.Validate(document.AuthoringLayout).IsValid, Is.True);
                CaptureGrid(window, "12-connection-through-zone-name");
                RuntimeFixtureAccess.Configure(transferred.Target, "_displayName", "迁移后的群");
                window.BeginGeneration(MapGraphGenerationMode.PlacementConnections); yield return Ready(window);
                Assert.That(document.AuthoringLayout.Nodes.Single(n => n.NodeId == b).DisplayName, Is.EqualTo("迁移后的群"), "生成前同步未触发层级事件的字段修改。");
                Assert.That(window.ApplyPreview(), Is.True, window.Status); Assert.That(window.SaveTo(), Is.True, window.Status);
                window.DiscardChanges(); window.Close(); window = null;
                scene = EditorSceneManager.OpenScene(scenePath); yield return null;
                binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
                Assert.That(binding.IsValid, Is.True); Assert.That(binding.TargetBindings.Count, Is.EqualTo(2));
                Assert.That(binding.MapDefinition.Nodes.Single(n => n.NodeId == b).ZoneId, Is.EqualTo(newZoneId));
                var publishedCrossing = MapGraphLayoutDraft.FromDefinition(binding.MapDefinition);
                Assert.That(publishedCrossing.Graph.TryGetEdgeBetween(a, b, out var reloadedCrossing), Is.True);
                Assert.That(reloadedCrossing.EdgeId, Is.EqualTo(nameCrossing.EdgeId));
                Assert.That(MapGraphGeometry.TryGetVisibleSegment(publishedCrossing, reloadedCrossing, out var publishedFrom, out var publishedTo), Is.True);
                Assert.That(publishedCrossing.Zones.Any(z => MapGraphGeometry.SegmentIntersectsRect(publishedFrom, publishedTo, z.NameSafeBounds, 2)), Is.True);
                Assert.That(MapGraphValidation.Validate(publishedCrossing).IsValid, Is.True);
                window = ScriptableObject.CreateInstance<MapGraphEditorWindow>(); window.position = new Rect(80,80,1280,820); window.ShowUtility(); window.Focus();
                window.Cancel(); window.Canvas.Fit(window.Document.AuthoringLayout, window.Canvas.ViewRect); yield return null;
                CaptureGrid(window, "09-synchronized-published");
                window.DiscardChanges(); window.Close(); window = null;
                // 正式图只读外观检查，保存/重载合同已在上方独立场景验证。
                EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity"); yield return null;
                window = ScriptableObject.CreateInstance<MapGraphEditorWindow>(); window.position = new Rect(80, 80, 1440, 900); window.ShowUtility(); window.Focus();
                Assert.That(window.Document.AuthoringLayout, Is.Not.Null); window.Cancel(); window.Repaint(); yield return null;
                CaptureGrid(window, "06-project-map-grid-editor");
                CaseArtifactWriter.Trace("grid-delivery", "Actual IMGUI drag/Shift, independent scene save/reload, formal map read-only screenshot.");
            }
            finally
            {
                if (window != null) { window.DiscardChanges(); window.Close(); }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                NavMesh.RemoveAllNavMeshData(); AssetDatabase.DeleteAsset(scenePath); AssetDatabase.DeleteAsset(assetPath); AssetDatabase.DeleteAsset(navPath);
            }
        }
        private static IEnumerator Ready(MapGraphEditorWindow window)
        {
            var request = window.Document.PendingGeneration; Assert.That(request, Is.Not.Null, window.Status);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (request.IsRunning && watch.Elapsed.TotalSeconds < 30) { request.Advance(32, 6); yield return null; }
            Assert.That(request.Stage, Is.EqualTo(MapGraphGenerationStage.Ready), string.Join(";", request.Diagnostics));
        }
        private static void ClickGrid(MapGraphEditorWindow window, string id, bool shift)
        {
            var point = window.Canvas.ToScreen(window.Document.AuthoringLayout.Graph.GetNodePosition(id), window.Canvas.ViewRect);
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point, modifiers = shift ? EventModifiers.Shift : EventModifiers.None });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point, modifiers = shift ? EventModifiers.Shift : EventModifiers.None });
        }
        private static void CaptureGrid(MapGraphEditorWindow window, string name)
        {
            string path = Path.Combine(TestRunContext.Load().outputPath, "visual", name + ".png");
            UnityEditorViewCapture.Capture(window, path, 0.3f);
            CaseArtifactWriter.Trace("grid-screenshot", path);
        }
    }
}
