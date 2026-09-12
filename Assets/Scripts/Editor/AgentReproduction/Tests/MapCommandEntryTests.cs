using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Targeting;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandEntryTests : ReproductionTestFixture
    {
        private ActiveEnemyClusterAuthoring Cleared(Vector3 position)
        {
            var enemy=EnemyFactory.Passive(World,position+Vector3.forward*3);
            var cluster=TargetFactory.Enemies(World,enemy); Object.DestroyImmediate(enemy.gameObject); return cluster;
        }
        private MapGraphBindingAuthoring Bind(AgentPawnRoot pawn, GameplayTargetClusterAuthoringBase[] clusters, Vector3[] anchors, bool connected=true)
        {
            var binding=MapRouteFactory.Bind(World,clusters,anchors,connected?MapRouteFactory.Chain(clusters.Length):null);
            pawn.ConfigureRoutes(MapRouteFactory.Environment(binding,pawn)); return binding;
        }
        private AgentPawnRoot Pawn(bool discovery=false,float speed=8)
        { TestNavMeshBuilder.Flat(World); Time.timeScale=4; return AgentFactory.Create(World,"1",Vector3.left*3,speed,discovery,false); }
        private void Discovery(AgentPawnRoot pawn)
        {
            var decision=pawn.GetComponent<AgentTargetDecisionController>(); if(decision!=null) decision.enabled=false;
            AgentTargetDiscoveryController.GetOrCreate();
        }
        private AgentTargetDecisionController Decision(AgentPawnRoot pawn)
        {
            var controller=pawn.GetComponent<AgentTargetDecisionController>()??pawn.gameObject.AddComponent<AgentTargetDecisionController>();
            RuntimeFixtureAccess.Configure(controller,"_decisionConfig",World.Own(ScriptableObject.CreateInstance<AgentDecisionConfig>()));
            RuntimeFixtureAccess.Configure(controller,"_enableDecisionModule",true); controller.enabled=true; return controller;
        }
        private static AgentTargetRef Ref(GameplayTargetClusterAuthoringBase cluster,AgentTargetKind kind=AgentTargetKind.Resource) =>
            AgentTargetRef.FromConcreteObject(kind,cluster.gameObject,cluster.TargetId);
        private static IEnumerator UntilTime(double seconds)
        { double until=Time.realtimeSinceStartupAsDouble+seconds; yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=until,"观察重复扫描窗口",seconds+2); }

        [UnityTest] public IEnumerator CompletedWorldClusterStillExecutesTheFullAuthoredRoute()
        {
            var pawn=Pawn(); var positions=new[]{Vector3.zero,Vector3.right*10,Vector3.right*20};
            var clusters=positions.Select(x=>(GameplayTargetClusterAuthoringBase)Cleared(x)).ToArray(); Bind(pawn,clusters,positions);
            var visits=new List<string>();
            pawn.DirectiveLifecycle.ResultPublished+=r=>{if(r.Stage==AgentDirectiveStage.Completed)visits.Add(r.Request.RouteContext.NodeId);};
            var result=new AgentTargetCommandDispatcher().TrySubmitClusterRoute(clusters[2]);
            Assert.That(result.Stage,Is.EqualTo(AgentRouteStage.Planning));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.Stage==AgentRouteStage.Completed&&pawn.RouteSnapshot.HasRoute,"世界点击按整条路线到达");
            CollectionAssert.AreEqual(new[]{"n0","n1","n2"},visits); Assert.That(pawn.Position.x,Is.GreaterThan(19));
            Assert.That(pawn.RouteSnapshot.Request.Source,Is.EqualTo(AgentRouteSource.Player)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator WorldAndRouterRequestsUseEquivalentNodesAndFocusedIdentity()
        {
            var first=Pawn(speed:0); var second=AgentFactory.Create(World,"2",Vector3.left*3+Vector3.forward,0,false,false);
            var clusters=new GameplayTargetClusterAuthoringBase[]{Cleared(Vector3.zero),Cleared(Vector3.right*20)};
            var binding=Bind(first,clusters,new[]{Vector3.zero,Vector3.right*20}); second.ConfigureRoutes(MapRouteFactory.Environment(binding,second));
            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2");
            var world=new AgentTargetCommandDispatcher().TrySubmitClusterRoute(clusters[1]);
            Assert.That(world.Request.TargetAgentId,Is.EqualTo(second.AgentId));
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,first.AgentId));
            yield return RuntimeWait.Until(()=>first.RouteSnapshot.IsActive&&second.RouteSnapshot.IsActive,"两个入口接受");
            CollectionAssert.AreEqual(first.RouteSnapshot.NodeIds,second.RouteSnapshot.NodeIds);
            Assert.That(first.RouteSnapshot.CurrentStep.Context.RootRequestId,Is.Not.EqualTo(second.RouteSnapshot.CurrentStep.Context.RootRequestId));
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator LegacyDirectiveReportsPlanningAndCannotBypassTheGraphWithALocation()
        {
            var pawn=Pawn(speed:0); var cluster=Cleared(Vector3.right*10); Bind(pawn,new GameplayTargetClusterAuthoringBase[]{cluster},new[]{Vector3.right*10});
            var request=new AgentDirectiveRequest(AgentDirectiveType.Engage,Ref(cluster,AgentTargetKind.Enemy),
                targetAgentId:pawn.AgentId,commandId:AgentManualDirectiveLock.CreateCommandId(cluster.TargetId),priority:AgentManualDirectiveLock.ManualDirectivePriority);
            Assert.That(pawn.TrySubmitDirective(request).Stage,Is.EqualTo(AgentDirectiveStage.Planning));
            Assert.That(pawn.DirectiveLifecycle.Active.HasValue,Is.False);
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"兼容调用生成根");
            var current=pawn.DirectiveLifecycle.Active.Value;
            var invalid=new AgentDirectiveRequest(AgentDirectiveType.MoveTo,AgentTargetRef.FromAbstractPoint(AgentTargetKind.Location,"",Vector3.right*30),"bad-location",pawn.AgentId);
            Assert.That(pawn.TrySubmitDirective(invalid).Stage,Is.EqualTo(AgentDirectiveStage.Rejected));
            Assert.That(pawn.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(current.CommandId));
            Assert.That(current.RouteContext.IsValid,Is.True); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator DecisionCreatesAnAutonomousRootAndDoesNotReplaceItAtRefresh()
        {
            var pawn=Pawn(true,0); var target=TargetFactory.Resources(World,Vector3.right*10);
            Bind(pawn,new GameplayTargetClusterAuthoringBase[]{target},new[]{Vector3.right*8});
            var decision=Decision(pawn); int planning=0; pawn.RouteResultPublished+=r=>{if(r.Stage==AgentRouteStage.Planning)planning++;};
            decision.RefreshTarget(); Assert.That(pawn.RouteSnapshot.HasPendingRequest,Is.True);
            for(int i=0;i<5;i++)decision.RefreshTarget(); Assert.That(planning,Is.EqualTo(1));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"Decision 根接受");
            var root=pawn.RouteSnapshot.Request.RequestId; decision.RefreshTarget(); decision.enabled=false;
            Assert.That(pawn.RouteSnapshot.IsActive,Is.True); Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo(root));
            Assert.That(pawn.RouteSnapshot.Request.Source,Is.EqualTo(AgentRouteSource.Autonomous)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator DiscoveryCreatesAnAutonomousRootAndHoldsAcrossRepeatedScans()
        {
            var pawn=Pawn(true,0); var target=TargetFactory.Resources(World,Vector3.right*10);
            Bind(pawn,new GameplayTargetClusterAuthoringBase[]{target},new[]{Vector3.right*8}); Discovery(pawn);
            int planning=0; pawn.RouteResultPublished+=r=>{if(r.Stage==AgentRouteStage.Planning)planning++;};
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"Discovery 根接受");
            var root=pawn.RouteSnapshot.Request.RequestId; yield return UntilTime(0.3);
            Assert.That(planning,Is.EqualTo(1)); Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo(root));
            Assert.That(pawn.RouteSnapshot.Request.Source,Is.EqualTo(AgentRouteSource.Autonomous)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator PendingPlayerRouteSurvivesDecisionRefreshAndDisable()
        {
            var pawn=Pawn(true,0); var target=TargetFactory.Resources(World,Vector3.right*10);
            var binding=Bind(pawn,new GameplayTargetClusterAuthoringBase[]{target},new[]{Vector3.right*8}); var ready=MapRouteFactory.Environment(binding,pawn);
            pawn.ConfigureRoutes(new AgentRouteEnvironment(ready.Graph,ready.GraphRevision,2,ready.Costs,ready.Targets,ready.Profile,false));
            var decision=Decision(pawn); var result=new AgentTargetCommandDispatcher().TrySubmitClusterRoute(target);
            decision.RefreshTarget(); decision.enabled=false;
            Assert.That(pawn.RouteSnapshot.PendingRequest.RequestId,Is.EqualTo(result.Request.RequestId));
            pawn.ConfigureRoutes(ready); yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"等待期间玩家根保留");
            Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo(result.Request.RequestId)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator FinishingPlayerDestinationResumesAutonomousSelection()
        {
            var pawn=Pawn(true); var first=Cleared(Vector3.zero); var resource=TargetFactory.Resources(World,Vector3.right*20);
            Bind(pawn,new GameplayTargetClusterAuthoringBase[]{first,resource},new[]{Vector3.zero,Vector3.right*18}); Discovery(pawn);
            var results=new List<AgentRouteResult>(); pawn.RouteResultPublished+=results.Add;
            var manual=new AgentTargetCommandDispatcher().TrySubmitClusterRoute(first);
            yield return RuntimeWait.Until(()=>results.Any(r=>r.Request.RequestId==manual.Request.RequestId&&r.Stage==AgentRouteStage.Completed),"玩家移动终点完成");
            yield return RuntimeWait.Until(()=>results.Any(r=>r.Accepted&&r.Request.Source==AgentRouteSource.Autonomous),"结束后恢复自主路线");
            Assert.That(results.Last(r=>r.Accepted).Request.TargetNodeId,Is.EqualTo("n1")); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator DisconnectedAutonomousTargetIsDeferredUntilTopologyChanges()
        {
            var pawn=Pawn(true,0); var first=Cleared(Vector3.zero); var resource=TargetFactory.Resources(World,Vector3.right*20);
            var binding=Bind(pawn,new GameplayTargetClusterAuthoringBase[]{first,resource},new[]{Vector3.zero,Vector3.right*18},false); Discovery(pawn);
            var results=new List<AgentRouteResult>(); pawn.RouteResultPublished+=results.Add;
            yield return RuntimeWait.Until(()=>results.Any(r=>r.Reason==AgentRouteFailure.Disconnected),"断图产生根拒绝");
            long queries=pawn.RoutePlanningQueryCount; yield return UntilTime(1.1);
            Assert.That(results.Count(r=>r.Stage==AgentRouteStage.Planning),Is.EqualTo(1)); Assert.That(pawn.RoutePlanningQueryCount,Is.EqualTo(queries));
            Assert.That(pawn.CanSelectAutonomousRouteTarget(Ref(resource)),Is.False);
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterRoute(resource).Stage,Is.EqualTo(AgentRouteStage.Planning),"玩家可以主动重试，不受自主失败记忆过滤");
            yield return RuntimeWait.Until(()=>!pawn.RouteSnapshot.HasPendingRequest,"手动重试也诚实报告断图");
            var definition=binding.MapDefinition;
            definition.ApplyCommandData(definition.MapId,"connected","",definition.Zones,definition.Nodes,MapRouteFactory.Chain(2),new MapGraphLayoutConstraints(),new MapGraphNavigationBakeData());
            binding.RebuildIndexes(); pawn.ConfigureRoutes(MapRouteFactory.Environment(binding,pawn));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"实际连线恢复后重新选择");
            Assert.That(pawn.RouteSnapshot.Request.TargetNodeId,Is.EqualTo("n1")); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator FailureMemoryUsesWorldChangesAndBoundedCooldownWithoutBlockingManualRequests()
        {
            var pawn=Pawn(); var target=TargetFactory.Resources(World,Vector3.right*10); var binding=Bind(pawn,new GameplayTargetClusterAuthoringBase[]{target},new[]{Vector3.right*8});
            var environment=MapRouteFactory.Environment(binding,pawn); var memory=new AgentRouteFailureMemory();
            var request=new AgentRouteRequest("n0",AgentRouteSource.Autonomous,pawn.AgentId);
            memory.Observe(new AgentRouteResult(request,1,AgentRouteStage.Failed,AgentRouteFailure.NoProgress),environment,Vector3.zero,0);
            Assert.That(memory.CanSelect("n0",environment,Vector3.zero,2),Is.False); Assert.That(memory.CanSelect("n0",environment,Vector3.zero,4),Is.True);
            memory.Observe(new AgentRouteResult(request,1,AgentRouteStage.Rejected,AgentRouteFailure.Disconnected),environment,Vector3.zero,4);
            Assert.That(memory.CanSelect("n0",environment,Vector3.zero,999),Is.False); Assert.That(memory.CanSelect("n0",environment,Vector3.right*3,999),Is.True);
            for(int i=0;i<80;i++)memory.Observe(new AgentRouteResult(new AgentRouteRequest("n"+i,AgentRouteSource.Autonomous),1,AgentRouteStage.Rejected,AgentRouteFailure.Disconnected),environment,Vector3.zero,0);
            Assert.That(memory.Count,Is.EqualTo(64));
            var manual=new AgentTargetCommandDispatcher().TrySubmitClusterRoute(target); Assert.That(manual.Stage,Is.EqualTo(AgentRouteStage.Planning));
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator UnboundVisibleEnemiesStayInRiskFactsButCannotBecomeRoutes()
        {
            var pawn=Pawn(); Bind(pawn,new GameplayTargetClusterAuthoringBase[]{Cleared(Vector3.zero)},new[]{Vector3.zero});
            var enemy=EnemyFactory.Passive(World,new Vector3(-3,0,10)); var unbound=TargetFactory.Enemies(World,enemy);
            yield return null; Physics.SyncTransforms();
            var collector=new AgentTargetCandidateCollector(); var results=new List<AgentTargetCandidate>();
            collector.CollectVisibleEnemies(pawn,new[]{unbound},100,results);
            Assert.That(results.Count,Is.EqualTo(1),Gameplay.Perception.TargetVisibilityQuery.Check(pawn.CachedTransform,Gameplay.Perception.CombatAimPointResolver.Resolve(pawn.CachedTransform),enemy.transform,100).ToString()); Assert.That(results[0].CanExecute,Is.False);
            collector.CollectVisibleEnemies(pawn,new[]{unbound},1,results); Assert.That(results,Is.Empty);
            World.Cube("Opaque wall",new Vector3(-3,5,5),new Vector3(10,20,1)); Physics.SyncTransforms();
            collector.CollectVisibleEnemies(pawn,new[]{unbound},100,results); Assert.That(results,Is.Empty);
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator CapacityFailureAutomaticallySelectsExitAndLeavesAllBoxRemainders()
        {
            var pawn=Pawn(true); Discovery(pawn); var screen=InventoryFactory.Create(World);
            var item=Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase").Items.First(x=>x.IncludeInRuntimeDatabase&&x.Width==1&&x.Height==1&&x.Type!=ItemType.Bag);
            LootBoxEntity Box(string name,float x) {
                var box=World.Root(name).AddComponent<LootBoxEntity>(); box.transform.position=Vector3.right*x; box.UseBoardGameResourceRules=false;
                box.SaveRuntimeState(new List<ContainerItemSaveData>{new ContainerItemSaveData{ItemData=item,Amount=1}},new List<ContainerCellStateSaveData>()); return box;
            }
            var first=Box("Full box",5); var second=Box("Route box",14);
            var exit=TargetFactory.Extraction(World,Vector3.right*24); exit.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds=0.2f;
            var firstCluster=TargetFactory.ResourceBoxes(World,first);
            Bind(pawn,new GameplayTargetClusterAuthoringBase[]{firstCluster,TargetFactory.ResourceBoxes(World,second),exit},new[]{Vector3.right*3,Vector3.right*12,Vector3.right*24});
            var raid=World.Root("Capacity route raid").AddComponent<RaidFlowController>(); var events=new List<AgentRouteResult>(); pawn.RouteResultPublished+=events.Add;
            yield return null; Time.timeScale=4;
            new AgentTargetCommandDispatcher().TrySubmitClusterRoute(firstCluster);
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"正式资源等待");
            first.Interact();
            for(int y=0;y<6;y++)for(int x=0;x<5;x++)Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item,screen.BackpackGrid,x,y,item.IsStackable?item.MaxStack:1),Is.Not.Null);
            var view=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x=>x.CurrentGrid==screen.ActiveExternalGrid);
            yield return RuntimeWait.Until(()=>view.IsInteractionReady,"箱内搜索完成"); screen.CloseInventory();
            yield return RuntimeWait.Until(()=>raid.IsInputLocked,"自主决定撤离并正式结算",20);
            Assert.That(new SceneRaidReadModel(new SceneRaidIdentityMap(),null).Capture().missionCompleted,Is.True);
            Assert.That(events.Any(r=>r.Reason==AgentRouteFailure.CapacityExtraction),Is.True);
            Assert.That(events.Any(r=>r.Accepted&&r.Request.Source==AgentRouteSource.Autonomous&&r.Request.TargetNodeId=="n2"),Is.True);
            Assert.That(first.GetSavedItems().Single().Amount,Is.EqualTo(1)); Assert.That(second.GetSavedItems().Single().Amount,Is.EqualTo(1));
            Assert.That(events.Count(r=>r.Stage==AgentRouteStage.Extracted),Is.EqualTo(1)); ContractCompleted=true;
        }
    }
}
