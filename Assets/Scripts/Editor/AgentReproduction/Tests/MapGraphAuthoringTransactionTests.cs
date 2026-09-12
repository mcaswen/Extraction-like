using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphAuthoringTransactionTests
    {
        private const string TestScene = "Assets/MapGraph_Save_Reproduction.unity";
        private const string TestAsset = "Assets/MapGraph_Save_Reproduction.asset";
        private MapGraphEditorDocument _document;
        private Scene _scene;
        [SetUp] public void SetUp()
        {
            TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(File.Exists(TestScene) || File.Exists(TestAsset), Is.False);
            CaseArtifactWriter.Trace("setup", "Saved scene copy, real NavMesh, only isolated fixture assets are written.");
        }
        [TearDown] public void TearDown()
        {
            _document?.Dispose(); _document = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TestScene); AssetDatabase.DeleteAsset(TestAsset);
            CaseArtifactWriter.Complete("COMPLETED");
        }
        private IEnumerator GenerateSceneCopy()
        {
            _scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity");
            // 正式场景已安装图；独立新建保存用例只移除当前隔离副本的专用绑定根。
            foreach (var binding in _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).ToArray())
            {
                Assert.That(binding.gameObject.name, Is.EqualTo("MapGraphBinding"));
                Assert.That(binding.GetComponents<Component>().Length, Is.EqualTo(2));
                UnityEngine.Object.DestroyImmediate(binding.gameObject);
            }
            Assert.That(EditorSceneManager.SaveScene(_scene, TestScene), Is.True); yield return null;
            _document = new MapGraphEditorDocument(); var request = _document.BeginGeneration(_scene);
            var time = System.Diagnostics.Stopwatch.StartNew();
            while (request.IsRunning && time.Elapsed.TotalSeconds < 120) { request.Advance(64, 6); yield return null; }
            Assert.That(request.Stage, Is.EqualTo(MapGraphGenerationStage.Ready), string.Join("\n", request.Diagnostics));
            Assert.That(_document.TryApplyGeneration(request, out var failure), Is.True, failure);
        }

        [UnityTest] public IEnumerator SavedMapReopensWithStableIdentityAndZeroBakeQueries()
        {
            yield return GenerateSceneCopy();
            string layout = JsonUtility.ToJson(new LayoutEvidence { nodes = _document.Layout.Nodes.ToArray(), zones = _document.Layout.Zones.ToArray(), edges = _document.Layout.Edges.ToArray() });
            Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, TestAsset, out var binding, out var failure), Is.True, failure);
            Assert.That(binding.IsValid, Is.True, string.Join("\n", binding.ValidationErrors));
            Assert.That(binding.TargetBindings.Count, Is.EqualTo(28)); Assert.That(binding.ZoneBindings.Count, Is.EqualTo(7));
            Assert.That(_document.IsDirty || _document.HasSourceConflict, Is.False);
            string guid = AssetDatabase.AssetPathToGUID(TestAsset); long revision = binding.MapDefinition.Revision;
            _document.SetDisplayName("保存后的地图名称");
            Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, null, out binding, out failure), Is.True, failure);
            Assert.That(binding.MapDefinition.Revision, Is.GreaterThan(revision));
            _document.Dispose(); _document = null;
            _scene = EditorSceneManager.OpenScene(TestScene); yield return null;
            binding = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
            Assert.That(binding.IsValid, Is.True, string.Join("\n", binding.ValidationErrors));
            Assert.That(AssetDatabase.AssetPathToGUID(TestAsset), Is.EqualTo(guid));
            Assert.That(binding.MapDefinition.DisplayName, Is.EqualTo("保存后的地图名称"));
            var draft = MapGraphLayoutDraft.FromDefinition(binding.MapDefinition);
            Assert.That(JsonUtility.ToJson(new LayoutEvidence { nodes = draft.Nodes.ToArray(), zones = draft.Zones.ToArray(), edges = draft.Edges.ToArray() }), Is.EqualTo(layout));
            var current = MapGraphSceneCollector.Capture(_scene);
            var costs = new MapGraphNavigationCostService(binding.MapDefinition, binding, current.Profiles[0].QueryProfile, current.SceneFingerprint, current.NavigationFingerprint);
            Assert.That(costs.PendingEdgeCount, Is.Zero); Assert.That(costs.CalculationCount, Is.Zero);
            _document = new MapGraphEditorDocument(binding.MapDefinition);
            var validate = _document.BeginGeneration(_scene, MapGraphGenerationMode.ValidateOnly);
            while (validate.IsRunning) { validate.Advance(64, 6); yield return null; }
            Assert.That(validate.Stage, Is.EqualTo(MapGraphGenerationStage.Ready), string.Join("\n", validate.Diagnostics));
            Assert.That(validate.SearchStates, Is.Zero); Assert.That(validate.CoordinateIterations, Is.Zero);
            Assert.That(_document.TryApplyGeneration(validate, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(new LayoutEvidence { nodes = _document.Layout.Nodes.ToArray(), zones = _document.Layout.Zones.ToArray(), edges = _document.Layout.Edges.ToArray() }), Is.EqualTo(layout));
            CaseArtifactWriter.Trace("saved-map", "28 targets, 7 zones, stable GUID, unchanged geometry, all baked edges reused.");
        }

        [UnityTest] public IEnumerator ConflictsAndInvalidPathsCannotMutateSavedInputs()
        {
            yield return GenerateSceneCopy(); var before = File.ReadAllBytes(TestScene);
            Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, "Assets/../bad.asset", out _, out _), Is.False);
            Assert.That(File.ReadAllBytes(TestScene), Is.EqualTo(before));
            Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, TestAsset, out var binding, out var failure), Is.True, failure);
            string originalBinding = EditorJsonUtility.ToJson(binding); before = File.ReadAllBytes(TestScene);
            var source = binding.MapDefinition;
            using (var serialized = new SerializedObject(source))
            { serialized.FindProperty("_displayName").stringValue = "外部编辑"; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            string changed = EditorJsonUtility.ToJson(source);
            Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, TestAsset, out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo("SourceAssetChanged"));
            Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(changed)); Assert.That(EditorJsonUtility.ToJson(binding), Is.EqualTo(originalBinding));
            Assert.That(File.ReadAllBytes(TestScene), Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator ExistingAssetFailureAfterAssetWriteRollsBack() => FailureAfterWrite(true, MapGraphSaveCheckpoint.AssetSaved);
        [UnityTest] public IEnumerator ExistingAssetFailureAfterSceneWriteRollsBack() => FailureAfterWrite(true, MapGraphSaveCheckpoint.SceneSaved);
        [UnityTest] public IEnumerator NewAssetFailureAfterSceneWriteRemovesOnlyItsNewObjects() => FailureAfterWrite(false, MapGraphSaveCheckpoint.SceneSaved);

        private IEnumerator FailureAfterWrite(bool existing, MapGraphSaveCheckpoint checkpoint)
        {
            yield return GenerateSceneCopy(); MapGraphBindingAuthoring saved = null;
            if (existing) Assert.That(MapGraphAuthoringTransaction.TrySave(_document, _scene, TestAsset, out saved, out var firstFailure), Is.True, firstFailure);
            byte[] sceneBytes = File.ReadAllBytes(TestScene), assetBytes = existing ? File.ReadAllBytes(TestAsset) : null;
            string bindingBefore = saved != null ? EditorJsonUtility.ToJson(saved) : "", assetBefore = existing ? EditorJsonUtility.ToJson(saved.MapDefinition) : "";
            _document.SetDisplayName("故障前的待保存修改"); bool observed = false;
            bool result = MapGraphAuthoringTransaction.TrySave(_document, _scene, TestAsset, phase =>
            {
                if (phase != checkpoint) return;
                observed = true; Assert.That(File.Exists(TestAsset), Is.True);
                throw new IOException("InjectedAfter" + phase);
            }, out _, out string failure);
            Assert.That(observed, Is.True); Assert.That(result, Is.False); Assert.That(failure, Does.Contain("InjectedAfter" + checkpoint));
            Assert.That(failure, Does.Not.Contain("RollbackFailed")); Assert.That(File.ReadAllBytes(TestScene), Is.EqualTo(sceneBytes));
            var bindings = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).ToArray();
            if (existing)
            {
                Assert.That(File.ReadAllBytes(TestAsset), Is.EqualTo(assetBytes)); Assert.That(bindings.Length, Is.EqualTo(1));
                Assert.That(EditorJsonUtility.ToJson(bindings[0]), Is.EqualTo(bindingBefore));
                Assert.That(EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(TestAsset)), Is.EqualTo(assetBefore));
                Assert.That(_document.HasSourceConflict, Is.False);
            }
            else
            {
                Assert.That(File.Exists(TestAsset) || File.Exists(TestAsset + ".meta"), Is.False);
                Assert.That(bindings, Is.Empty); Assert.That(_document.SourceDefinition, Is.Null);
            }
            Assert.That(_document.WorkingDefinition.DisplayName, Is.EqualTo("故障前的待保存修改")); Assert.That(_document.IsDirty, Is.True);
        }

        [Serializable] private sealed class LayoutEvidence
        { public MapGraphNodeDefinition[] nodes; public MapGraphZoneDefinition[] zones; public MapGraphEdgeDefinition[] edges; }
    }
}
