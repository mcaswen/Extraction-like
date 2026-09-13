using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphGenerationControllerTests
    {
        private TestWorldBuilder _world;
        private MapGraphSceneSnapshot _snapshot;
        [SetUp] public void SetUp()
        {
            TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _world = new TestWorldBuilder(); TestNavMeshBuilder.Flat(_world);
            var profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 1, 0.5f);
            var nodes = new List<MapGraphSceneNode>();
            for (int i = 0; i < 3; i++)
            {
                string id = ((char)('A' + i)).ToString(); Vector3 point = new Vector3((i - 1) * 4, 0, 3);
                nodes.Add(new MapGraphSceneNode(id, "source-" + id, "zone", id, id, "", null, point,
                    new Rect(point.x - 1, point.z - 1, 2, 2), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(point, "member-" + id) }));
            }
            _snapshot = new MapGraphSceneSnapshot("fixture", "scene-guid", "scene-fingerprint", "nav-fingerprint",
                new[] { new MapGraphSceneZone("zone", "zone-source", "区域", null, new Rect(-8, -2, 16, 10)) }, nodes,
                new[] { new MapGraphSceneProfile(profile, MapGraphNavigationCostService.CaptureProfile(profile), new[] { "agent" }, new[] { Vector3.zero }) }, Array.Empty<string>());
            CaseArtifactWriter.Trace("setup", "Generation controller uses real fixture NavMesh and production solver.");
        }
        [TearDown] public void TearDown()
        {
            _world.Dispose(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavMesh.RemoveAllNavMeshData(); CaseArtifactWriter.Complete("COMPLETED");
        }

        [Test] public void BudgetedPipelinePublishesOnlyValidatedResults()
        {
            var settings = JsonUtility.FromJson<MapGraphGenerationSettings>("{\"_queriesPerEditorTick\":2}");
            var controller = new MapGraphGenerationController(() => _snapshot, settings, inputRevision: 42);
            var stages = new HashSet<MapGraphGenerationStage>();
            for (int i = 0; controller.IsRunning && i < 10000; i++)
            {
                stages.Add(controller.Stage); Assert.That(controller.Result, Is.Null);
                int workBefore = controller.WorkItems; long queriesBefore = controller.NavigationQueries;
                int work = controller.Advance(1, 1000);
                Assert.That(work, Is.EqualTo(1)); Assert.That(controller.WorkItems - workBefore, Is.EqualTo(work));
                Assert.That(controller.NavigationQueries - queriesBefore, Is.LessThanOrEqualTo(2));
            }
            AssertReady(controller);
            Assert.That(stages, Is.EquivalentTo(new[] { MapGraphGenerationStage.Collecting, MapGraphGenerationStage.ScanningNavigation,
                MapGraphGenerationStage.Generating, MapGraphGenerationStage.Validating }));
            Assert.That(controller.Result.RequestId, Is.EqualTo(controller.RequestId));
            Assert.That(controller.Result.InputRevision, Is.EqualTo(42));
            Assert.That(controller.Result.Anchors.Count, Is.EqualTo(3)); Assert.That(controller.Result.Connections.Count, Is.EqualTo(3));
            Assert.That(controller.TryGetCurrentResult(out var current), Is.True); Assert.That(current, Is.SameAs(controller.Result));
            Assert.That(controller.Advance(100), Is.Zero);
        }

        [Test] public void NavigationTickLimitAndSettingsAreFrozenAtStart()
        {
            var settings = JsonUtility.FromJson<MapGraphGenerationSettings>("{\"_queriesPerEditorTick\":2}");
            var controller = new MapGraphGenerationController(() => _snapshot, settings);
            JsonUtility.FromJsonOverwrite("{\"_queriesPerEditorTick\":100,\"_maximumSearchStates\":1}", settings);
            for (int i = 0; controller.IsRunning && i < 1000; i++)
            {
                int before = controller.NavigationWorkItems;
                controller.Advance(50, 1000);
                Assert.That(controller.NavigationWorkItems - before, Is.LessThanOrEqualTo(2));
            }
            AssertReady(controller);
            var timeSlice = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings());
            Assert.That(timeSlice.Advance(0), Is.Zero); Assert.That(timeSlice.Advance(1, double.NaN), Is.Zero);
            Assert.That(timeSlice.Advance(100, double.Epsilon), Is.EqualTo(1), "An atomic step is completed before checking the time slice.");
            Assert.That(timeSlice.MaximumStepMilliseconds, Is.GreaterThan(0)); timeSlice.Cancel();
        }

        [TestCase("ScanningNavigation")][TestCase("Generating")][TestCase("Ready")]
        public void CancellationStopsEveryOwnedStage(string stage)
        {
            var controller = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings());
            var target = (MapGraphGenerationStage)Enum.Parse(typeof(MapGraphGenerationStage), stage);
            for (int i = 0; controller.Stage != target && controller.IsRunning && i < 10000; i++) controller.Advance(1, 1000);
            Assert.That(controller.Stage, Is.EqualTo(target));
            if (target == MapGraphGenerationStage.Generating) controller.Advance(8, 1000);
            controller.Cancel(); int work = controller.WorkItems; long queries = controller.NavigationQueries;
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Cancelled)); Assert.That(controller.Result, Is.Null);
            Assert.That(controller.Advance(1000), Is.Zero); Assert.That(controller.WorkItems, Is.EqualTo(work));
            Assert.That(controller.NavigationQueries, Is.EqualTo(queries)); Assert.That(controller.TryGetCurrentResult(out _), Is.False);
            var replacement = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings());
            Assert.That(replacement.RequestId, Is.Not.EqualTo(controller.RequestId)); replacement.Cancel();
        }

        [TestCase("scene")][TestCase("navigation")][TestCase("unavailable")]
        public void InputChangesBeforePublicationRejectTheWholeResult(string change)
        {
            var controller = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings());
            for (int i = 0; controller.Stage != MapGraphGenerationStage.Validating && controller.IsRunning && i < 10000; i++) controller.Advance(1, 1000);
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Validating));
            ChangeSnapshot(change); controller.Advance(1, 1000);
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Stale)); Assert.That(controller.Result, Is.Null);
            Assert.That(controller.Diagnostics.Any(d => d.Code == "GenerationInputsChanged"), Is.True);
        }

        [Test] public void APreviouslyReadyPreviewMustBeRecheckedBeforeApplying()
        {
            var controller = Run(new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings()));
            var preview = controller.Result; ChangeSnapshot("navigation");
            Assert.That(controller.TryGetCurrentResult(out var usable), Is.False);
            Assert.That(usable, Is.Null); Assert.That(controller.Result, Is.Null);
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Stale));
            Assert.That(preview.Layout.Nodes.Count, Is.EqualTo(3), "Earlier immutable snapshots remain readable, but cannot be applied as current.");
        }

        [Test] public void LayoutOnlyKeepsConnectionsAndAuthoredStyles()
        {
            var initial = Run(new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings())).Result.Layout;
            var styled = initial.Edges[0].WithPresentation(initial.Edges[0].Axis, 2, 4, 2, true, Color.cyan, MapGraphEdgeOrigin.Manual);
            var previous = new MapGraphLayoutDraft(initial.Zones, initial.Nodes, initial.Edges.Select(e => e.EdgeId == styled.EdgeId ? styled : e), initial.Constraints);
            var result = Run(new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings(), previous, MapGraphGenerationMode.LayoutOnly)).Result;
            Assert.That(result.Mode, Is.EqualTo(MapGraphGenerationMode.LayoutOnly));
            Assert.That(result.Layout.Edges.Select(e => e.EdgeId), Is.EquivalentTo(previous.Edges.Select(e => e.EdgeId)));
            Assert.That(MapGraphValidation.Validate(result.Layout, previous, MapGraphIntentPreservation.AllIntent | MapGraphIntentPreservation.Topology).IsValid, Is.True);
            Assert.That(result.Layout.Graph.TryGetEdge(styled.EdgeId, out var actual), Is.True);
            Assert.That(actual.FromInset, Is.EqualTo(2)); Assert.That(actual.ToInset, Is.EqualTo(4)); Assert.That(actual.ColorOverride, Is.EqualTo(Color.cyan));
        }

        [Test] public void MissingOrChangedInputsCannotBeSilentlySynchronized()
        {
            var initial = Run(new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings())).Result.Layout;
            var missing = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings(), mode: MapGraphGenerationMode.LayoutOnly);
            missing.Advance(1); Assert.That(missing.Stage, Is.EqualTo(MapGraphGenerationStage.Failed));
            var previous = new MapGraphLayoutDraft(initial.Zones.Concat(new[] { new MapGraphZoneDefinition("orphan", "已删除区域", new Rect(1000, 0, 100, 100), new Vector2(40, 20), sourceObjectId: "removed") }),
                initial.Nodes, initial.Edges, initial.Constraints);
            var orphan = new MapGraphGenerationController(() => _snapshot, new MapGraphGenerationSettings(), previous);
            orphan.Advance(1); Assert.That(orphan.Diagnostics.Any(d => d.Code == "ZoneSynchronizationRequired"), Is.True);
            Assert.That(string.Join(";", orphan.Diagnostics), Does.Contain("已删除区域").And.Contain("在当前场景中已不存在"));
            Assert.That(orphan.Result, Is.Null); Assert.That(orphan.NavigationQueries, Is.Zero);
            var broken = new MapGraphGenerationController(() => throw new InvalidOperationException("scene closed"), new MapGraphGenerationSettings());
            broken.Advance(1); Assert.That(broken.Stage, Is.EqualTo(MapGraphGenerationStage.Failed)); Assert.That(broken.Result, Is.Null);
        }

        [UnityTest] public IEnumerator ActualSceneControllerProducesAValidatedReadOnlyResult()
        {
            _world.Dispose(); NavMesh.RemoveAllNavMeshData();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity"); yield return null;
            var before = MapGraphSceneCollector.Capture(scene);
            bool dirty = scene.isDirty; var controller = new MapGraphGenerationController(scene, new MapGraphGenerationSettings());
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (controller.IsRunning && deadline.Elapsed.TotalSeconds < 120)
            { Assert.That(controller.Advance(64, 6), Is.LessThanOrEqualTo(64)); yield return null; }
            AssertReady(controller); Assert.That(controller.Result.Layout.Nodes.Count, Is.EqualTo(28)); Assert.That(controller.Result.Layout.Zones.Count, Is.EqualTo(7));
            Assert.That(controller.TryGetCurrentResult(out var result), Is.True);
            Assert.That(MapGraphSceneCollector.Capture(scene).SceneFingerprint, Is.EqualTo(before.SceneFingerprint)); Assert.That(scene.isDirty, Is.EqualTo(dirty));
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath, "map-generation-controller.json"), JsonUtility.ToJson(new ControllerEvidence
            { request = result.RequestId, sceneFingerprint = result.Scene.SceneFingerprint, navigationFingerprint = result.Scene.NavigationFingerprint,
                nodes = result.Layout.Nodes.Count, zones = result.Layout.Zones.Count, edges = result.Layout.Edges.Count, states = controller.SearchStates,
                coordinates = controller.CoordinateIterations, queries = controller.NavigationQueries, work = controller.WorkItems,
                milliseconds = controller.ElapsedMilliseconds, maximumAdvance = controller.MaximumAdvanceMilliseconds,
                maximumStep = controller.MaximumStepMilliseconds, captureMilliseconds = controller.CaptureMilliseconds }, true));
            CaseArtifactWriter.Trace("scene-controller", "Validated result; max advance ms=" + controller.MaximumAdvanceMilliseconds);
        }

        [UnityTest] public IEnumerator SceneFingerprintIncludesLabelsProfilesAndAgentOrigins()
        {
            _world.Dispose(); NavMesh.RemoveAllNavMeshData();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity"); yield return null;
            var before = MapGraphSceneCollector.Capture(scene);
            var zone = before.Zones[0].Target;
            using (var serialized = new SerializedObject(zone))
            {
                var display = serialized.FindProperty("_displayName"); Assert.That(display, Is.Not.Null);
                display.stringValue += "测试"; serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var label = MapGraphSceneCollector.Capture(scene); Assert.That(label.SceneFingerprint, Is.Not.EqualTo(before.SceneFingerprint));
            var actor = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AgentPawnRoot>(true)).First();
            actor.transform.position += Vector3.right * 2;
            var moved = MapGraphSceneCollector.Capture(scene); Assert.That(moved.SceneFingerprint, Is.Not.EqualTo(label.SceneFingerprint));
            actor.GetComponent<NavMeshAgent>().radius += 0.3f;
            var profile = MapGraphSceneCollector.Capture(scene); Assert.That(profile.SceneFingerprint, Is.Not.EqualTo(moved.SceneFingerprint));
            Assert.That(profile.NavigationFingerprint, Is.EqualTo(before.NavigationFingerprint), "Configuration edits do not rewrite the baked NavMesh asset.");
        }

        private void ChangeSnapshot(string change)
        {
            if (change == "unavailable") { _snapshot = null; return; }
            _snapshot = new MapGraphSceneSnapshot(_snapshot.ScenePath, _snapshot.SceneGuid, change == "scene" ? "changed" : _snapshot.SceneFingerprint,
                change == "navigation" ? "changed" : _snapshot.NavigationFingerprint, _snapshot.Zones, _snapshot.Nodes, _snapshot.Profiles, _snapshot.Diagnostics);
        }
        private static MapGraphGenerationController Run(MapGraphGenerationController controller)
        { for (int i = 0; controller.IsRunning && i < 1000; i++) controller.Advance(32, 1000); AssertReady(controller); return controller; }
        private static void AssertReady(MapGraphGenerationController controller)
        {
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Ready), string.Join("\n", controller.Diagnostics));
            Assert.That(controller.Result, Is.Not.Null); Assert.That(MapGraphValidation.Validate(controller.Result.Layout).IsValid, Is.True);
            foreach (var profile in controller.Result.Scene.Profiles)
                Assert.That(MapGraphNavigationValidation.Validate(controller.Result.Layout,
                    controller.Result.Connections.Where(c => c.ProfileId == profile.Data.ProfileId).Select(c => c.Edge).ToArray()).IsValid, Is.True);
        }
        [Serializable] private sealed class ControllerEvidence
        { public string request, sceneFingerprint, navigationFingerprint; public int nodes, zones, edges, states, coordinates, work;
            public long queries; public double milliseconds, maximumAdvance, maximumStep, captureMilliseconds; }
    }
}
