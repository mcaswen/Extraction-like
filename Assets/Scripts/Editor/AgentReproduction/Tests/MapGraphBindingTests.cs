using System;
using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphBindingTests : ReproductionTestFixture
    {
        private SO_MapGraphDefinition _definition;
        private MapGraphBindingAuthoring _binding;
        private AgentNavigationProfile _profile;
        private ResourceClusterAuthoring[] _clusters;

        private void Build(int count = 2, bool buildNavigation = true, float spacing = 10)
        {
            if (buildNavigation) TestNavMeshBuilder.Flat(World);
            _profile = new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, -1, 1, 0.5f);
            var zone = World.Root("Zone").AddComponent<TargetZoneAuthoring>();
            var nodes = new List<MapGraphNodeDefinition>();
            var targets = new List<MapGraphTargetBinding>();
            _clusters = new ResourceClusterAuthoring[count];
            for (int i = 0; i < count; i++)
            {
                string id = ((char)('A' + i)).ToString();
                var root = World.Root(id, false);
                root.transform.SetParent(zone.transform);
                root.transform.position = Vector3.right * (i * spacing);
                var cluster = root.AddComponent<ResourceClusterAuthoring>();
                root.SetActive(true); _clusters[i] = cluster;
                nodes.Add(new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, Vector2.right * (i * 80),
                    zoneId: "zone", sourceObjectId: id + "-source"));
                targets.Add(new MapGraphTargetBinding(id, cluster, root.transform.position, id + "-source"));
            }
            var edges = new List<MapGraphEdgeDefinition>();
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    edges.Add(new MapGraphEdgeDefinition(nodes[i].NodeId + nodes[j].NodeId, nodes[i].NodeId, nodes[j].NodeId,
                        999, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Generated));
            _definition = World.Own(ScriptableObject.CreateInstance<SO_MapGraphDefinition>());
            _definition.ApplyCommandData("map", "map", "A",
                new[] { new MapGraphZoneDefinition("zone", "Zone", new Rect(-50, -100, 400, 200), new Vector2(80, 20)) },
                nodes, edges, new MapGraphLayoutConstraints(), new MapGraphNavigationBakeData());
            _binding = World.Root("Binding").AddComponent<MapGraphBindingAuthoring>();
            _binding.Configure(_definition, targets, new[] { new MapGraphZoneBinding("zone", zone) });
            Assert.That(_binding.IsValid, Is.True, string.Join(";", _binding.ValidationErrors));
        }

        private MapGraphNavigationCostService Service(string scene = "scene", string navigation = "navigation",
            AgentNavigationProfile profile = null)
            => new MapGraphNavigationCostService(_definition, _binding, profile ?? _profile, scene, navigation);

        private void SaveBake(MapGraphNavigationBakeData bake)
        {
            _definition.ApplyCommandData(_definition.MapId, _definition.DisplayName, _definition.StartNodeId,
                _definition.Zones, _definition.Nodes, _definition.Edges, _definition.LayoutConstraints, bake);
            _binding.RebuildIndexes();
        }

        [UnityTest]
        public IEnumerator DirectReferencesDistinguishDuplicateRuntimeIds()
        {
            Build();
            RuntimeFixtureAccess.Configure(_clusters[0], "_targetId", "duplicate");
            RuntimeFixtureAccess.Configure(_clusters[1], "_targetId", "duplicate");
            _binding.RebuildIndexes();
            Assert.That(_binding.IsValid, Is.True);
            Assert.That(_binding.TryGetNodeIdForDirectTarget(_clusters[0], out var a), Is.True);
            Assert.That(_binding.TryGetNodeIdForDirectTarget(_clusters[1], out var b), Is.True);
            Assert.That(a, Is.EqualTo("A")); Assert.That(b, Is.EqualTo("B"));
            Assert.That(_binding.TryGetNodeIdForTargetId("duplicate", out _), Is.False, "Ambiguous legacy IDs must be refused.");
            RuntimeFixtureAccess.Configure(_clusters[1], "_targetId", "regenerated");
            Assert.That(_binding.TryGetNodeIdForTargetId("regenerated", out b), Is.True);
            Assert.That(b, Is.EqualTo("B"));
            Assert.That(_binding.TryGetNodeIdForTargetObject(_clusters[0].gameObject, out a), Is.True);
            Assert.That(a, Is.EqualTo("A"));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator DuplicateOrOrphanBindingsAreDiagnosedAndNotQueryable()
        {
            Build();
            var targets = new List<MapGraphTargetBinding>(_binding.TargetBindings);
            targets.Add(new MapGraphTargetBinding("A", _clusters[1], Vector3.right * 10));
            targets.Add(new MapGraphTargetBinding("orphan", _clusters[1], Vector3.right * 10));
            _binding.Configure(_definition, targets, _binding.ZoneBindings);
            Assert.That(_binding.IsValid, Is.False);
            Assert.That(_binding.ValidationErrors, Does.Contain("DuplicateNodeBinding:A"));
            Assert.That(_binding.ValidationErrors, Does.Contain("OrphanNodeBinding:orphan"));
            Assert.That(_binding.ValidationErrors, Does.Contain("DuplicateTargetBinding:orphan"));
            Assert.That(_binding.TryGetBinding("A", out _), Is.False);
            Assert.Throws<ArgumentException>(() => Service());
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ZoneReferencesAndBindingCollectionsHaveClearOwnership()
        {
            Build();
            var targets = new List<MapGraphTargetBinding>(_binding.TargetBindings);
            var zones = new List<MapGraphZoneBinding>(_binding.ZoneBindings);
            _binding.Configure(_definition, targets, zones); targets.Clear(); zones.Clear();
            Assert.That(_binding.TargetBindings.Count, Is.EqualTo(2));
            Assert.That(_binding.TryGetZone("zone", out var zone), Is.True);
            Assert.That(zone, Is.EqualTo(_clusters[0].Zone));
            Assert.Throws<NotSupportedException>(() => ((IList<MapGraphTargetBinding>)_binding.TargetBindings).Clear());
            _binding.Configure(_definition, _binding.TargetBindings, Array.Empty<MapGraphZoneBinding>());
            Assert.That(_binding.ValidationErrors, Does.Contain("MissingZoneBinding:zone"));
            Assert.That(_binding.ValidationErrors, Does.Contain("ClusterZoneMismatch:A"));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator MatchingBakeLoadsWithoutAnyNavigationQueries()
        {
            Build();
            var service = Service(); service.ProcessPending(1);
            SaveBake(service.CreateBakeData("scene", "navigation"));
            string before = JsonUtility.ToJson(_definition.NavigationBake);
            var loaded = Service();
            Assert.That(loaded.CalculationCount, Is.Zero);
            Assert.That(loaded.PendingEdgeCount, Is.Zero);
            Assert.That(loaded.Snapshot.TryGetCost(_definition.Edges[0], "A", out float cost), Is.True);
            Assert.That(cost, Is.EqualTo(10).Within(0.05f));
            Assert.That(loaded.RefreshChangedAnchors(100), Is.Zero);
            Assert.That(loaded.ProcessPending(100), Is.Zero);
            Assert.That(loaded.CalculationCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(_definition.NavigationBake), Is.EqualTo(before));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ContextAndProfileChangesInvalidateCachedCosts()
        {
            Build(); var service = Service(); service.ProcessPending(1);
            SaveBake(service.CreateBakeData("scene", "navigation"));
            var costs = new float[32]; for (int i = 0; i < 32; i++) costs[i] = 1; costs[3] = 2;
            var profiles = new[]
            {
                new AgentNavigationProfile(_profile.AgentTypeId, 1, 1, 0.5f),
                new AgentNavigationProfile(_profile.AgentTypeId, -1, 2, 0.5f),
                new AgentNavigationProfile(_profile.AgentTypeId, -1, 1, 0.6f),
                new AgentNavigationProfile(_profile.AgentTypeId, -1, 1, 0.5f, costs)
            };
            foreach (var profile in profiles)
            {
                var changed = Service(profile: profile);
                Assert.That(changed.PendingEdgeCount, Is.EqualTo(1));
                Assert.That(changed.Snapshot.Count, Is.Zero);
                Assert.That(changed.CalculationCount, Is.Zero);
            }
            Assert.That(Service(scene: "changed").Snapshot.Count, Is.Zero);
            Assert.That(Service(navigation: "changed").Snapshot.Count, Is.Zero);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator RecalculationHasABoundedQueueAndImmutableSnapshots()
        {
            Build(3); var service = Service();
            Assert.That(service.PendingEdgeCount, Is.EqualTo(3));
            Assert.That(service.ProcessPending(0), Is.Zero);
            Assert.That(service.ProcessPending(1), Is.EqualTo(1));
            Assert.That(service.CalculationCount, Is.EqualTo(2));
            Assert.That(service.PendingEdgeCount, Is.EqualTo(2));
            var first = service.Snapshot;
            Assert.That(first.Count, Is.EqualTo(1));
            service.ProcessPending(100);
            Assert.That(service.CalculationCount, Is.EqualTo(6));
            Assert.That(service.Snapshot.Count, Is.EqualTo(3));
            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(service.Snapshot.Revision, Is.GreaterThan(first.Revision));
            service.InvalidateEdge("AB", "test"); service.InvalidateEdge("AB", "repeat");
            Assert.That(service.PendingEdgeCount, Is.EqualTo(1));
            Assert.That(service.Snapshot.TryGetCost(_definition.Edges[0], "A", out _), Is.False);
            Assert.That(first.TryGetCost(_definition.Edges[0], "A", out _), Is.True);
            service.InvalidateAll("navigation rebuilt");
            Assert.That(service.PendingEdgeCount, Is.EqualTo(3));
            Assert.That(service.Snapshot.Count, Is.Zero);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator AnchorMovementInvalidatesCostsButCompletionKeepsAnchor()
        {
            Build(); var service = Service(); service.ProcessPending(1);
            Assert.That(_binding.TryGetNavigationAnchor("A", out var initial), Is.True);
            _clusters[0].MarkCompleted();
            Assert.That(_binding.TryGetNavigationAnchor("A", out var completed), Is.True);
            Assert.That(completed, Is.EqualTo(initial));
            Assert.That(service.RefreshChangedAnchors(1), Is.Zero);
            _clusters[0].transform.position += Vector3.right * 2;
            Assert.That(service.RefreshChangedAnchors(1), Is.EqualTo(1));
            Assert.That(service.Snapshot.Count, Is.Zero);
            long before = service.CalculationCount;
            service.ProcessPending(1);
            Assert.That(service.CalculationCount - before, Is.EqualTo(2));
            Assert.That(service.Snapshot.TryGetCost(_definition.Edges[0], "A", out float cost), Is.True);
            Assert.That(cost, Is.EqualTo(8).Within(0.05f));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator PartialOrOneWayMeasurementsNeverBecomeBidirectionalEdges()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12)),
                new Bounds(new Vector3(30, -0.1f, 0), new Vector3(12, 0.2f, 12)));
            Build(buildNavigation: false, spacing: 30);
            var service = Service(); service.ProcessPending(1);
            Assert.That(service.TryGetMeasurement("AB", out var measurement), Is.True);
            Assert.That(measurement.ForwardFailure, Is.EqualTo("PathPartial"));
            Assert.That(service.Snapshot.Count, Is.Zero);
            SaveBake(new MapGraphNavigationBakeData("scene", "navigation", 1,
                MapGraphNavigationCostService.CaptureProfile(_profile),
                new[] { new MapGraphNavigationEdgeBake("AB", "A", "B", Vector3.zero, Vector3.right * 30,
                    30, float.PositiveInfinity, reverseFailure: "PathPartial") }));
            var oneWay = Service();
            Assert.That(oneWay.PendingEdgeCount, Is.Zero);
            Assert.That(oneWay.CalculationCount, Is.Zero);
            Assert.That(oneWay.Snapshot.Count, Is.Zero);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ReplacedGraphOrBindingCannotReuseAnOldCostService()
        {
            Build(); var service = Service(); service.ProcessPending(1);
            var frozen = service.Snapshot;
            SaveBake(service.CreateBakeData("scene", "navigation"));
            Assert.That(service.RequiresRebuild, Is.True);
            Assert.That(service.Snapshot.Count, Is.Zero);
            Assert.That(service.PendingEdgeCount, Is.Zero);
            Assert.That(service.ProcessPending(100), Is.Zero);
            Assert.That(frozen.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => service.CreateBakeData("scene", "navigation"));
            var replacement = Service();
            Assert.That(replacement.Snapshot.Count, Is.EqualTo(1));
            Assert.That(replacement.CalculationCount, Is.Zero);
            _binding.Configure(_definition, _binding.TargetBindings, _binding.ZoneBindings);
            Assert.That(replacement.RequiresRebuild, Is.True);
            Assert.That(replacement.Snapshot.Count, Is.Zero);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator SourceIdentityAndRepeatedZoneReferencesAreRejected()
        {
            Build();
            var targets = new List<MapGraphTargetBinding>(_binding.TargetBindings);
            targets[0] = new MapGraphTargetBinding("A", _clusters[0], Vector3.zero, "wrong-source");
            var zones = new List<MapGraphZoneBinding>(_binding.ZoneBindings);
            zones.Add(new MapGraphZoneBinding("duplicate-zone-object", _clusters[0].Zone));
            _binding.Configure(_definition, targets, zones);
            Assert.That(_binding.ValidationErrors, Does.Contain("SourceIdentityMismatch:A"));
            Assert.That(_binding.ValidationErrors, Does.Contain("DuplicateZoneTarget:duplicate-zone-object"));
            Assert.That(_binding.TryGetNodeIdForDirectTarget(_clusters[0], out _), Is.False);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator LostBindingFailsWithoutInventingAFallbackPath()
        {
            Build(); var service = Service(); service.ProcessPending(1);
            UnityEngine.Object.DestroyImmediate(_clusters[1].gameObject);
            Assert.That(service.RefreshChangedAnchors(1), Is.EqualTo(1));
            long before = service.CalculationCount;
            service.ProcessPending(1);
            Assert.That(service.CalculationCount, Is.EqualTo(before));
            Assert.That(service.Snapshot.Count, Is.Zero);
            Assert.That(service.TryGetMeasurement("AB", out var failed), Is.True);
            Assert.That(failed.ForwardFailure, Is.EqualTo("MissingNavigationAnchor"));
            Assert.That(_binding.TryResolveTargetForNodeId("B", out _), Is.False);
            ContractCompleted = true;
            yield break;
        }
    }
}
