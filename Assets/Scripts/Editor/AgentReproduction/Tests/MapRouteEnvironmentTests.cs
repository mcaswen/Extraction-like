using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapRouteEnvironmentTests : ReproductionTestFixture
    {
        private MapGraphBindingAuthoring _binding;
        private ResourceClusterAuthoring _first;
        private AgentNavigationProfile Profile(float radius=1) => new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID,-1,radius,1);
        private MapGraphRouteEnvironmentService Build()
        {
            TestNavMeshBuilder.Flat(World); _first=TargetFactory.Resources(World,Vector3.zero);
            _binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{_first,TargetFactory.Resources(World,Vector3.right*10),TargetFactory.Resources(World,Vector3.right*20)},
                new[]{Vector3.zero,Vector3.right*10,Vector3.right*20},MapRouteFactory.Chain(3));
            return new MapGraphRouteEnvironmentService(_binding);
        }

        [UnityTest]
        public IEnumerator EquivalentProfilesShareSnapshotsAndOneGlobalQueryBudget()
        {
            var service=Build(); var first=service.GetEnvironment(Profile()); var same=service.GetEnvironment(Profile());
            Assert.That(same,Is.SameAs(first)); service.GetEnvironment(Profile(2)); Assert.That(service.ProfileCount,Is.EqualTo(2));
            Assert.That(service.PendingEdgeCount,Is.EqualTo(4));
            for(int i=0;i<4;i++) {
                long before=service.CalculationCount; Assert.That(service.Tick(1),Is.EqualTo(1));
                Assert.That(service.CalculationCount-before,Is.EqualTo(2));
            }
            Assert.That(service.GetEnvironment(Profile()).IsReady,Is.True);
            Assert.That(service.GetEnvironment(Profile(2)).IsReady,Is.True);
            Assert.That(first.IsReady,Is.False); Assert.That(first.Costs.Count,Is.Zero,"旧快照不可变");
            var ready=service.GetEnvironment(Profile()); Assert.That(service.Tick(1),Is.Zero);
            Assert.That(service.GetEnvironment(Profile()),Is.SameAs(ready));
            service.PruneProfiles(new HashSet<string>{ready.Costs.ProfileId}); Assert.That(service.ProfileCount,Is.EqualTo(1));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator ObservedAnchorRevisionCannotPublishOldCostsAsReady()
        {
            var service=Build(); var profile=Profile(); service.GetEnvironment(profile); service.Tick(2);
            var before=service.GetEnvironment(profile); Assert.That(before.IsReady,Is.True);
            _first.transform.position+=Vector3.right*2;
            Assert.That(before.Targets.TryGetFacts("n0",out _),Is.True);
            Assert.That(before.Targets.Revision,Is.Not.EqualTo(before.TargetRevision));
            long queries=service.CalculationCount; service.Tick(0);
            var invalid=service.GetEnvironment(profile); Assert.That(invalid.IsReady,Is.False);
            Assert.That(invalid.Costs.Count,Is.EqualTo(1)); Assert.That(service.PendingEdgeCount,Is.EqualTo(1));
            Assert.That(service.CalculationCount,Is.EqualTo(queries));
            service.Tick(1); var current=service.GetEnvironment(profile);
            Assert.That(current.IsReady,Is.True); Assert.That(current.TargetRevision,Is.EqualTo(current.Targets.Revision));
            Assert.That(service.CalculationCount-queries,Is.EqualTo(2));
            Assert.That(current.Costs.TryGetCost(_binding.MapDefinition.Edges[0],"n0",out float cost),Is.True);
            Assert.That(cost,Is.EqualTo(8).Within(0.1)); Assert.That(before.Costs.Count,Is.EqualTo(2));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator NavigationPauseAndRebuildNeverExposeAReadyStaleEnvironment()
        {
            var service=Build(); var profile=Profile(); service.GetEnvironment(profile); service.Tick(2);
            var before=service.GetEnvironment(profile);
            service.SetNavigationState(false,"rebuilt",true);
            Assert.That(service.GetEnvironment(profile).NavigationReady,Is.False);
            Assert.That(service.GetEnvironment(profile).IsReady,Is.False); Assert.That(service.Tick(20),Is.Zero);
            long queries=service.CalculationCount; service.SetNavigationState(true,"rebuilt");
            Assert.That(service.GetEnvironment(profile).IsReady,Is.False);
            service.Tick(1); Assert.That(service.CalculationCount-queries,Is.EqualTo(2));
            service.Tick(1); var after=service.GetEnvironment(profile); Assert.That(after.IsReady,Is.True);
            Assert.That(after.ContextVersion,Is.GreaterThan(before.ContextVersion)); ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator DisabledBindingInvalidatesSharedEnvironmentAndCanBeRestored()
        {
            var service=Build(); var profile=Profile(); service.GetEnvironment(profile); service.Tick(2);
            var before=service.GetEnvironment(profile); _binding.enabled=false; service.Tick(2);
            Assert.That(service.GetEnvironment(profile).IsReady,Is.False); Assert.That(service.Graph,Is.Null);
            _binding.enabled=true; service.Tick(2);
            Assert.That(service.GetEnvironment(profile).IsReady,Is.True);
            Assert.That(service.GetEnvironment(profile).Graph,Is.Not.SameAs(before.Graph));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator SourceActiveClusterAvailabilityHasAnObservableRevision()
        {
            TestNavMeshBuilder.Flat(World);
            var source=World.Root("Source").AddComponent<EnemySourceClusterAuthoring>();
            Assert.That(source.ConfiguredActiveEnemyCluster,Is.Not.Null);
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{source},new[]{Vector3.zero});
            var service=new MapGraphRouteEnvironmentService(binding); var profile=Profile(); var before=service.GetEnvironment(profile);
            source.ConfiguredActiveEnemyCluster.enabled=false;
            service.Tick(0); var unavailable=service.GetEnvironment(profile);
            Assert.That(unavailable.TargetRevision,Is.GreaterThan(before.TargetRevision));
            Assert.That(unavailable.Targets.TryGetFacts("n0",out var facts),Is.True); Assert.That(facts.Status,Is.EqualTo(AgentRouteTargetStatus.Unavailable));
            source.ConfiguredActiveEnemyCluster.enabled=true; service.Tick(0);
            Assert.That(service.GetEnvironment(profile).TargetRevision,Is.GreaterThan(unavailable.TargetRevision));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator ReplacingBindingResolverReplansEvenWhenNodeIdsAndAnchorsMatch()
        {
            var service=Build(); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false);
            var profile=AgentNavigationProfile.FromAgent(agent.NavMeshAgent);
            service.GetEnvironment(profile); service.Tick(2); agent.ConfigureRoutes(service.GetEnvironment(profile));
            agent.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,agent.AgentId,"binding-root"));
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.IsActive,"接受第一组真实绑定");
            var before=agent.RouteSnapshot;
            var replacement=new MapGraphRouteEnvironmentService(_binding);
            replacement.GetEnvironment(profile); replacement.Tick(2);
            agent.ConfigureRoutes(replacement.GetEnvironment(profile));
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.RouteVersion>before.RouteVersion,"新 Resolver 必须重新创建步骤");
            Assert.That(agent.RouteSnapshot.Request.RequestId,Is.EqualTo("binding-root"));
            Assert.That(agent.RouteSnapshot.CurrentStep.Context.RouteVersion,Is.EqualTo(agent.RouteSnapshot.RouteVersion));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator UnrelatedEdgeRevalidationDoesNotCloseInventoryOrReplaceRoot()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var screen=InventoryFactory.Create(World);
            var item=World.Own(ScriptableObject.CreateInstance<InventoryItemData>()); item.ItemID="SharedEnvironmentItem"; item.Width=item.Height=1;
            var box=World.Root("Waiting box").AddComponent<LootBoxEntity>(); box.transform.position=Vector3.right*3; box.UseBoardGameResourceRules=false;
            box.SaveRuntimeState(new List<ContainerItemSaveData>{new ContainerItemSaveData{ItemData=item,Amount=1}},new List<ContainerCellStateSaveData>());
            var clusters=new GameplayTargetClusterAuthoringBase[5]; clusters[0]=TargetFactory.ResourceBoxes(World,box);
            var anchors=new Vector3[5];
            for(int i=1;i<5;i++) { anchors[i]=Vector3.right*i*10; clusters[i]=TargetFactory.Resources(World,anchors[i]); }
            var binding=MapRouteFactory.Bind(World,clusters,anchors,MapRouteFactory.Chain(5));
            var service=new MapGraphRouteEnvironmentService(binding); var profile=AgentNavigationProfile.FromAgent(agent.NavMeshAgent);
            service.GetEnvironment(profile); while(service.PendingEdgeCount>0) service.Tick(2);
            agent.ConfigureRoutes(service.GetEnvironment(profile)); yield return null;
            agent.TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,agent.AgentId,"waiting-root"));
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"在当前根等待箱子");
            box.Interact(); var session=screen.ActiveSessionContext; var before=agent.RouteSnapshot;
            var command=agent.DirectiveLifecycle.Active.Value.CommandId; int cancelled=0;
            agent.DirectiveLifecycle.ResultPublished+=result=>{if(result.Stage==AgentDirectiveStage.Cancelled)cancelled++;};
            var environment=service.GetEnvironment(profile); service.InvalidateEdge(environment.Costs.ProfileId,"e3","unrelated");
            service.Tick(0); agent.ConfigureRoutes(service.GetEnvironment(profile));
            double until=Time.realtimeSinceStartupAsDouble+0.3;
            yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=until,"无关边成本暂缺");
            Assert.That(screen.IsInventoryOpen,Is.True); Assert.That(screen.ActiveSessionContext,Is.SameAs(session));
            Assert.That(agent.RouteSnapshot.RouteVersion,Is.EqualTo(before.RouteVersion));
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(command)); Assert.That(cancelled,Is.Zero);
            service.Tick(1); agent.ConfigureRoutes(service.GetEnvironment(profile)); yield return null;
            Assert.That(agent.RouteSnapshot.Request.RequestId,Is.EqualTo("waiting-root")); Assert.That(screen.IsInventoryOpen,Is.True);
            screen.CloseInventory(); ContractCompleted=true;
        }
    }
}
