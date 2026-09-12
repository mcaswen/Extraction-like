using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphRouteTargetTests : ReproductionTestFixture
    {
        private MapGraphBindingAuthoring Bind(params GameplayTargetClusterAuthoringBase[] clusters)
        {
            var nodes = new List<MapGraphNodeDefinition>(); var bindings = new List<MapGraphTargetBinding>();
            for (int i = 0; i < clusters.Length; i++)
            {
                string id = "n" + i;
                var kind = clusters[i] is ResourceClusterAuthoring ? MapGraphNodeKind.Resource :
                    clusters[i] is ExtractionClusterAuthoring ? MapGraphNodeKind.Extraction :
                    clusters[i] is EnemySourceClusterAuthoring ? MapGraphNodeKind.EnemySource : MapGraphNodeKind.ActiveEnemy;
                nodes.Add(new MapGraphNodeDefinition(id, kind, Vector2.right * i * 80, zoneId: "unassigned"));
                bindings.Add(new MapGraphTargetBinding(id, clusters[i], clusters[i].transform.position));
            }
            var definition = World.Own(ScriptableObject.CreateInstance<SO_MapGraphDefinition>());
            definition.ApplyCommandData("test", "test", "", new[] {
                new MapGraphZoneDefinition("unassigned", "未分区", new Rect(-50,-100,500,200), new Vector2(80,20), isSynthetic: true)
            }, nodes, new MapGraphEdgeDefinition[0], new MapGraphLayoutConstraints(), new MapGraphNavigationBakeData());
            var binding = World.Root("Binding").AddComponent<MapGraphBindingAuthoring>();
            binding.Configure(definition, bindings, new[] { new MapGraphZoneBinding("unassigned", null, isSynthetic: true) });
            Assert.That(binding.IsValid, Is.True, string.Join(";", binding.ValidationErrors));
            return binding;
        }

        private EnemySourceClusterAuthoring Source(GameObject prefab, out EnemySpawnPoint point, float x = 10, bool createActive = true)
        {
            var root = World.Root("Source", false); root.transform.position = Vector3.right * x;
            var source = root.AddComponent<EnemySourceClusterAuthoring>();
            if (!createActive) RuntimeFixtureAccess.Configure(source, "_autoCreateActiveEnemyCluster", false);
            var child = new GameObject("Spawn"); child.transform.SetParent(root.transform, false);
            point = child.AddComponent<EnemySpawnPoint>(); RuntimeFixtureAccess.Configure(point, "_enemyPrefab", prefab);
            root.SetActive(true); return source;
        }
        private static AgentRouteTargetFacts Facts(MapGraphRouteTargetResolver resolver, string node = "n0")
        { Assert.That(resolver.TryGetFacts(node, out var facts), Is.True); return facts; }

        [UnityTest]
        public IEnumerator UnstartedSourceCannotCompleteAndFailedAttemptStaysDistinct()
        {
            TestNavMeshBuilder.Flat(World);
            var source = Source(null, out var point); var resolver = new MapGraphRouteTargetResolver(Bind(source));
            Assert.That(point.HasSpawned, Is.False);
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.WaitingSpawn));
            Assert.That(point.Spawn(), Is.Null);
            Assert.That(point.HasSpawned, Is.True); Assert.That(point.SpawnState, Is.EqualTo(EnemySpawnState.Failed));
            Assert.That(point.SpawnFailure, Is.EqualTo("MissingPrefab"));
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.SpawnFailed));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator SuccessfulSpawnRegistrationAndDeathProduceAuthoritativeFacts()
        {
            TestNavMeshBuilder.Flat(World);
            var prototype = EnemyFactory.Passive(World, new Vector3(-20,0,10));
            var source = Source(prototype.gameObject, out var point); var resolver = new MapGraphRouteTargetResolver(Bind(source));
            var enemy = World.Own(point.Spawn());
            Assert.That(point.SpawnState, Is.EqualTo(EnemySpawnState.Completed));
            Assert.That(point.RegisteredToSourceCluster, Is.True); Assert.That(point.RegisteredSourceCluster, Is.EqualTo(source));
            Assert.That(Facts(resolver).AliveMembers, Is.EqualTo(1)); Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Ready));
            long version = resolver.Revision;
            Object.DestroyImmediate(enemy);
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Completed));
            Assert.That(resolver.Revision, Is.EqualTo(version), "成员死亡不使全图导航版本失效");
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator MissingHealthAndMissingRegistrationCannotMasqueradeAsClearGroups()
        {
            TestNavMeshBuilder.Flat(World);
            var badPrefab = World.Root("No health");
            var bad = Source(badPrefab, out var badPoint);
            var prototype = EnemyFactory.Passive(World, new Vector3(-20,0,10));
            var unregistered = Source(prototype.gameObject, out var unregisteredPoint, 20, false);
            var resolver = new MapGraphRouteTargetResolver(Bind(bad, unregistered));
            World.Own(badPoint.Spawn()); World.Own(unregisteredPoint.Spawn());
            Assert.That(badPoint.SpawnFailure, Is.EqualTo("MissingHealth"));
            Assert.That(unregisteredPoint.SpawnState, Is.EqualTo(EnemySpawnState.Completed));
            Assert.That(unregisteredPoint.RegisteredToSourceCluster, Is.False);
            Assert.That(Facts(resolver, "n0").Status, Is.EqualTo(AgentRouteTargetStatus.SpawnFailed));
            Assert.That(Facts(resolver, "n1").Status, Is.EqualTo(AgentRouteTargetStatus.SpawnFailed));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator DisabledLivingEnemiesAreNotReportedAsCompleted()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "disabled-enemy-observer", Vector3.zero);
            var enemy = EnemyFactory.Passive(World, Vector3.right * 10);
            var cluster = TargetFactory.Enemies(World, enemy); var resolver = new MapGraphRouteTargetResolver(Bind(cluster));
            enemy.gameObject.SetActive(false);
            var executable = new List<EnemyHealthController>(); cluster.CopyAliveEnemiesTo(executable);
            Assert.That(executable, Is.Empty); Assert.That(cluster.CountLivingEnemies(), Is.EqualTo(1));
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Ready));
            Assert.That(Facts(resolver).AliveMembers, Is.EqualTo(1));
            Assert.That(resolver.TryCreateProcessingDirective("n0", agent.AgentId, "no-member", 0, out _, out _), Is.False);
            float refreshAfter = Time.time + 0.1f;
            yield return RuntimeWait.Until(() => Time.time >= refreshAfter, "群聚合刷新");
            Assert.That(cluster.HasBeenCompleted, Is.False, "原聚合也不能永久标记临时禁用的活敌人完成");
            enemy.gameObject.SetActive(true); cluster.CopyAliveEnemiesTo(executable);
            Assert.That(executable.Count, Is.EqualTo(1));
            Object.DestroyImmediate(enemy.gameObject);
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Completed));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator InactiveNestedHealthIsStillLivingUntilActuallyDestroyed()
        {
            var enemy = EnemyFactory.Passive(World, Vector3.zero);
            var wrapper = World.Root("Enemy member wrapper"); enemy.transform.SetParent(wrapper.transform, true);
            var cluster = TargetFactory.Enemies(World, enemy);
            RuntimeFixtureAccess.Configure(cluster, "_initialEnemies", new List<GameplayTargetEntityMember> {
                new GameplayTargetEntityMember("nested-health", wrapper)
            });
            var resolver = new MapGraphRouteTargetResolver(Bind(cluster));
            enemy.gameObject.SetActive(false);
            Assert.That(Facts(resolver).AliveMembers, Is.EqualTo(1));
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Ready));
            Object.DestroyImmediate(enemy.gameObject);
            Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.Completed));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator DuplicateSourceIdsStillResolveConcreteMembersToTheirOwnNodes()
        {
            TestNavMeshBuilder.Flat(World);
            var prototype = EnemyFactory.Passive(World, new Vector3(-20,0,10));
            var a = Source(prototype.gameObject, out var aPoint, 10);
            var b = Source(prototype.gameObject, out var bPoint, 20);
            RuntimeFixtureAccess.Configure(a, "_targetId", "duplicate"); RuntimeFixtureAccess.Configure(b, "_targetId", "duplicate");
            var resolver = new MapGraphRouteTargetResolver(Bind(a, b));
            var aEnemy = World.Own(aPoint.Spawn()); var bEnemy = World.Own(bPoint.Spawn());
            Assert.That(resolver.TryResolveNode(AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, aEnemy), out var aNode), Is.True);
            Assert.That(resolver.TryResolveNode(AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, bEnemy), out var bNode), Is.True);
            Assert.That(aNode, Is.EqualTo("n0")); Assert.That(bNode, Is.EqualTo("n1"));
            Assert.That(resolver.TryResolveNode(AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, b.ConfiguredActiveEnemyCluster.gameObject), out bNode), Is.True);
            Assert.That(bNode, Is.EqualTo("n1"));
            Assert.That(resolver.TryResolveNode(AgentTargetRef.FromAbstractPoint(AgentTargetKind.EnemySource, "duplicate", Vector3.zero), out _), Is.False);
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator RegistrationToAnotherSourceIsNotEnoughForReadiness()
        {
            TestNavMeshBuilder.Flat(World);
            var prototype = EnemyFactory.Passive(World, new Vector3(-20,0,10));
            var actual = Source(prototype.gameObject, out var point);
            var other = Source(null, out var ignored, 20); ignored.gameObject.SetActive(false);
            RuntimeFixtureAccess.Configure(other, "_autoCollectChildSpawnPoints", false);
            RuntimeFixtureAccess.Configure(other, "_spawnPoints", new List<Transform> { point.transform });
            var resolver = new MapGraphRouteTargetResolver(Bind(actual, other));
            var enemy = World.Own(point.Spawn());
            other.ConfiguredActiveEnemyCluster.RegisterSceneEnemy(enemy.GetComponent<EnemyHealthController>());
            Assert.That(point.RegisteredSourceCluster, Is.EqualTo(actual));
            Assert.That(Facts(resolver, "n1").AliveMembers, Is.EqualTo(1));
            Assert.That(Facts(resolver, "n1").Status, Is.EqualTo(AgentRouteTargetStatus.SpawnFailed));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator ResourceEnemyAndExtractionCandidatesReuseFormalValidation()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "candidate-agent", Vector3.zero);
            var resource = TargetFactory.Resources(World, Vector3.right * 5);
            var enemy = EnemyFactory.Passive(World, Vector3.right * 10);
            var cluster = TargetFactory.Enemies(World, enemy);
            var extraction = TargetFactory.Extraction(World, Vector3.right * 15);
            var resolver = new MapGraphRouteTargetResolver(Bind(resource, cluster, extraction));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(resolver.TryCreateProcessingDirective("n"+i, agent.AgentId, "candidate-"+i, 0, out var request, out var failure), Is.True, failure.ToString());
                Assert.That(request.DirectiveType, Is.EqualTo(i == 0 ? AgentDirectiveType.Search : i == 1 ? AgentDirectiveType.Engage : AgentDirectiveType.Extract));
                Assert.That(AgentDirectiveValidationService.Validate(agent, request), Is.EqualTo(AgentDirectiveFailure.None));
            }
            Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False, "候选构造不执行指令");
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator AnchorRefreshIsBudgetedAndUnavailableIsDistinctFromMissing()
        {
            var a = TargetFactory.Resources(World, Vector3.zero); var b = TargetFactory.Resources(World, Vector3.right * 10);
            var resolver = new MapGraphRouteTargetResolver(Bind(a, b)); long initial = resolver.Revision;
            a.transform.position += Vector3.right * 3;
            Assert.That(resolver.RefreshAnchors(1), Is.EqualTo(1)); Assert.That(resolver.Revision, Is.GreaterThan(initial));
            Assert.That(Facts(resolver).Anchor.x, Is.EqualTo(3));
            b.enabled = false;
            Assert.That(Facts(resolver,"n1").Status, Is.EqualTo(AgentRouteTargetStatus.Unavailable));
            Object.DestroyImmediate(b.gameObject);
            Assert.That(Facts(resolver,"n1").Status, Is.EqualTo(AgentRouteTargetStatus.Missing));
            Assert.That(resolver.TryGetFacts("unknown", out _), Is.False);
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator ReadingAnUnreadySourceDoesNotCreateAnActiveCluster()
        {
            var source = Source(null, out _); Object.DestroyImmediate(source.ConfiguredActiveEnemyCluster.gameObject);
            var resolver = new MapGraphRouteTargetResolver(Bind(source));
            int children = source.transform.childCount;
            for (int i = 0; i < 4; i++) Assert.That(Facts(resolver).Status, Is.EqualTo(AgentRouteTargetStatus.WaitingSpawn));
            Assert.That(source.ConfiguredActiveEnemyCluster == null, Is.True); Assert.That(source.transform.childCount, Is.EqualTo(children));
            ContractCompleted = true; yield break;
        }
    }
}
