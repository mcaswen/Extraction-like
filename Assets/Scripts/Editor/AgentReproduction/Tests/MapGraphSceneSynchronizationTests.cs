using System;
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

namespace AgentReproduction.Tests
{
    public sealed class MapGraphSceneSynchronizationTests
    {
        private SO_MapGraphDefinition _definition;
        [SetUp] public void SetUp()
        {
            TestRunContext.Load(); Assert.That(Application.isPlaying, Is.False); Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity");
            CaseArtifactWriter.Trace("setup", "Scene synchronization fixtures; the source scene is never saved.");
        }
        [TearDown] public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (_definition != null) UnityEngine.Object.DestroyImmediate(_definition);
            CaseArtifactWriter.Complete("COMPLETED");
        }
        private static MapGraphSceneSnapshot Capture() => MapGraphSceneCollector.Capture(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        [Serializable] private sealed class IdentityEvidence
        {
            public string scene, fingerprint;
            public string[] differences, nodes;
        }
        [Test] public void SavedSceneAndBoundMapHaveMatchingNodeIdentities()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var snapshot = Capture();
            var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
            var definition = binding.MapDefinition;
            Assert.That(definition, Is.Not.Null);
            var differences = new System.Collections.Generic.List<string>();
            foreach (var saved in definition.Nodes)
            {
                var actual = snapshot.Nodes.FirstOrDefault(n => n.Id == saved.NodeId);
                if (actual == null) { differences.Add("Missing:" + saved.NodeId + " source=" + saved.SourceObjectId); continue; }
                if (saved.SourceObjectId != actual.SourceObjectId) differences.Add("Source:" + saved.NodeId + " old=" + saved.SourceObjectId + " new=" + actual.SourceObjectId);
                if (saved.ZoneId != actual.ZoneId) differences.Add("Zone:" + saved.NodeId + " old=" + saved.ZoneId + " new=" + actual.ZoneId + " hierarchy=" + actual.HierarchyPath);
                if (saved.NodeKind != actual.Kind) differences.Add("Kind:" + saved.NodeId + " old=" + saved.NodeKind + " new=" + actual.Kind);
            }
            foreach (var actual in snapshot.Nodes)
                if (!definition.Nodes.Any(n => n.NodeId == actual.Id)) differences.Add("Added:" + actual.Id + " source=" + actual.SourceObjectId + " hierarchy=" + actual.HierarchyPath);
            var evidence = new IdentityEvidence { scene = scene.path, fingerprint = snapshot.SceneFingerprint, differences = differences.ToArray(),
                nodes = snapshot.Nodes.Select(n => n.Id + " source=" + n.SourceObjectId + " zone=" + n.ZoneId + " kind=" + n.Kind + " hierarchy=" + n.HierarchyPath).ToArray() };
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath, "map-scene-identities.json"), JsonUtility.ToJson(evidence, true));
            Assert.That(snapshot.IsValid, Is.True, string.Join(";", snapshot.Diagnostics));
            Assert.That(differences, Is.Empty, string.Join(";", differences));
        }
        private MapGraphBindingAuthoring Bind(MapGraphSceneSnapshot snapshot, MapGraphLayoutDraft layout)
        {
            _definition = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            _definition.ApplyCommandData("fixture", "fixture", "", layout.Zones, layout.Nodes, layout.Edges, layout.Constraints, new MapGraphNavigationBakeData());
            var binding = new GameObject("FixtureBinding").AddComponent<MapGraphBindingAuthoring>();
            binding.Configure(_definition, snapshot.Nodes.Select(n => new MapGraphTargetBinding(n.Id, n.Target, n.Candidates[0].Position, n.SourceObjectId)),
                snapshot.Zones.Select(z => new MapGraphZoneBinding(z.Id, z.Target, z.SourceObjectId, z.IsSynthetic)));
            return binding;
        }
        [Test] public void UnassignedClusterGetsAStableDisplayZoneWithoutAWorldZone()
        {
            var before = Capture(); var node = before.Nodes.First(n => n.Kind == MapGraphNodeKind.Resource); var position = node.Target.transform.position;
            using (var serialized = new SerializedObject(node.Target))
            {
                serialized.FindProperty("_autoResolveZoneFromParent").boolValue = false;
                serialized.FindProperty("_zone").objectReferenceValue = null; serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var snapshot = Capture(); Assert.That(snapshot.IsValid, Is.True, string.Join(";", snapshot.Diagnostics));
            var synthetic = snapshot.Zones.Single(z => z.IsSynthetic);
            Assert.That(snapshot.Zones.Count, Is.EqualTo(8)); Assert.That(synthetic.Name, Is.EqualTo("未分区")); Assert.That(synthetic.Target, Is.Null);
            Assert.That(snapshot.Nodes.Single(n => n.Id == node.Id).ZoneId, Is.EqualTo(synthetic.Id));
            Assert.That(Capture().SceneFingerprint, Is.EqualTo(snapshot.SceneFingerprint));
            Assert.That(node.Target.Zone, Is.Null); Assert.That(node.Target.transform.position, Is.EqualTo(position));
            var layout = MapGraphLayoutGenerator.CreateReference(snapshot, new MapGraphGenerationSettings());
            var binding = Bind(snapshot, layout); Assert.That(binding.IsValid, Is.True, string.Join(";", binding.ValidationErrors));
            Assert.That(binding.ZoneBindings.Single(z => z.IsSynthetic).Zone, Is.Null);
            Assert.That(binding.TryGetNodeIdForDirectTarget(node.Target, out var id), Is.True); Assert.That(id, Is.EqualTo(node.Id));
            var restored = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(_definition), restored); restored.OnAfterDeserialize();
                Assert.That(restored.Zones.Single(z => z.IsSynthetic).ZoneId, Is.EqualTo(synthetic.Id));
                Assert.That(restored.Zones.Single(z => z.IsSynthetic).WithLayout(new Rect(10, 20, 200, 100), true).IsSynthetic, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(restored); }
        }
        [Test] public void MissingRealZoneCannotPretendToBeAnUnassignedDisplayZone()
        {
            var snapshot = Capture(); var layout = MapGraphLayoutGenerator.CreateReference(snapshot, new MapGraphGenerationSettings());
            var binding = Bind(snapshot, layout); Assert.That(binding.IsValid, Is.True);
            var zone = snapshot.Zones[0];
            binding.Configure(_definition, binding.TargetBindings, snapshot.Zones.Select(z => z.Id == zone.Id
                ? new MapGraphZoneBinding(z.Id, null, z.SourceObjectId)
                : new MapGraphZoneBinding(z.Id, z.Target, z.SourceObjectId)));
            Assert.That(binding.IsValid, Is.False); Assert.That(binding.ValidationErrors, Does.Contain("InvalidZoneBinding"));
            binding.Configure(_definition, binding.TargetBindings, snapshot.Zones.Select(z => z.Id == zone.Id
                ? new MapGraphZoneBinding(z.Id, null, z.SourceObjectId, true)
                : new MapGraphZoneBinding(z.Id, z.Target, z.SourceObjectId)));
            Assert.That(binding.ValidationErrors, Does.Contain("ZoneBindingKindMismatch:" + zone.Id));
        }
        [Test] public void SourceLabelsRefreshWhileAuthoredLayoutAndStylesStayFixed()
        {
            var scene = Capture(); var settings = new MapGraphGenerationSettings(); var previous = MapGraphLayoutGenerator.CreateReference(scene, settings);
            previous = MapGraphEditOperations.LockNode(previous, previous.Nodes[0].NodeId, true);
            previous = MapGraphEditOperations.LockZone(previous, previous.Zones[0].ZoneId, true);
            var manual = new MapGraphEdgeDefinition("author-edge", previous.Nodes[0].NodeId, previous.Nodes[1].NodeId, 999, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual, 3, 4, 2, true, Color.cyan);
            previous = new MapGraphLayoutDraft(previous.Zones, previous.Nodes, new[] { manual }, previous.Constraints);
            var renamed = new MapGraphSceneSnapshot(scene.ScenePath, scene.SceneGuid, "renamed", scene.NavigationFingerprint,
                scene.Zones.Select(z => new MapGraphSceneZone(z.Id, z.SourceObjectId, "新区域", z.Target, z.WorldBounds, z.IsSynthetic)),
                scene.Nodes.Select(n => new MapGraphSceneNode(n.Id, n.SourceObjectId, n.ZoneId, "新群名称", "新层级", n.PrefabPath, n.Target, n.WorldCenter, n.WorldBounds, n.Kind, n.Candidates)),
                scene.Profiles, Array.Empty<string>());
            var result = MapGraphLayoutGenerator.CreateReference(renamed, settings, previous);
            Assert.That(result.Zones.All(z => z.DisplayName == "新区域"), Is.True); Assert.That(result.Nodes.All(n => n.DisplayName == "新群名称" && n.Description == "新层级"), Is.True);
            foreach (var node in previous.Nodes) Assert.That(result.Graph.GetNodePosition(node.NodeId), Is.EqualTo(previous.Graph.GetNodePosition(node.NodeId)));
            Assert.That(result.Edges.Single().ColorOverride, Is.EqualTo(Color.cyan)); Assert.That(result.Nodes[0].PositionLocked, Is.True); Assert.That(result.Zones[0].LayoutLocked, Is.True);
            Assert.That(MapGraphLayoutIntentValidation.Validate(result, previous, MapGraphIntentPreservation.AllIntent | MapGraphIntentPreservation.Topology).IsValid, Is.True);
        }
        [Test] public void AnEditCannotChangeARealZonesIdentityIntoSynthetic()
        {
            var original = MapGraphLayoutGenerator.CreateReference(Capture(), new MapGraphGenerationSettings()); var target = original.Zones[0];
            var changed = new MapGraphLayoutDraft(original.Zones.Select(z => z.ZoneId == target.ZoneId
                ? new MapGraphZoneDefinition(z.ZoneId, z.DisplayName, z.Bounds, z.NameSafeSize, z.LayoutLocked, z.SourceObjectId, true) : z), original.Nodes, original.Edges, original.Constraints);
            Assert.That(MapGraphLayoutIntentValidation.Validate(changed, original).Issues.Any(i => i.Code == "ZoneIdentityChanged"), Is.True);
        }
    }
}
