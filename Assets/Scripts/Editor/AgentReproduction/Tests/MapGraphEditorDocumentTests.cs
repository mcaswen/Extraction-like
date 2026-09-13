using System;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphEditorDocumentTests
    {
        private TestWorldBuilder _world;
        private MapGraphSceneSnapshot _snapshot;
        private readonly List<MapGraphEditorDocument> _documents = new List<MapGraphEditorDocument>();
        private const string AssetPath = "Assets/MapGraphEditorDocument_Reproduction.asset";
        [SetUp] public void SetUp()
        {
            TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath), Is.Null);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _world = new TestWorldBuilder(); TestNavMeshBuilder.Flat(_world);
            var profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 1, 0.5f);
            var nodes = new List<MapGraphSceneNode>();
            for (int i = 0; i < 2; i++)
            {
                string id = ((char)('A' + i)).ToString(); var point = new Vector3(i * 10 - 5, 0, 3);
                nodes.Add(new MapGraphSceneNode(id, "source-" + id, "zone", id, id, "", null, point,
                    new Rect(point.x - 1, point.z - 1, 2, 2), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(point, "member-" + id) }));
            }
            _snapshot = new MapGraphSceneSnapshot("fixture", "scene", "scene-input", "nav-input",
                new[] { new MapGraphSceneZone("zone", "zone-source", "区域", null, new Rect(-8, -2, 16, 10)) }, nodes,
                new[] { new MapGraphSceneProfile(profile, MapGraphNavigationCostService.CaptureProfile(profile), new[] { "agent" }, new[] { Vector3.zero }) }, Array.Empty<string>());
            CaseArtifactWriter.Trace("setup", "Real Unity Undo and isolated working asset, fixture NavMesh.");
        }
        [TearDown] public void TearDown()
        {
            foreach (var document in _documents) document.Dispose(); _documents.Clear();
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath) != null) AssetDatabase.DeleteAsset(AssetPath);
            _world.Dispose(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); NavMesh.RemoveAllNavMeshData();
            CaseArtifactWriter.Complete("COMPLETED");
        }

        [Test] public void FirstGenerationUndoRedoRestoresGraphAndBakeAtomically()
        {
            var document = Document(); Assert.That(document.Layout, Is.Null); Assert.That(document.IsDirty, Is.False);
            Generate(document); string generated = EditorJsonUtility.ToJson(document.WorkingDefinition); long revision = document.Revision;
            Assert.That(document.IsDirty, Is.True); Assert.That(document.WorkingDefinition.NavigationBake.Edges.Count, Is.EqualTo(1));
            Assert.That(document.WorkingDefinition.NavigationBake.Revision, Is.EqualTo(document.WorkingDefinition.Revision));
            Undo.PerformUndo();
            Assert.That(document.Layout, Is.Null); Assert.That(document.IsDirty, Is.False); Assert.That(document.Revision, Is.GreaterThan(revision));
            revision = document.Revision; Undo.PerformRedo();
            Assert.That(EditorJsonUtility.ToJson(document.WorkingDefinition), Is.EqualTo(generated)); Assert.That(document.Revision, Is.GreaterThan(revision));
            Assert.That(document.Layout.Nodes.Count, Is.EqualTo(2));
        }

        [Test] public void MetadataAndSettingsHaveIndependentUndoWithoutRevertingEpoch()
        {
            var document = Document(); Generate(document); string original = EditorJsonUtility.ToJson(document.WorkingDefinition);
            document.SetDisplayName("新地图名称"); Assert.That(document.WorkingDefinition.DisplayName, Is.EqualTo("新地图名称"));
            document.SetGenerationSettings(JsonUtility.FromJson<MapGraphGenerationSettings>("{\"_queriesPerEditorTick\":3}"));
            long revision = document.Revision; Assert.That(document.WorkingDefinition.GenerationSettings.QueriesPerEditorTick, Is.EqualTo(3));
            Undo.PerformUndo(); Assert.That(document.WorkingDefinition.GenerationSettings.QueriesPerEditorTick, Is.EqualTo(12));
            Assert.That(document.WorkingDefinition.DisplayName, Is.EqualTo("新地图名称")); Assert.That(document.Revision, Is.GreaterThan(revision));
            Undo.PerformUndo(); Assert.That(EditorJsonUtility.ToJson(document.WorkingDefinition), Is.EqualTo(original));
            Undo.PerformRedo(); Assert.That(document.WorkingDefinition.DisplayName, Is.EqualTo("新地图名称"));
            Undo.PerformRedo(); Assert.That(document.WorkingDefinition.GenerationSettings.QueriesPerEditorTick, Is.EqualTo(3));
        }

        [Test] public void ReplacedEditedAndUndoneRequestsCannotOverwriteTheDocument()
        {
            var document = Document(); var first = document.BeginGeneration(() => _snapshot); Complete(first);
            var second = document.BeginGeneration(() => _snapshot);
            Assert.That(document.TryApplyGeneration(first, out var failure), Is.False); Assert.That(failure, Is.EqualTo("GenerationRequestSuperseded"));
            Complete(second); document.SetDisplayName("编辑后的名称");
            Assert.That(document.TryApplyGeneration(second, out _), Is.False); Assert.That(document.WorkingDefinition.DisplayName, Is.EqualTo("编辑后的名称"));
            var third = document.BeginGeneration(() => _snapshot); Complete(third); long revision = document.Revision;
            Undo.PerformUndo(); Assert.That(document.Revision, Is.GreaterThan(revision));
            Assert.That(document.TryApplyGeneration(third, out _), Is.False); Assert.That(document.Layout, Is.Null);
        }

        [Test] public void SceneChangeRejectsReadyGenerationWithoutCreatingUndoState()
        {
            var document = Document(); var request = document.BeginGeneration(() => _snapshot); Complete(request);
            string before = EditorJsonUtility.ToJson(document.WorkingDefinition); long revision = document.Revision;
            _snapshot = new MapGraphSceneSnapshot(_snapshot.ScenePath, _snapshot.SceneGuid, "changed", _snapshot.NavigationFingerprint,
                _snapshot.Zones, _snapshot.Nodes, _snapshot.Profiles, _snapshot.Diagnostics);
            Assert.That(document.TryApplyGeneration(request, out _), Is.False);
            Assert.That(EditorJsonUtility.ToJson(document.WorkingDefinition), Is.EqualTo(before)); Assert.That(document.Revision, Is.EqualTo(revision));
        }

        [Test] public void ClosingOrEditingTheWorkingCopyNeverWritesTheSourceAsset()
        {
            var source = CreateSourceAsset(); string original = EditorJsonUtility.ToJson(source);
            var document = Document(source);
            Assert.That(document.WorkingDefinition, Is.Not.SameAs(source)); Assert.That(AssetDatabase.GetAssetPath(document.WorkingDefinition), Is.Empty);
            Assert.That(document.WorkingDefinition.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            document.SetDisplayName("只在草稿中修改"); Assert.That(document.HasSourceConflict, Is.False);
            var request = document.BeginGeneration(() => _snapshot); request.Advance(1);
            var temporary = document.WorkingDefinition; document.Dispose();
            Assert.That(temporary == null, Is.True); Assert.That(request.Stage, Is.EqualTo(MapGraphGenerationStage.Cancelled));
            Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(original)); Assert.That(EditorUtility.IsDirty(source), Is.False);
            Assert.Throws<ObjectDisposedException>(() => document.SetDisplayName("已关闭"));
            Resources.UnloadAsset(source); var loaded = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetPath);
            Assert.That(EditorJsonUtility.ToJson(loaded), Is.EqualTo(original));
        }

        [Test] public void ExternalInspectorChangesAreDetectedWithoutASourceRevisionIncrement()
        {
            var source = CreateSourceAsset(); long revision = source.Revision; var document = Document(source);
            using (var serialized = new SerializedObject(source))
            { serialized.FindProperty("_displayName").stringValue = "外部修改"; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            Assert.That(source.Revision, Is.EqualTo(revision)); Assert.That(document.HasSourceConflict, Is.True);
            Assert.That(document.WorkingDefinition.DisplayName, Is.Not.EqualTo(source.DisplayName));
        }

        [Test] public void BakeUsesAuthoredEdgeIdentityAndBothMeasuredDirections()
        {
            var document = Document(); Generate(document); var evidence = document.LastVerifiedInput;
            var original = evidence.Layout.Edges.Single(); var measured = evidence.Connections.Single();
            var reverse = new MapGraphEdgeDefinition("manually-named-edge", measured.Edge.ToNodeId, measured.Edge.FromNodeId, 999,
                original.Axis, MapGraphEdgeOrigin.Manual, 3, 5, 2, true, Color.cyan);
            var layout = new MapGraphLayoutDraft(evidence.Layout.Zones, evidence.Layout.Nodes, new[] { reverse }, evidence.Layout.Constraints);
            var matrix = new[] { new MapGraphScannedConnection(measured.ProfileId,
                new MapGraphNavigationEdgeBake(measured.Edge.EdgeId, measured.Edge.FromNodeId, measured.Edge.ToNodeId,
                    measured.Edge.FromAnchor, measured.Edge.ToAnchor, 12, 17)) };
            var bake = MapGraphNavigationBakeBuilder.Build(_snapshot, layout, evidence.Anchors, matrix, 45, measured.ProfileId);
            Assert.That(bake.Revision, Is.EqualTo(45)); Assert.That(bake.Edges.Single().EdgeId, Is.EqualTo("manually-named-edge"));
            Assert.That(bake.Edges[0].ForwardLength, Is.EqualTo(17)); Assert.That(bake.Edges[0].ReverseLength, Is.EqualTo(12));
            Assert.That(bake.Edges[0].FromAnchor, Is.EqualTo(measured.Edge.ToAnchor)); Assert.That(bake.Edges[0].ToAnchor, Is.EqualTo(measured.Edge.FromAnchor));
            Assert.That(bake.SceneFingerprint, Is.EqualTo(_snapshot.SceneFingerprint));
        }

        [Test] public void IncompleteUnavailableOrWrongProfileEvidenceCannotProduceABake()
        {
            var document = Document(); Generate(document); var evidence = document.LastVerifiedInput;
            var measured = evidence.Connections.Single();
            Assert.Throws<ArgumentException>(() => MapGraphNavigationBakeBuilder.Build(evidence, 1, "wrong-profile"));
            Assert.Throws<InvalidOperationException>(() => MapGraphNavigationBakeBuilder.Build(_snapshot, evidence.Layout,
                Array.Empty<MapGraphScannedAnchor>(), evidence.Connections, 1, measured.ProfileId));
            var blocked = new[] { new MapGraphScannedConnection(measured.ProfileId, new MapGraphNavigationEdgeBake(measured.Edge.EdgeId,
                measured.Edge.FromNodeId, measured.Edge.ToNodeId, measured.Edge.FromAnchor, measured.Edge.ToAnchor, 10, float.PositiveInfinity, reverseFailure: "Partial")) };
            Assert.Throws<InvalidOperationException>(() => MapGraphNavigationBakeBuilder.Build(_snapshot, evidence.Layout, evidence.Anchors, blocked, 1, measured.ProfileId));
            var wrongAnchor = new[] { new MapGraphScannedConnection(measured.ProfileId, new MapGraphNavigationEdgeBake(measured.Edge.EdgeId,
                measured.Edge.FromNodeId, measured.Edge.ToNodeId, measured.Edge.FromAnchor + Vector3.up, measured.Edge.ToAnchor, 10, 12)) };
            Assert.Throws<InvalidOperationException>(() => MapGraphNavigationBakeBuilder.Build(_snapshot, evidence.Layout, evidence.Anchors, wrongAnchor, 1, measured.ProfileId));
        }

        [Test] public void AuthoredDeletionUsesUndoAndPreservesTheFrozenNavigationInput()
        {
            var document = Document(); Generate(document); string before = document.Layout.ContentFingerprint;
            var request = document.BeginEdit(g => MapGraphEditOperations.DeleteEdge(g, g.Edges[0].EdgeId), "删除连接");
            Assert.That(document.TryApplyEdit(request, out var failure), Is.True, failure);
            Assert.That(document.Layout.Edges, Is.Empty); Assert.That(document.WorkingDefinition.NavigationBake.Edges, Is.Empty);
            Assert.That(document.TryVerifyForSave(out _, out failure), Is.True, failure);
            Undo.PerformUndo(); Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(document.Layout.Edges, Is.Empty);
        }

        [Test] public void EditingAfterStartingAPreviewRejectsTheEarlierPreview()
        {
            var document = Document(); Generate(document);
            var request = document.BeginEdit(g => MapGraphEditOperations.DeleteEdge(g, g.Edges[0].EdgeId), "删除连接");
            document.SetDisplayName("更晚的修改");
            Assert.That(document.TryApplyEdit(request, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo("EditRequestSuperseded")); Assert.That(document.Layout.Edges.Count, Is.EqualTo(1));
        }

        [Test] public void PlacementPreviewUsesRealNavigationAndUndoRestoresTheUnpublishedDraft()
        {
            var document = Document(); Generate(document); string before = document.Layout.ContentFingerprint;
            var zone = document.Layout.Zones[0];
            document.EditPlacement(g => MapGraphGridPlacement.MoveZone(g, zone.ZoneId, new Rect(zone.Bounds.position + Vector2.right * 80, zone.Bounds.size), 80), "移动区域草稿");
            string draft = document.AuthoringLayout.ContentFingerprint;
            var request = document.BeginGeneration(() => _snapshot, MapGraphGenerationMode.PlacementConnections); Complete(request);
            Assert.That(request.NavigationQueries, Is.GreaterThan(0)); Assert.That(document.HasPlacementDraft, Is.True);
            Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(before));
            Assert.That(document.TryApplyGeneration(request, out var failure), Is.True, failure);
            Assert.That(document.HasPlacementDraft, Is.False); Assert.That(document.TryVerifyForSave(out _, out failure), Is.True, failure);
            string accepted = document.Layout.ContentFingerprint;
            Undo.PerformUndo(); Assert.That(document.HasPlacementDraft, Is.True); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(draft));
            Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(before)); Assert.That(document.TryVerifyForSave(out _, out _), Is.False);
            Undo.PerformRedo(); Assert.That(document.HasPlacementDraft, Is.False); Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(accepted));
        }

        [Test] public void PlacementEditAndSceneChangesRejectReadyCandidatesWithoutLosingDraft()
        {
            var document = Document(); Generate(document);
            var request = document.BeginGeneration(() => _snapshot, MapGraphGenerationMode.PlacementConnections); Complete(request);
            document.EditPlacement(g => MapGraphEditOperations.LockNode(g, g.Nodes[0].NodeId, true), "固定群");
            Assert.That(document.TryApplyGeneration(request, out _), Is.False); Assert.That(document.HasPlacementDraft, Is.True);
            var second = document.BeginGeneration(() => _snapshot, MapGraphGenerationMode.PlacementConnections); Complete(second);
            string draft = document.AuthoringLayout.ContentFingerprint;
            _snapshot = new MapGraphSceneSnapshot(_snapshot.ScenePath, _snapshot.SceneGuid, "changed", _snapshot.NavigationFingerprint,
                _snapshot.Zones, _snapshot.Nodes, _snapshot.Profiles, _snapshot.Diagnostics);
            Assert.That(document.TryApplyGeneration(second, out _), Is.False);
            Assert.That(second.Stage, Is.EqualTo(MapGraphGenerationStage.Stale)); Assert.That(document.AuthoringLayout.ContentFingerprint, Is.EqualTo(draft));
        }

        [Test] public void ExplicitConnectionRejectsDuplicatesAndRebindsWithoutChangingStyle()
        {
            var document = Document(); Generate(document); var edge = document.Layout.Edges[0]; string before = document.Layout.ContentFingerprint;
            Assert.That(document.TryConnectPlacement(edge.FromNodeId, edge.ToNodeId, edge.Axis, "", out _, out string failure), Is.False);
            StringAssert.Contains("ConnectionAlreadyExists", failure); Assert.That(document.HasPlacementDraft, Is.False);
            document.EditPlacement(g => MapGraphEditOperations.StyleEdge(g, edge.EdgeId, 2, 3, 4, true, Color.cyan), "样式");
            Assert.That(document.TryConnectPlacement(edge.ToNodeId, edge.FromNodeId, edge.Axis, edge.EdgeId, out string id, out failure), Is.True, failure);
            Assert.That(id, Is.EqualTo(edge.EdgeId)); var changed = document.AuthoringLayout.Edges[0];
            Assert.That(changed.FromNodeId, Is.EqualTo(edge.ToNodeId)); Assert.That(changed.FromInset, Is.EqualTo(2)); Assert.That(changed.ColorOverride, Is.EqualTo(Color.cyan));
            document.EditPlacement(g => MapGraphEditOperations.DeleteEdge(g, id), "删除");
            Assert.That(document.AuthoringLayout.Constraints.IsExcluded(edge.FromNodeId, edge.ToNodeId), Is.True);
            Assert.That(document.TryConnectPlacement(edge.FromNodeId, edge.ToNodeId, edge.Axis, "", out _, out failure), Is.True, failure);
            Assert.That(document.AuthoringLayout.Constraints.IsExcluded(edge.FromNodeId, edge.ToNodeId), Is.False);
            Assert.That(document.Layout.ContentFingerprint, Is.EqualTo(before));
        }

        [Test] public void NavigationRefreshDoesNotReplaceRecoveredInvalidPlacement()
        {
            var source = CreateSourceAsset(); var first = Document(source);
            first.EditPlacement(g => MapGraphGridPlacement.MoveNode(g, g.Nodes[0].NodeId, Vector2.one * 9000, 80), "未完成摆放");
            var restored = new MapGraphEditorDocument(source, first.ExportWorkingCopy(), first.SourceBaseline, first.ExportPlacement()); _documents.Add(restored);
            string draft = restored.AuthoringLayout.ContentFingerprint;
            var request = restored.BeginNavigationRefresh(() => _snapshot); Complete(request);
            Assert.That(restored.TryRefreshNavigationEvidence(request, out string failure), Is.True, failure);
            Assert.That(restored.HasPlacementDraft, Is.True); Assert.That(restored.AuthoringLayout.ContentFingerprint, Is.EqualTo(draft));
            Assert.That(restored.PendingGeneration, Is.Null); Assert.That(restored.LastVerifiedInput, Is.Not.Null);
            Assert.That(restored.TryVerifyForSave(out _, out _), Is.False);
        }

        private MapGraphEditorDocument Document(SO_MapGraphDefinition source = null)
        { var document = new MapGraphEditorDocument(source); _documents.Add(document); return document; }
        private void Generate(MapGraphEditorDocument document)
        {
            var request = document.BeginGeneration(() => _snapshot); Complete(request);
            Assert.That(document.TryApplyGeneration(request, out var failure), Is.True, failure);
            Assert.That(MapGraphValidation.Validate(document.Layout).IsValid, Is.True);
        }
        private static void Complete(MapGraphGenerationController controller)
        {
            for (int i = 0; controller.IsRunning && i < 1000; i++) controller.Advance(32, 1000);
            Assert.That(controller.Stage, Is.EqualTo(MapGraphGenerationStage.Ready), string.Join("\n", controller.Diagnostics));
        }
        private SO_MapGraphDefinition CreateSourceAsset()
        {
            var initial = Document(); Generate(initial);
            var source = UnityEngine.Object.Instantiate(initial.WorkingDefinition); source.hideFlags = HideFlags.None; source.name = "Source fixture";
            AssetDatabase.CreateAsset(source, AssetPath); AssetDatabase.SaveAssetIfDirty(source); return source;
        }
    }
}
