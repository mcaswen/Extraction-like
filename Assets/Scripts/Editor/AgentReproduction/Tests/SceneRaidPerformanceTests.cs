using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Agent.Targeting;
using Gameplay.Agent.Navigation;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidPerformanceTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator StaticRangesStayCachedAndMemberChangesRefreshZone()
        {
            TestNavMeshBuilder.Flat(World);
            var zone = World.Root("Cached zone").AddComponent<TargetZoneAuthoring>();
            var line = zone.gameObject.AddComponent<LineRenderer>();
            RuntimeFixtureAccess.Configure(zone, "_rangeLineRenderer", line);
            var cluster = TargetFactory.Resources(World, new Vector3(3, 0, 0));
            RuntimeFixtureAccess.Configure(cluster, "_zone", zone);
            zone.RegisterCluster(cluster);
            for (int i = 0; i < 3; i++) yield return null;
            long zoneBuild = zone.RangeGeometryBuildCount, memberBuild = cluster.RangeGeometryBuildCount;
            long lineWrites = zone.RangeLineWriteCount;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(zone.RangeGeometryBuildCount, Is.EqualTo(zoneBuild));
            Assert.That(cluster.RangeGeometryBuildCount, Is.EqualTo(memberBuild));
            Assert.That(zone.RangeLineWriteCount, Is.EqualTo(lineWrites), "Identical projected points do not rewrite the line.");
            cluster.ResourceMembers[0].EntityObject.transform.position = new Vector3(9, 0, 0);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(cluster.RangeGeometryBuildCount, Is.GreaterThan(memberBuild));
            Assert.That(zone.RangeGeometryBuildCount, Is.GreaterThan(zoneBuild));
            Assert.That(zone.CenterPosition.x, Is.EqualTo(9).Within(0.01));
            var added = TargetFactory.Resources(World, new Vector3(21, 0, 0));
            RuntimeFixtureAccess.Configure(added, "_zone", zone);
            zone.RegisterCluster(added);
            Assert.That(zone.CenterPosition.x, Is.GreaterThan(12));
            zone.UnregisterCluster(added);
            Assert.That(zone.CenterPosition.x, Is.EqualTo(9).Within(0.01));
            Assert.That(line.positionCount, Is.EqualTo(zone.RangePoints.Count));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ColliderChangesRefreshRangeAndGroundChangesReproject()
        {
            var ground = World.Cube("Movable ground", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
            var shape = World.Root("Range collider").AddComponent<BoxCollider>();
            shape.isTrigger = true; shape.size = new Vector3(8, 1, 8);
            var zone = World.Root("Collider zone").AddComponent<TargetZoneAuthoring>();
            RuntimeFixtureAccess.Configure(zone, "_rangeColliderTarget", shape.gameObject);
            zone.RefreshRangeShape();
            yield return null;
            long count = zone.RangeGeometryBuildCount;
            shape.transform.position = new Vector3(6, 0, 0);
            shape.transform.rotation = Quaternion.Euler(0, 30, 0);
            shape.size = new Vector3(12, 1, 8);
            Physics.SyncTransforms();
            for (int i = 0; i < 2; i++) yield return null;
            Assert.That(zone.RangeGeometryBuildCount, Is.GreaterThan(count));
            Assert.That(zone.CenterPosition.x, Is.EqualTo(6).Within(0.01));
            Vector3 corner = shape.transform.TransformPoint(new Vector3(-6, 0, -4));
            Assert.That(zone.RangePoints[0].x, Is.EqualTo(corner.x).Within(0.01));
            count = zone.RangeGeometryBuildCount;
            long projections = zone.RangeGroundProjectionCount;
            ground.transform.position += Vector3.up * 2;
            Physics.SyncTransforms();
            yield return RuntimeWait.Until(() => zone.RangePoints[0].y > 2, "ground cache invalidation", 1);
            Assert.That(zone.RangeGeometryBuildCount, Is.EqualTo(count), "Ground changes reuse unprojected geometry.");
            Assert.That(zone.RangeGroundProjectionCount, Is.GreaterThan(projections));
            Assert.That(zone.RangePoints[0].y, Is.EqualTo(2.08f).Within(0.01));
            shape.transform.localScale = new Vector3(2, 1, 1);
            for (int i = 0; i < 2; i++) yield return null;
            corner = shape.transform.TransformPoint(new Vector3(-6, 0, -4));
            Assert.That(zone.RangePoints[0].x, Is.EqualTo(corner.x).Within(0.01));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator MovementQueriesAreBoundedAndBuffersAreIndependent()
        {
            TestNavMeshBuilder.Flat(World);
            var a = AgentFactory.Create(World, "Motor A", Vector3.zero);
            var b = AgentFactory.Create(World, "Motor B", new Vector3(0, 0, 8));
            yield return null;
            var first = new AgentNavigationMotor(a.NavMeshAgent, 2, 3);
            var second = new AgentNavigationMotor(b.NavMeshAgent, 2, 3);
            var target = new Vector3(20, 0, 0);
            var result = first.Move("A", target, 0, 2);
            Assert.That(result.Status, Is.EqualTo(AgentNavigationStatus.Moving));
            for (int i = 0; i < 100; i++) first.Move("A", target, 0, 2);
            Assert.That(first.PathCalculationCount, Is.EqualTo(1), "An unchanged same-frame move reuses its complete path.");
            Vector3 end = result.Path.corners[result.Path.corners.Length - 1];
            var other = second.Move("B", new Vector3(-20, 0, 8), 0, 2);
            Assert.That(other.Path, Is.Not.SameAs(result.Path));
            Assert.That(result.Path.corners[result.Path.corners.Length - 1], Is.EqualTo(end));
            first.Move("A", new Vector3(20, 0, 5), 0, 2);
            Assert.That(first.PathCalculationCount, Is.EqualTo(2), "A changed target invalidates immediately.");
            float start = Time.time;
            long count = first.PathCalculationCount;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                first.Move("A", new Vector3(20, 0, 5), 0, 2);
            }
            Assert.That(first.PathCalculationCount - count, Is.LessThanOrEqualTo(Mathf.CeilToInt((Time.time - start) / 0.1f) + 1));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ResourceQueryCacheInvalidatesOnCompletionAndMovement()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Resource cache", Vector3.zero);
            var cluster = TargetFactory.Resources(World, new Vector3(5, 0, 0), new Vector3(14, 0, 0));
            yield return null;
            var resolver = new AgentResourceNavigationResolver();
            Assert.That(resolver.TryResolve(cluster, agent.Position, agent.NavMeshAgent, out var selected, out _), Is.True);
            Assert.That(selected, Is.SameAs(cluster.ResourceMembers[0].EntityObject));
            long count = cluster.NavigationPathCalculationCount;
            for (int i = 0; i < 100; i++) resolver.TryResolve(cluster, agent.Position, agent.NavMeshAgent, out _, out _);
            Assert.That(cluster.NavigationPathCalculationCount, Is.EqualTo(count));
            cluster.MarkResourceCompleted(selected);
            Assert.That(resolver.TryResolve(cluster, agent.Position, agent.NavMeshAgent, out selected, out _), Is.True);
            Assert.That(selected, Is.SameAs(cluster.ResourceMembers[1].EntityObject));
            selected.transform.position = new Vector3(32, 0, 0);
            Assert.That(resolver.TryResolve(cluster, agent.Position, agent.NavMeshAgent, out _, out var destination), Is.True);
            Assert.That(destination.x, Is.GreaterThan(30), "Cached candidate coordinates must move with the resource.");
            selected.SetActive(false);
            Assert.That(resolver.TryResolve(cluster, agent.Position, agent.NavMeshAgent, out _, out _), Is.False);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator OutOfRangeMembersDoNotCalculatePaths()
        {
            TestNavMeshBuilder.Flat(World, 300);
            var agent = AgentFactory.Create(World, "Far resources", Vector3.zero);
            var positions = new Vector3[64];
            for (int i = 0; i < positions.Length; i++) positions[i] = new Vector3(40 + i, 0, 0);
            var cluster = TargetFactory.Resources(World, positions);
            yield return null;
            var collector = new AgentTargetCandidateCollector();
            var candidates = new List<AgentTargetCandidate>();
            var clusters = new GameplayTargetClusterAuthoringBase[] { cluster };
            long before = cluster.NavigationPathCalculationCount;
            for (int i = 0; i < 10; i++) collector.CollectWorldTargets(agent, clusters, 10, candidates);
            Assert.That(candidates, Is.Empty);
            Assert.That(cluster.NavigationPathCalculationCount - before, Is.Zero,
                "Repeated scans must not pathfind to any of the 64 out-of-range members.");
            Assert.That(cluster.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _, out _), Is.True,
                "The unbounded action query must still reach distant members.");
            Assert.That(cluster.NavigationPathCalculationCount, Is.GreaterThan(before));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DiscoveryRangeUsesMembersInsteadOfClusterCenter()
        {
            TestNavMeshBuilder.Flat(World, 250);
            var agent = AgentFactory.Create(World, "Mixed cluster", Vector3.zero);
            var mixed = TargetFactory.Resources(World, new Vector3(3, 0, 0), new Vector3(80, 0, 0));
            var nearOnly = TargetFactory.Resources(World, new Vector3(3, 0, 0));
            yield return null;
            Assert.That(Vector3.Distance(mixed.CenterPosition, agent.Position), Is.GreaterThan(10));
            long mixedBefore = mixed.NavigationPathCalculationCount;
            long nearBefore = nearOnly.NavigationPathCalculationCount;
            var collector = new AgentTargetCandidateCollector();
            var candidates = new List<AgentTargetCandidate>();
            collector.CollectWorldTargets(agent, new GameplayTargetClusterAuthoringBase[] { mixed }, 10, candidates);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].Member, Is.SameAs(mixed.ResourceMembers[0].EntityObject));
            collector.CollectWorldTargets(agent, new GameplayTargetClusterAuthoringBase[] { nearOnly }, 10, candidates);
            Assert.That(mixed.NavigationPathCalculationCount - mixedBefore,
                Is.EqualTo(nearOnly.NavigationPathCalculationCount - nearBefore), "Adding a distant member adds zero path queries.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator UnreachableNearMemberDoesNotHideReachableMember()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(80, 0.2f, 80)),
                new Bounds(new Vector3(0, 7.9f, 4), new Vector3(6, 0.2f, 6)));
            var agent = AgentFactory.Create(World, "Member fallback", Vector3.zero);
            var cluster = TargetFactory.Resources(World, new Vector3(0, 8, 4), new Vector3(12, 0, 0));
            yield return null;
            var collector = new AgentTargetCandidateCollector();
            var candidates = new List<AgentTargetCandidate>();
            collector.CollectWorldTargets(agent, new GameplayTargetClusterAuthoringBase[] { cluster }, 20, candidates);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].Member, Is.SameAs(cluster.ResourceMembers[1].EntityObject));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ExtractionCollectionRemainsAvailableBeyondDiscoveryRange()
        {
            TestNavMeshBuilder.Flat(World, 150);
            var agent = AgentFactory.Create(World, "Exit fallback", Vector3.zero);
            var resource = TargetFactory.Resources(World, new Vector3(3, 0, 0));
            var exit = TargetFactory.Extraction(World, new Vector3(50, 0, 0));
            yield return null;
            var clusters = new GameplayTargetClusterAuthoringBase[] { resource, exit };
            var collector = new AgentTargetCandidateCollector();
            var candidates = new List<AgentTargetCandidate>();
            collector.CollectWorldTargets(agent, clusters, 10, candidates, false, false);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].Kind, Is.EqualTo(AgentTargetKind.Resource));
            collector.CollectExtractionTargets(agent, clusters, candidates);
            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].Member, Is.SameAs(exit.ExtractionMembers[0].EntityObject));
            collector.CollectWorldTargets(agent, clusters, 10, candidates);
            Assert.That(candidates.Count, Is.EqualTo(2), "Decision's default collector retains both kinds.");
            ContractCompleted = true;
        }
    }
}
