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
    public sealed class MapGraphSceneMergeTests
    {
        private const string Guid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private TestWorldBuilder _world;
        private SO_MapGraphDefinition _source;
        private MapGraphLayoutDraft _original;
        private MapGraphSceneSnapshot _current;
        [SetUp] public void SetUp()
        {
            TestRunContext.Load(); _world = new TestWorldBuilder(); TestNavMeshBuilder.Flat(_world);
            var zones = new[] { new MapGraphZoneDefinition("lab", "实验室", new Rect(-240,-160,480,320), new Vector2(100,28), true, Source(1)),
                new MapGraphZoneDefinition("rain", "雨林", new Rect(560,-160,480,320), new Vector2(100,28), sourceObjectId: Source(2)) };
            var nodes = new[] { Node("deleted", "lab", new Vector2(80,80), 10), Node("moved", "rain", new Vector2(-80,80), 11),
                Node("kept", "lab", new Vector2(-80,-80), 12, true), Node("other", "rain", new Vector2(80,80), 13) };
            var edges = new[] { new MapGraphEdgeDefinition("removed-edge", "deleted", "other", 10, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual),
                new MapGraphEdgeDefinition("kept-edge", "moved", "other", 10, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual, 2, 3, 4, true, Color.cyan) };
            _original = MapGraphGridPlacement.WithPositions(new MapGraphLayoutDraft(zones, nodes, edges,
                new MapGraphLayoutConstraints(null, new[] { new MapGraphConnectionExclusion("deleted","kept"), new MapGraphConnectionExclusion("kept","other") }), "deleted"), null);
            _current = Snapshot(true);
            _source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            _source.ApplyCommandData("fixture", "同步构造", _original.StartNodeId, _original.Zones, _original.Nodes, _original.Edges, _original.Constraints, new MapGraphNavigationBakeData());
            CaseArtifactWriter.Trace("setup", "Delete one lab group, transfer one rainforest group to lab, retain author intent.");
        }
        [TearDown] public void TearDown()
        { if (_source != null) UnityEngine.Object.DestroyImmediate(_source); _world.Dispose(); NavMesh.RemoveAllNavMeshData(); CaseArtifactWriter.Complete("COMPLETED"); }
        private static string Source(int id) => "GlobalObjectId_V1-2-" + Guid + "-" + id + "-0";
        private static MapGraphNodeDefinition Node(string id, string zone, Vector2 position, int source, bool locked = false)
            => new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, position, "群-" + id, zone + "/群-" + id, zoneId: zone, positionLocked: locked, sourceObjectId: Source(source));
        private MapGraphSceneSnapshot Snapshot(bool edited, string fingerprint = null)
        {
            var profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 1, 0.5f);
            return new MapGraphSceneSnapshot("fixture", Guid, fingerprint ?? (edited ? "edited" : "original"), "nav",
                _original.Zones.Select(z => new MapGraphSceneZone(z.ZoneId, z.SourceObjectId, z.DisplayName, null, new Rect(-10,-10,20,20))),
                _original.Nodes.Where(n => !edited || n.NodeId != "deleted").Select((n,i) => new MapGraphSceneNode(n.NodeId, n.SourceObjectId,
                    edited && n.NodeId == "moved" ? "lab" : n.ZoneId, n.DisplayName, (edited && n.NodeId == "moved" ? "lab" : n.ZoneId) + "/" + n.DisplayName,
                    "", null, new Vector3(i*3-3,0,3), new Rect(-2,-2,4,4), n.NodeKind,
                    new[] { new MapGraphAnchorCandidate(new Vector3(i*3-3,0,3), "member-"+n.NodeId) })),
                new[] { new MapGraphSceneProfile(profile, MapGraphNavigationCostService.CaptureProfile(profile), new[] { "agent" }, new[] { Vector3.zero }) }, Array.Empty<string>());
        }
        [Test] public void DeletedAndReassignedGroupsMergeWithoutResettingOtherAuthorData()
        {
            var result = MapGraphSceneSynchronizer.Prepare(_current, _original, new MapGraphGenerationSettings());
            Assert.That(result.Layout.Graph.TryGetNode("deleted", out _), Is.False); Assert.That(result.Layout.StartNodeId, Is.Empty);
            Assert.That(result.Layout.Graph.TryGetNode("moved", out var moved), Is.True); Assert.That(moved.ZoneId, Is.EqualTo("lab"));
            Assert.That(moved.Position, Is.EqualTo(new Vector2(-80,80)));
            Assert.That(result.Layout.Graph.GetNodePosition("kept"), Is.EqualTo(_original.Graph.GetNodePosition("kept")));
            Assert.That(result.Layout.Nodes.Single(n => n.NodeId == "kept").PositionLocked, Is.True); Assert.That(result.Layout.Zones[0].LayoutLocked, Is.True);
            Assert.That(result.Layout.Edges.Single().EdgeId, Is.EqualTo("kept-edge")); Assert.That(result.Layout.Edges[0].ColorOverride, Is.EqualTo(Color.cyan));
            Assert.That(result.Layout.Edges[0].FromInset, Is.EqualTo(2)); Assert.That(result.Layout.Constraints.ExcludedConnections.Count, Is.EqualTo(1));
            Assert.That(result.Layout.Constraints.IsExcluded("kept","other"), Is.True);
            Assert.That(result.Changes.Select(c => c.Code), Does.Contain("SceneNodeRemoved").And.Contain("SceneNodeReassigned").And.Contain("SceneEdgeRemoved"));
            CaseArtifactWriter.Trace("scene-sync-changes", string.Join("\n", result.Changes));
        }
        [Test] public void RepeatedSynchronizationIsIdempotentAndWorldMovementKeepsManualMapLayout()
        {
            var first = MapGraphSceneSynchronizer.Prepare(_current, _original, new MapGraphGenerationSettings());
            var repeat = MapGraphSceneSynchronizer.Prepare(_current, first.Layout, new MapGraphGenerationSettings());
            Assert.That(repeat.HasChanges, Is.False); Assert.That(repeat.Layout, Is.SameAs(first.Layout));
            var movedWorld = new MapGraphSceneSnapshot(_current.ScenePath, Guid, "world-moved", "nav", _current.Zones,
                _current.Nodes.Select(n => new MapGraphSceneNode(n.Id,n.SourceObjectId,n.ZoneId,n.Name,n.HierarchyPath,"",null,n.WorldCenter+Vector3.one*200,n.WorldBounds,n.Kind,n.Candidates)), _current.Profiles, Array.Empty<string>());
            Assert.That(MapGraphSceneSynchronizer.Prepare(movedWorld, first.Layout, new MapGraphGenerationSettings()).Layout.ContentFingerprint, Is.EqualTo(first.Layout.ContentFingerprint));
        }
        [Test] public void AddedNodesAndRenamedZonesKeepExistingIdsAndCoordinates()
        {
            var originalScene = Snapshot(false);
            var extra = new MapGraphSceneNode("new",Source(20),"lab","新增群","lab/新增群","",null,Vector3.zero,new Rect(-1,-1,2,2),MapGraphNodeKind.Extraction,
                new[] { new MapGraphAnchorCandidate(Vector3.zero,"member-new") });
            var scene = new MapGraphSceneSnapshot("fixture",Guid,"added","nav",originalScene.Zones.Select(z => new MapGraphSceneZone(z.Id,z.SourceObjectId,"改名-"+z.Name,null,z.WorldBounds)),
                originalScene.Nodes.Concat(new[] { extra }), originalScene.Profiles, Array.Empty<string>());
            var result=MapGraphSceneSynchronizer.Prepare(scene,_original,new MapGraphGenerationSettings());
            Assert.That(result.Layout.Nodes.Count,Is.EqualTo(5)); Assert.That(result.Layout.Graph.TryGetNode("new",out var node),Is.True);
            Assert.That(node.ZoneId,Is.EqualTo("lab")); Assert.That(result.Layout.Zones.All(z=>z.DisplayName.StartsWith("改名-")),Is.True);
            foreach(var old in _original.Nodes) Assert.That(result.Layout.Graph.GetNodePosition(old.NodeId),Is.EqualTo(_original.Graph.GetNodePosition(old.NodeId)));
        }
        [Test] public void ForeignScenesAndIdentityCollisionsAreRejectedBeforeMutation()
        {
            var foreign=new MapGraphSceneSnapshot("other","bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","other","nav",_current.Zones,_current.Nodes,_current.Profiles,Array.Empty<string>());
            Assert.Throws<InvalidOperationException>(()=>MapGraphSceneSynchronizer.Prepare(foreign,_original,new MapGraphGenerationSettings()));
            var collision=new MapGraphSceneSnapshot("fixture",Guid,"collision","nav",_current.Zones,
                _current.Nodes.Select(n=>new MapGraphSceneNode(n.Id,Source(99),n.ZoneId,n.Name,n.HierarchyPath,"",null,n.WorldCenter,n.WorldBounds,n.Kind,n.Candidates)),_current.Profiles,Array.Empty<string>());
            Assert.Throws<InvalidOperationException>(()=>MapGraphSceneSynchronizer.Prepare(collision,_original,new MapGraphGenerationSettings()));
            Assert.That(_original.Nodes.Count,Is.EqualTo(4));
        }
        [Test] public void RemovedZonesAndChangedKindsFollowTheCurrentScene()
        {
            var scene=new MapGraphSceneSnapshot("fixture",Guid,"zone-removed","nav",_current.Zones.Where(z=>z.Id=="lab"),
                _current.Nodes.Select(n=>new MapGraphSceneNode(n.Id,n.SourceObjectId,"lab",n.Name,"lab/"+n.Name,"",null,n.WorldCenter,n.WorldBounds,
                    n.Id=="kept"?MapGraphNodeKind.Extraction:n.Kind,n.Candidates)),_current.Profiles,Array.Empty<string>());
            var result=MapGraphSceneSynchronizer.Prepare(scene,_original,new MapGraphGenerationSettings());
            Assert.That(result.Layout.Zones.Count,Is.EqualTo(1)); Assert.That(result.Layout.Nodes.All(n=>n.ZoneId=="lab"),Is.True);
            Assert.That(result.Layout.Nodes.Single(n=>n.NodeId=="kept").NodeKind,Is.EqualTo(MapGraphNodeKind.Extraction));
            Assert.That(result.Changes.Select(c=>c.Code),Does.Contain("SceneZoneRemoved").And.Contain("SceneNodeKindChanged"));
            Assert.That(MapGraphSceneSynchronizer.Prepare(scene,result.Layout,new MapGraphGenerationSettings()).HasChanges,Is.False);
        }
        [Test] public void SynchronizationIsUndoableRecoverableAndDoesNotWritePublishedGraph()
        {
            using var document=new MapGraphEditorDocument(_source); string source=EditorJsonUtility.ToJson(_source);
            var request=document.PrepareSceneSynchronization(()=>_current); Assert.That(document.TryApplySceneSynchronization(request,out var failure),Is.True,failure);
            Assert.That(document.HasPlacementDraft,Is.True); Assert.That(document.Layout.Nodes.Count,Is.EqualTo(4)); Assert.That(document.AuthoringLayout.Nodes.Count,Is.EqualTo(3));
            Assert.That(document.TryVerifyForSave(out _,out _),Is.False); long revision=document.Revision;
            var repeated=document.PrepareSceneSynchronization(()=>_current); Assert.That(document.TryApplySceneSynchronization(repeated,out failure),Is.True,failure);
            Assert.That(document.Revision,Is.EqualTo(revision));
            Undo.PerformUndo(); Assert.That(document.HasPlacementDraft,Is.False); Undo.PerformRedo(); Assert.That(document.AuthoringLayout.Nodes.Count,Is.EqualTo(3));
            using var recovered=new MapGraphEditorDocument(_source,document.ExportWorkingCopy(),document.SourceBaseline,document.ExportPlacement());
            Assert.That(recovered.AuthoringLayout.ContentFingerprint,Is.EqualTo(document.AuthoringLayout.ContentFingerprint));
            Assert.That(EditorJsonUtility.ToJson(_source),Is.EqualTo(source));
        }
        [Test] public void SceneAndDocumentChangesRejectAnOutdatedSynchronizationProposal()
        {
            using var document=new MapGraphEditorDocument(_source);
            var request=document.PrepareSceneSynchronization(()=>_current); _current=Snapshot(true,"newer");
            Assert.That(document.TryApplySceneSynchronization(request,out _),Is.False); Assert.That(document.HasPlacementDraft,Is.False);
            request=document.PrepareSceneSynchronization(()=>_current); document.SetDisplayName("编辑新名称");
            Assert.That(document.TryApplySceneSynchronization(request,out _),Is.False); Assert.That(document.HasPlacementDraft,Is.False);
        }
        [Test] public void NavigationEvidenceForSynchronizedDraftEnablesConnectionsButCannotPublish()
        {
            using var document=new MapGraphEditorDocument(_source);
            Assert.That(document.TryApplySceneSynchronization(document.PrepareSceneSynchronization(()=>_current),out var failure),Is.True,failure);
            document.EditPlacement(g=>MapGraphGridPlacement.MoveNode(g,"other",new Vector2(800,0),80),"构造尚未修正的旧线");
            var request=document.BeginNavigationRefresh(()=>_current);
            for(int i=0;request.IsRunning && i<1000;i++) request.Advance(32,1000);
            Assert.That(request.Stage,Is.EqualTo(MapGraphGenerationStage.Ready),string.Join(";",request.Diagnostics));
            Assert.That(request.Mode,Is.EqualTo(MapGraphGenerationMode.NavigationEvidence));
            Assert.That(MapGraphValidation.Validate(request.Result.Layout).IsValid,Is.False);
            Assert.That(document.TryApplyGeneration(request,out _),Is.False);
            Assert.That(document.TryRefreshNavigationEvidence(request,out failure),Is.True,failure);
            Assert.That(document.TryVerifyForSave(out _,out _),Is.False);
            Assert.That(document.TryConnectPlacement("kept","moved",MapGraphAxis.Vertical,"",out _,out failure),Is.True,failure);
        }
        [Test] public void ActualSavedSceneCanSynchronizeItsBoundMapWithoutWritingAssets()
        {
            _world.Dispose(); NavMesh.RemoveAllNavMeshData();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity");
            try
            {
                var binding=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
                string before=EditorJsonUtility.ToJson(binding.MapDefinition); bool dirty=scene.isDirty;
                var captured=MapGraphSceneCollector.Capture(scene);
                using var document=new MapGraphEditorDocument(binding.MapDefinition);
                var request=document.PrepareSceneSynchronization(scene);
                Assert.That(document.TryApplySceneSynchronization(request,out var failure),Is.True,failure);
                Assert.That(document.AuthoringLayout.Nodes.Select(n=>n.NodeId),Is.EquivalentTo(captured.Nodes.Select(n=>n.Id)));
                foreach(var node in document.AuthoringLayout.Nodes)
                {
                    var actual=captured.Nodes.Single(n=>n.Id==node.NodeId);
                    Assert.That(node.ZoneId,Is.EqualTo(actual.ZoneId)); Assert.That(node.SourceObjectId,Is.EqualTo(actual.SourceObjectId)); Assert.That(node.NodeKind,Is.EqualTo(actual.Kind));
                }
                Assert.That(EditorJsonUtility.ToJson(binding.MapDefinition),Is.EqualTo(before)); Assert.That(scene.isDirty,Is.EqualTo(dirty));
                CaseArtifactWriter.Trace("actual-scene-sync", "nodes="+captured.Nodes.Count+"; zones="+captured.Zones.Count+"\n"+string.Join("\n",request.Changes));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        }
    }
}
