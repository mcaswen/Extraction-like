using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidCommandHarnessTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator CatalogCaptureDoesNotChangeCommandsNavigationOrTargetState()
        {
            TestNavMeshBuilder.Flat(World);
            var pawn = AgentFactory.Create(World, "Catalog", Vector3.zero);
            var resource = TargetFactory.Resources(World, new Vector3(8, 0, 0), new Vector3(14, 0, 0));
            var exit = TargetFactory.Extraction(World, new Vector3(18, 0, 0));
            var enemy = EnemyFactory.Passive(World, new Vector3(0, 0, 10));
            TargetFactory.Enemies(World, enemy);
            World.Root("Excluded source").AddComponent<EnemySourceClusterAuthoring>();
            yield return null;
            var nav = pawn.NavMeshAgent;
            nav.SetDestination(new Vector3(4, 0, 0));
            yield return null;
            var position = pawn.Position; var destination = nav.destination; var corners = nav.path.corners;
            var focus = AgentRuntimeRegistry.ActiveInstance.FocusedAgentId;
            int registered = GameplayTargetRegistry.ActiveInstance.ClusterCount;
            bool touched = resource.HasBeenTouched, completed = resource.HasBeenCompleted;
            var command = pawn.DirectiveLifecycle.Active;
            var catalog = new SceneRaidClusterCatalog(new SceneRaidIdentityMap());
            var first = catalog.Capture(); var second = catalog.Capture();
            Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
            // 来源群启用时还会创建配套活跃群；只排除来源群本身，不能丢掉合法的空活跃群。
            Assert.That(first.clusters.Count, Is.EqualTo(registered - 1));
            Assert.That(first.clusters.Any(x => x.kind == "EnemySource"), Is.False);
            Assert.That(first.clusters.Single(x => x.kind == "Resource").members.Count, Is.EqualTo(2));
            Assert.That(pawn.Position, Is.EqualTo(position));
            Assert.That(nav.destination, Is.EqualTo(destination));
            Assert.That(nav.path.corners, Is.EqualTo(corners));
            Assert.That(pawn.DirectiveLifecycle.Active, Is.EqualTo(command));
            Assert.That(AgentRuntimeRegistry.ActiveInstance.FocusedAgentId, Is.EqualTo(focus));
            Assert.That(GameplayTargetRegistry.ActiveInstance.ClusterCount, Is.EqualTo(registered));
            Assert.That(resource.HasBeenTouched, Is.EqualTo(touched));
            Assert.That(resource.HasBeenCompleted, Is.EqualTo(completed));
            Assert.That(resource.ResourceMembers.All(x => !x.HasBeenCompleted), Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CatalogSeparatesDistanceFromReachability()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20)),
                new Bounds(new Vector3(100, -0.1f, 0), new Vector3(20, 0.2f, 20)));
            AgentFactory.Create(World, "Catalog", Vector3.zero);
            TargetFactory.Extraction(World, new Vector3(100, 0, 0));
            yield return null;
            var capture = new SceneRaidClusterCatalog(new SceneRaidIdentityMap()).Capture();
            var approach = capture.clusters.Single().members.Single().approaches.Single();
            Assert.That(approach.sampled, Is.True);
            Assert.That(approach.completePath, Is.False);
            Assert.That(approach.distancePlanar, Is.EqualTo(100).Within(0.1));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(10, 30, 2), Is.EqualTo("Near"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(60, 30, 2), Is.EqualTo("Far"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(45, 30, 2), Is.EqualTo("Middle"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(1, 30, 2), Is.EqualTo("Interaction"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(10, 0, 2), Is.EqualTo("UndefinedRange"));
            ContractCompleted = true;
        }
    }
}
