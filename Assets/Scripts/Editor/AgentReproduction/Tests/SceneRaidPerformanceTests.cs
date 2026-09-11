using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Agent.Targeting;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidPerformanceTests : ReproductionTestFixture
    {
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
