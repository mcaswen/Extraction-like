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
                    foreach (string code in new[] { "NodeOverName", "EdgeThroughName", "EdgeThroughNode", "NonOrthogonalEdge" })
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
