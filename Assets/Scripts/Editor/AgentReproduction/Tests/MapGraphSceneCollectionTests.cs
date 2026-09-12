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
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphSceneCollectionTests
    {
        private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        private TestWorldBuilder _world;
        [SetUp]
        public void SetUp()
        {
            TestRunContext.Load();
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(Application.productName, Does.StartWith("AgentRepro_"));
            _world = new TestWorldBuilder();
            CaseArtifactWriter.Trace("setup", "Editor scene collection; no Play Mode or actor movement.");
        }
        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavMesh.RemoveAllNavMeshData();
            CaseArtifactWriter.Complete("COMPLETED");
        }

        [UnityTest]
        public IEnumerator ActualSceneIdentityProfilesAndPrefabReferencesAreStable()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            yield return null;
            Physics.SyncTransforms();
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var poses = components.Select(x => x.position).ToArray();
            var clusterTargets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplayTargetClusterAuthoringBase>(true)).ToArray();
            var serialized = clusterTargets.Select(x => EditorJsonUtility.ToJson(x)).ToArray();
            var random = UnityEngine.Random.state;
            var first = MapGraphSceneCollector.Capture(scene);
            var second = MapGraphSceneCollector.Capture(scene);
            WriteEvidence(first, null, "scene-collection.json");
            Assert.That(first.Diagnostics, Is.Empty);
            Assert.That(first.Zones.Count, Is.EqualTo(7));
            Assert.That(first.Nodes.Count, Is.EqualTo(28));
            Assert.That(first.Nodes.Count(n => n.Kind == MapGraphNodeKind.Resource), Is.EqualTo(12));
            Assert.That(first.Nodes.Count(n => n.Kind == MapGraphNodeKind.EnemySource), Is.EqualTo(14));
            Assert.That(first.Nodes.Count(n => n.Kind == MapGraphNodeKind.Extraction), Is.EqualTo(2));
            Assert.That(first.Profiles.Sum(p => p.AgentSourceIds.Count), Is.EqualTo(2));
            Assert.That(first.Nodes.Select(n => n.Id).Distinct().Count(), Is.EqualTo(28));
            Assert.That(first.Nodes.Where(n => n.Kind == MapGraphNodeKind.Resource).All(n => n.PrefabPath.EndsWith("ResourceCluster.prefab")), Is.True);
            Assert.That(first.Zones.All(z => first.Nodes.Any(n => n.ZoneId == z.Id)), Is.True,
                "The user removed the erroneous empty village zone; use the corrected scene baseline.");
            Assert.That(second.SceneFingerprint, Is.EqualTo(first.SceneFingerprint));
            Assert.That(second.NavigationFingerprint, Is.EqualTo(first.NavigationFingerprint));
            Assert.That(second.Nodes.Select(n => n.Id), Is.EqualTo(first.Nodes.Select(n => n.Id)));
            Assert.That(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(components.Length));
            Assert.That(components.Select(x => x.position), Is.EqualTo(poses));
            Assert.That(clusterTargets.Select(x => EditorJsonUtility.ToJson(x)), Is.EqualTo(serialized));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random), "Scene collection must not consume spawn randomness.");
        }

        [UnityTest]
        public IEnumerator ActualSceneAnchorsAndAllDirectedPairsAreMeasuredWithinBudget()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            yield return null;
            Physics.SyncTransforms();
            var snapshot = MapGraphSceneCollector.Capture(scene);
            var scan = new MapGraphSceneNavigationScan(snapshot);
            int ticks = 0;
            while (!scan.IsComplete && ticks++ < 2000)
            {
                long before = scan.TotalQueryCount;
                Assert.That(scan.Advance(7), Is.LessThanOrEqualTo(7));
                Assert.That(scan.TotalQueryCount - before, Is.LessThanOrEqualTo(14));
                yield return null;
            }
            WriteEvidence(snapshot, scan, "scene-navigation.json");
            Assert.That(scan.IsComplete && !scan.IsCancelled, Is.True);
            Assert.That(snapshot.Diagnostics, Is.Empty);
            Assert.That(scan.Anchors.Count, Is.EqualTo(28 * snapshot.Profiles.Count));
            Assert.That(scan.Anchors.Where(a => !a.IsValid).Select(a => a.NodeId + ":" + a.Failure), Is.Empty);
            Assert.That(scan.Connections.Count, Is.EqualTo(378 * snapshot.Profiles.Count));
            Assert.That(scan.DirectedQueryCount, Is.EqualTo(756 * snapshot.Profiles.Count));
            Assert.That(scan.Connections.All(c => c.Edge.ForwardAvailable == c.Edge.ReverseAvailable), Is.True,
                "A one-way route needs explicit design review, not a fake bidirectional edge.");
            Assert.That(scan.Connections.Any(c => c.Edge.ForwardAvailable && c.Edge.ReverseAvailable), Is.True);
        }

        [Test]
        public void SamplingAndCancellationNeverInventMissingConnections()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TestNavMeshBuilder.Flat(_world);
            var profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 3, 0.5f);
            var nodes = new[]
            {
                new MapGraphSceneNode("A", "A-source", "zone", "A", "A", "", null, Vector3.zero, new Rect(), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(Vector3.up * 2, "wrong-floor"), new MapGraphAnchorCandidate(Vector3.zero, "ground") }),
                new MapGraphSceneNode("B", "B-source", "zone", "B", "B", "", null, Vector3.right, new Rect(), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(Vector3.up * 8, "missing-floor") })
            };
            var snapshot = new MapGraphSceneSnapshot("fixture", "fixture", "scene", "nav", Array.Empty<MapGraphSceneZone>(), nodes,
                new[] { new MapGraphSceneProfile(profile, MapGraphNavigationCostService.CaptureProfile(profile), Array.Empty<string>(), Array.Empty<Vector3>()) },
                Array.Empty<string>());
            var cancelled = new MapGraphSceneNavigationScan(snapshot);
            Assert.That(cancelled.Advance(1), Is.EqualTo(1));
            cancelled.Cancel();
            Assert.That(cancelled.Advance(100), Is.Zero);
            Assert.That(cancelled.IsCancelled && !cancelled.IsComplete, Is.True);
            Assert.That(cancelled.Connections, Is.Empty);
            var scan = new MapGraphSceneNavigationScan(snapshot);
            for (int i = 0; !scan.IsComplete && i < 20; i++) Assert.That(scan.Advance(1), Is.LessThanOrEqualTo(1));
            Assert.That(scan.IsComplete, Is.True);
            Assert.That(scan.SampleCount, Is.EqualTo(3));
            Assert.That(scan.DirectedQueryCount, Is.Zero);
            Assert.That(scan.Anchors[0].MemberSourceId, Is.EqualTo("ground"));
            Assert.That(scan.Anchors[1].IsValid, Is.False);
            Assert.That(scan.Connections[0].Edge.ForwardFailure, Is.EqualTo("MissingAnchor"));
            Assert.That(scan.Connections[0].Edge.ForwardAvailable || scan.Connections[0].Edge.ReverseAvailable, Is.False);
            Assert.That(MapGraphSceneNavigationScan.ConnectionId("A", "B"), Is.EqualTo(MapGraphSceneNavigationScan.ConnectionId("B", "A")));
        }

        [Test]
        public void CandidateSelectionSkipsAValidButDisconnectedNavigationIsland()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TestNavMeshBuilder.Build(_world,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(30, 0.2f, 30)),
                new Bounds(new Vector3(60, -0.1f, 0), new Vector3(20, 0.2f, 20)));
            var profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 1, 0.5f);
            var nodes = new[]
            {
                new MapGraphSceneNode("A", "A-source", "zone", "A", "A", "", null, Vector3.zero, new Rect(), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(Vector3.right * 60, "island"), new MapGraphAnchorCandidate(Vector3.right * 5, "approach") }),
                new MapGraphSceneNode("B", "B-source", "zone", "B", "B", "", null, Vector3.zero, new Rect(), MapGraphNodeKind.Resource,
                    new[] { new MapGraphAnchorCandidate(Vector3.right * 60, "isolated") })
            };
            var snapshot = new MapGraphSceneSnapshot("fixture", "fixture", "scene", "nav", Array.Empty<MapGraphSceneZone>(), nodes,
                new[] { new MapGraphSceneProfile(profile, MapGraphNavigationCostService.CaptureProfile(profile), new[] { "agent" }, new[] { Vector3.zero }) },
                Array.Empty<string>());
            var scan = new MapGraphSceneNavigationScan(snapshot);
            for (int i = 0; !scan.IsComplete && i < 30; i++)
            {
                long before = scan.TotalQueryCount;
                Assert.That(scan.Advance(1), Is.LessThanOrEqualTo(1));
                Assert.That(scan.TotalQueryCount - before, Is.LessThanOrEqualTo(2));
            }
            Assert.That(scan.IsComplete, Is.True);
            Assert.That(scan.Anchors[0].MemberSourceId, Is.EqualTo("approach"));
            Assert.That(scan.Anchors[0].ReachableFromAgentOrigin, Is.True);
            Assert.That(scan.Anchors[1].OriginReachabilityChecked && scan.Anchors[1].IsValid, Is.True);
            Assert.That(scan.Anchors[1].ReachableFromAgentOrigin, Is.False);
            Assert.That(scan.Connections[0].Edge.ForwardAvailable || scan.Connections[0].Edge.ReverseAvailable, Is.False);
            Assert.That(scan.OriginQueryCount, Is.EqualTo(3));
            var spawnObject = _world.Own(new GameObject("Readonly spawn"));
            var spawn = spawnObject.AddComponent<global::EnemySpawnPoint>();
            var random = UnityEngine.Random.state;
            Assert.That(spawn.TryGetNavigationGroundCandidate(out var ground), Is.True);
            Assert.That(Mathf.Abs(ground.y), Is.LessThan(0.25f), "Allow the baked fixture's vertical voxel quantization.");
            Assert.That(spawn.HasSpawned, Is.False);
            Assert.That(spawn.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
        }

        [Serializable] private sealed class CandidateEvidence { public Vector3 position; public string member, derivation; }
        [Serializable] private sealed class NodeEvidence
        {
            public string id, source, zone, name, hierarchy, prefab, kind;
            public Vector3 center; public Rect bounds;
            public List<CandidateEvidence> candidates = new List<CandidateEvidence>();
        }
        [Serializable] private sealed class ZoneEvidence { public string id, source, name; public Rect bounds; }
        [Serializable] private sealed class AnchorEvidence
        { public string profile, node, member, failure; public bool valid, originChecked, reachableFromOrigin; public Vector3 position; public int samples; }
        [Serializable] private sealed class OriginEvidence { public string profile, agent; public Vector3 position; }
        [Serializable] private sealed class ConnectionEvidence { public string profile; public MapGraphNavigationEdgeBake edge; }
        [Serializable] private sealed class SurfaceEvidence { public string collider; public Vector3 point, normal; }
        [Serializable] private sealed class NeighborhoodEvidence
        {
            public string node; public Vector3 candidate, nearestNavigation; public bool navigationFound;
            public List<SurfaceEvidence> surfaces = new List<SurfaceEvidence>();
        }
        [Serializable] private sealed class Evidence
        {
            public string scene, sceneFingerprint, navigationFingerprint;
            public bool complete; public long directedQueries, originQueries; public int samples; public double milliseconds;
            public List<string> diagnostics;
            public List<MapGraphNavigationProfileData> profiles;
            public List<OriginEvidence> origins = new List<OriginEvidence>();
            public List<ZoneEvidence> zones = new List<ZoneEvidence>();
            public List<NodeEvidence> nodes = new List<NodeEvidence>();
            public List<AnchorEvidence> anchors = new List<AnchorEvidence>();
            public List<ConnectionEvidence> connections = new List<ConnectionEvidence>();
            public List<NeighborhoodEvidence> failedNeighborhoods = new List<NeighborhoodEvidence>();
        }

        private static void WriteEvidence(MapGraphSceneSnapshot scene, MapGraphSceneNavigationScan scan, string file)
        {
            var result = new Evidence
            {
                scene = scene.ScenePath, sceneFingerprint = scene.SceneFingerprint, navigationFingerprint = scene.NavigationFingerprint,
                diagnostics = scene.Diagnostics.ToList(), profiles = scene.Profiles.Select(p => p.Data).ToList()
            };
            foreach (var zone in scene.Zones) result.zones.Add(new ZoneEvidence { id = zone.Id, source = zone.SourceObjectId, name = zone.Name, bounds = zone.WorldBounds });
            foreach (var profile in scene.Profiles)
                for (int i = 0; i < profile.AgentOrigins.Count; i++)
                    result.origins.Add(new OriginEvidence { profile = profile.Data.ProfileId, agent = profile.AgentSourceIds[i], position = profile.AgentOrigins[i] });
            foreach (var node in scene.Nodes)
            {
                var item = new NodeEvidence { id = node.Id, source = node.SourceObjectId, zone = node.ZoneId, name = node.Name,
                    hierarchy = node.HierarchyPath, prefab = node.PrefabPath, kind = node.Kind.ToString(), center = node.WorldCenter, bounds = node.WorldBounds };
                foreach (var candidate in node.Candidates) item.candidates.Add(new CandidateEvidence { position = candidate.Position, member = candidate.MemberSourceId, derivation = candidate.Derivation });
                result.nodes.Add(item);
            }
            if (scan != null)
            {
                result.complete = scan.IsComplete; result.directedQueries = scan.DirectedQueryCount; result.originQueries = scan.OriginQueryCount;
                result.samples = scan.SampleCount; result.milliseconds = scan.ElapsedMilliseconds;
                foreach (var anchor in scan.Anchors) result.anchors.Add(new AnchorEvidence { profile = anchor.ProfileId, node = anchor.NodeId,
                    member = anchor.MemberSourceId, failure = anchor.Failure, valid = anchor.IsValid, position = anchor.Position, samples = anchor.Samples,
                    originChecked = anchor.OriginReachabilityChecked, reachableFromOrigin = anchor.ReachableFromAgentOrigin });
                foreach (var connection in scan.Connections) result.connections.Add(new ConnectionEvidence { profile = connection.ProfileId, edge = connection.Edge });
                // 宽半径只用于失败证据，不能作为正式锚点或连接接受条件。
                foreach (var anchor in scan.Anchors.Where(a => !a.IsValid))
                {
                    var node = scene.Nodes.First(n => n.Id == anchor.NodeId);
                    foreach (var candidate in node.Candidates)
                    {
                        var item = new NeighborhoodEvidence { node = node.Id, candidate = candidate.Position };
                        item.navigationFound = NavMesh.SamplePosition(candidate.Position, out var hit, 32, NavMesh.AllAreas);
                        if (item.navigationFound) item.nearestNavigation = hit.position;
                        foreach (var surface in Physics.RaycastAll(candidate.Position + Vector3.up * 20, Vector3.down, 80,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                            item.surfaces.Add(new SurfaceEvidence { collider = surface.collider.name, point = surface.point, normal = surface.normal });
                        result.failedNeighborhoods.Add(item);
                    }
                }
            }
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath, file), JsonUtility.ToJson(result, true));
            CaseArtifactWriter.Trace("scene-map-evidence", file + "; nodes=" + result.nodes.Count + "; samples=" + result.samples + "; queries=" + result.directedQueries);
        }
    }
}
