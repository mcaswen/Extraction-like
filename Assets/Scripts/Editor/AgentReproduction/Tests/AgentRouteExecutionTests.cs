using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class AgentRouteExecutionTests : ReproductionTestFixture
    {
        private ActiveEnemyClusterAuthoring Cleared(Vector3 position)
        {
            var enemy=EnemyFactory.Passive(World,position+Vector3.forward*3);
            var cluster=TargetFactory.Enemies(World,enemy); Object.DestroyImmediate(enemy.gameObject); return cluster;
        }
        private MapGraphBindingAuthoring Install(AgentPawnRoot agent, params Vector3[] anchors)
        {
            var clusters=anchors.Select(x=>(GameplayTargetClusterAuthoringBase)Cleared(x)).ToArray();
            var binding=MapRouteFactory.Bind(World,clusters,anchors,MapRouteFactory.Chain(anchors.Length));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent)); return binding;
        }
        private static AgentRouteResult Send(AgentPawnRoot agent,string node,string root="root",AgentRouteSource source=AgentRouteSource.Player)
            => AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest(node,source,agent.AgentId,root));
        private static IEnumerator Finished(AgentPawnRoot agent,string message="路线结束")
        { yield return RuntimeWait.Until(()=>agent.RouteSnapshot.HasRoute&&!agent.RouteSnapshot.IsActive&&!agent.RouteSnapshot.HasPendingRequest,message,20); }
        private static void AssertComplete(AgentPawnRoot agent)
        {
            var value=agent.RouteSnapshot;
            CaseArtifactWriter.Trace("route-terminal",value.Request.RequestId+"; "+value.Stage+"; "+value.Failure+"; index="+value.StepIndex+"; pos="+agent.Position.ToString("R"));
            Assert.That(value.Stage,Is.EqualTo(AgentRouteStage.Completed),value.Failure.ToString());
        }

        [UnityTest]
        public IEnumerator RouterExecutesEveryClusterInOrderWithActualArrival()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,8,false,false);
            Install(agent,Vector3.zero,Vector3.right*10,Vector3.right*20);
            var visited=new List<string>(); var positions=new List<float>();
            agent.DirectiveLifecycle.ResultPublished+=result=> {
                if(result.Stage==AgentDirectiveStage.Completed && result.Request.DirectiveType==AgentDirectiveType.MoveTo)
                { visited.Add(result.Request.RouteContext.NodeId); positions.Add(agent.Position.x); }
            };
            var result=Send(agent,"n2"); Assert.That(result.Stage,Is.EqualTo(AgentRouteStage.Planning));
            yield return Finished(agent); AssertComplete(agent);
            CollectionAssert.AreEqual(new[]{"n0","n1","n2"},visited);
            for(int i=0;i<positions.Count;i++) Assert.That(positions[i],Is.EqualTo(i*10).Within(0.4));
            Assert.That(agent.RoutePlanningQueryCount,Is.EqualTo(3)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ResourceThenCombatThenDestinationUsesOneRoot()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8);
            var screen=InventoryFactory.Create(World);
            var item=World.Own(ScriptableObject.CreateInstance<InventoryItemData>()); item.ItemID="RouteItem"; item.Width=item.Height=1;
            var box=World.Root("Route source box").AddComponent<LootBoxEntity>(); box.transform.position=Vector3.right*5; box.UseBoardGameResourceRules=false;
            box.SaveRuntimeState(new List<ContainerItemSaveData>{new ContainerItemSaveData{ItemData=item,Amount=1}},new List<ContainerCellStateSaveData>());
            var enemy=EnemyFactory.Passive(World,new Vector3(15,0,2),40); var enemies=TargetFactory.Enemies(World,enemy);
            var clusters=new GameplayTargetClusterAuthoringBase[]{TargetFactory.ResourceBoxes(World,box),enemies,Cleared(Vector3.right*25)};
            var binding=MapRouteFactory.Bind(World,clusters,new[]{Vector3.right*3,Vector3.right*12,Vector3.right*25},MapRouteFactory.Chain(3));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent)); yield return null;
            var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add;
            Send(agent,"n2","whole-root");
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"根路线等待背包");
            Assert.That(agent.RouteSnapshot.StepIndex,Is.Zero); Assert.That(enemy.HasLivingHealth,Is.True);
            box.Interact();
            var view=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x=>x.CurrentGrid==screen.ActiveExternalGrid);
            yield return RuntimeWait.Until(()=>view.IsInteractionReady,"正式箱内搜索");
            Assert.That(view.TryQuickTransfer(out _),Is.True); screen.CloseInventory();
            yield return Finished(agent,"搜完资源、清掉敌人、到达终点"); AssertComplete(agent);
            Assert.That(enemies.CountLivingEnemies(),Is.Zero); Assert.That(box.GetSavedItems(),Is.Empty);
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Accepted),Is.EqualTo(1));
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Completed),Is.EqualTo(1));
            Assert.That(agent.Position.x,Is.GreaterThan(24)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RejectedDisconnectedReplacementPreservesOldRoute()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,4,false,false);
            var positions=new[]{Vector3.zero,Vector3.right*10,Vector3.right*20,Vector3.forward*10};
            var binding=MapRouteFactory.Bind(World,positions.Select(x=>(GameplayTargetClusterAuthoringBase)Cleared(x)).ToArray(),positions,MapRouteFactory.Chain(3));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent)); var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add;
            Send(agent,"n2","old"); yield return RuntimeWait.Until(()=>agent.RouteSnapshot.IsActive,"旧路线接受");
            string command=agent.DirectiveLifecycle.Active.Value.CommandId;
            Assert.That(Send(agent,"missing","bad").Reason,Is.EqualTo(AgentRouteFailure.MissingTarget));
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(command));
            Send(agent,"n3","disconnected");
            yield return RuntimeWait.Until(()=>results.Any(x=>x.Request.RequestId=="disconnected"&&x.Stage==AgentRouteStage.Rejected),"断图拒绝");
            Assert.That(agent.RouteSnapshot.Request.RequestId,Is.EqualTo("old"));
            Assert.That(results.Single(x=>x.Request.RequestId=="disconnected"&&x.Stage==AgentRouteStage.Rejected).Reason,Is.EqualTo(AgentRouteFailure.Disconnected));
            yield return Finished(agent); AssertComplete(agent); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ValidReplacementFromCurrentEdgeRejectsOldCompletion()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            Install(agent,Vector3.zero,Vector3.right*12,Vector3.right*24);
            Send(agent,"n2","old");
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.StepIndex==1&&agent.Position.x>2,"进入第一条边");
            var old=agent.DirectiveLifecycle.Active.Value;
            Send(agent,"n0","new");
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.Request.RequestId=="new","新根原子接受");
            Assert.That(agent.RouteSnapshot.NodeIds[0],Is.EqualTo("n0"));
            Assert.That(agent.FinishDirective(old.CommandId),Is.False);
            Assert.That(agent.RouteSnapshot.RouteVersion,Is.GreaterThan(old.RouteContext.RouteVersion));
            yield return Finished(agent); AssertComplete(agent); Assert.That(agent.Position.x,Is.EqualTo(0).Within(0.4)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RetaliationKeepsRootAndOnlyValidNewRequestReplacesIt()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            Install(agent,Vector3.zero,Vector3.right*12,Vector3.right*24); Send(agent,"n2","old");
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.StepIndex==1&&agent.Position.x>2,"反击前的边");
            var enemy=EnemyFactory.Passive(World,agent.Position+Vector3.forward*10);
            var old=agent.DirectiveLifecycle.Active.Value;
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            Assert.That(agent.RouteSnapshot.CurrentStep.IsRetaliating,Is.True);
            Assert.That(Send(agent,"missing","bad").Stage,Is.EqualTo(AgentRouteStage.Rejected));
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.Value.CommandId,Is.EqualTo(old.CommandId));
            Send(agent,"n0","new"); yield return RuntimeWait.Until(()=>agent.RouteSnapshot.Request.RequestId=="new","反击中有效改令");
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue,Is.False);
            Object.DestroyImmediate(enemy.gameObject); Assert.That(agent.FinishDirective(old.CommandId),Is.False);
            yield return Finished(agent); AssertComplete(agent); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator PlayerRootLockCoversTheGapBetweenCompletedChildren()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false);
            Install(agent,Vector3.zero,Vector3.right*10); int gaps=0;
            agent.DirectiveLifecycle.ResultPublished+=result=> {
                if(result.Stage!=AgentDirectiveStage.Completed || !result.Request.RouteContext.IsValid) return;
                gaps++; Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(agent),Is.True);
                Assert.That(Send(agent,"n0","auto",AgentRouteSource.Autonomous).Reason,Is.EqualTo(AgentRouteFailure.Superseded));
            };
            Send(agent,"n1"); yield return Finished(agent); AssertComplete(agent);
            Assert.That(gaps,Is.EqualTo(2)); Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(agent),Is.False);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator PlanningDiscardsChangedEnvironmentAndAcceptsOnlyCurrentCosts()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false);
            var binding=Install(agent,Enumerable.Range(0,9).Select(x=>Vector3.right*x*4).ToArray());
            var accepted=new List<AgentRouteResult>(); agent.RouteResultPublished+=result=>{if(result.Accepted) accepted.Add(result);};
            Send(agent,"n8"); yield return null;
            Assert.That(agent.RouteSnapshot.HasRoute,Is.False);
            var newer=MapRouteFactory.Environment(binding,agent,2); agent.ConfigureRoutes(newer);
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.IsActive,"新环境接受");
            Assert.That(accepted.Count,Is.EqualTo(1)); Assert.That(agent.RoutePlanningQueryCount,Is.LessThanOrEqualTo(18));
            yield return Finished(agent); AssertComplete(agent); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RemovingTheRequiredEdgeCannotFallBackToDirectDestination()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            var binding=Install(agent,Vector3.zero,Vector3.right*12,Vector3.right*24);
            Send(agent,"n2"); yield return RuntimeWait.Until(()=>agent.RouteSnapshot.StepIndex==1&&agent.Position.x>2,"删边前行进");
            var definition=binding.MapDefinition;
            definition.ApplyCommandData(definition.MapId,"changed","",definition.Zones,definition.Nodes,
                new[]{definition.Edges[0]},new MapGraphLayoutConstraints(),new MapGraphNavigationBakeData());
            binding.RebuildIndexes(); agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent,2));
            yield return Finished(agent,"删边后有界结束");
            Assert.That(agent.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Failed));
            Assert.That(agent.RouteSnapshot.Failure,Is.EqualTo(AgentRouteFailure.Disconnected));
            Assert.That(agent.Position.x,Is.LessThan(20)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator FocusedRouterAndTwoAgentsKeepIndependentRoots()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var a=AgentFactory.Create(World,"A",Vector3.left*4,8,false,false);
            var b=AgentFactory.Create(World,"B",Vector3.right*14,8,false,false);
            var binding=Install(a,Vector3.zero,Vector3.right*10,Vector3.right*20);
            b.ConfigureRoutes(MapRouteFactory.Environment(binding,b));
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("B"),Is.True);
            var routed=AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,requestId:"B-root"));
            Assert.That(routed.Request.TargetAgentId,Is.EqualTo(b.AgentId));
            Send(a,"n0","A-root",AgentRouteSource.Autonomous);
            yield return Finished(a); yield return Finished(b); AssertComplete(a); AssertComplete(b);
            Assert.That(a.RouteSnapshot.Request.RequestId,Is.EqualTo("A-root")); Assert.That(b.RouteSnapshot.Request.RequestId,Is.EqualTo("B-root"));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DeathEndsOneRootAndDisableCannotChangeItsTerminal()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            Install(agent,Vector3.zero,Vector3.right*12); var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add;
            Send(agent,"n1"); yield return RuntimeWait.Until(()=>agent.RouteSnapshot.IsActive,"死亡前接受");
            agent.TakeCombatDamage(999999,agent.Position,Vector3.zero,null);
            Assert.That(agent.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Dead));
            agent.enabled=false; yield return null;
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Dead),Is.EqualTo(1));
            Assert.That(results.Any(x=>x.Stage==AgentRouteStage.Cancelled),Is.False);
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue,Is.False); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator IntermediatePhysicalExitDoesNotSettleUntilFinalExit()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,8,false,false); InventoryFactory.Create(World);
            var first=TargetFactory.Extraction(World,Vector3.zero); var last=TargetFactory.Extraction(World,Vector3.right*20);
            var firstPoint=first.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>(); firstPoint.ExtractionDurationSeconds=0.05f;
            firstPoint.GetComponent<BoxCollider>().size=new Vector3(6,8,6);
            last.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds=0.3f;
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{first,Cleared(Vector3.right*10),last},
                new[]{Vector3.zero,Vector3.right*10,Vector3.right*20},MapRouteFactory.Chain(3));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent));
            var raid=World.Root("Route raid").AddComponent<RaidFlowController>();
            var model=new SceneRaidReadModel(new SceneRaidIdentityMap(),null);
            var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add; yield return null;
            Time.timeScale=4; Assert.That(Time.timeScale,Is.EqualTo(4)); Send(agent,"n2");
            yield return RuntimeWait.Until(()=>agent!=null&&agent.RouteSnapshot.StepIndex==1,"穿过中途物理撤离碰撞");
            Assert.That(model.Capture().extractedAgents,Is.Empty);
            yield return RuntimeWait.Until(()=>raid.IsInputLocked,"最终撤离真实结算",15);
            var snapshot=model.Capture(); Assert.That(snapshot.missionCompleted,Is.True); CollectionAssert.AreEqual(new[]{"1"},snapshot.settledAgents);
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Extracted),Is.EqualTo(1));
            Assert.That(results.Any(x=>x.Stage==AgentRouteStage.Completed),Is.False);
            ContractCompleted=true;
        }

        [UnityTest] public IEnumerator FinalExtractionRetaliationResumesAtNormalSpeed() { yield return ExtractionRetaliation(1); }
        [UnityTest] public IEnumerator FinalExtractionRetaliationResumesAtFourTimesSpeed() { yield return ExtractionRetaliation(4); }
        private IEnumerator ExtractionRetaliation(float speed)
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=speed;
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,8,false,false); InventoryFactory.Create(World);
            var exit=TargetFactory.Extraction(World,Vector3.zero);
            var point=exit.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>(); point.ExtractionDurationSeconds=1;
            point.GetComponent<BoxCollider>().size=new Vector3(20,8,20);
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{exit},new[]{Vector3.zero});
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent));
            var raid=World.Root("Retaliation extraction raid").AddComponent<RaidFlowController>();
            var progress=RuntimeFixtureAccess.Read<IDictionary>(raid,"_activeExtractionProgressByAgentId"); yield return null;
            Time.timeScale=speed; Assert.That(Time.timeScale,Is.EqualTo(speed));
            CaseArtifactWriter.Trace("extraction-speed", "scale="+Time.timeScale);
            Send(agent,"n0"); yield return RuntimeWait.Until(()=>progress.Contains("1"),"正式最终撤离计时");
            var root=agent.RouteSnapshot.Request.RequestId; var enemy=EnemyFactory.Passive(World,new Vector3(5,0,3));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            yield return RuntimeWait.Until(()=>!progress.Contains("1"),"受击清理物理和动作入口的撤离计时");
            double wait=Time.realtimeSinceStartupAsDouble+0.25;
            yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=wait,"仍在物理碰撞内反击");
            Assert.That(raid.IsInputLocked,Is.False); Assert.That(agent.RouteSnapshot.Request.RequestId,Is.EqualTo(root));
            Object.DestroyImmediate(enemy.gameObject);
            yield return RuntimeWait.Until(()=>raid.IsInputLocked,"反击后恢复原终点结算",8);
            Assert.That(new SceneRaidReadModel(new SceneRaidIdentityMap(),null).Capture().missionCompleted,Is.True); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator AutonomousRootAlsoResumesItsRemainingClustersAfterDamage()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false);
            Install(agent,Vector3.zero,Vector3.right*12,Vector3.right*24); Send(agent,"n2","auto-root",AgentRouteSource.Autonomous);
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.StepIndex==1,"自主路线进入下一群");
            var before=agent.RouteSnapshot;
            var enemy=EnemyFactory.Passive(World,agent.Position+Vector3.forward*10);
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            yield return RuntimeWait.Until(()=>agent.CurrentMacroStateId==AgentMacroStateId.Combat,"自主路线受击反击");
            Assert.That(agent.RouteSnapshot.CurrentStep.IsRetaliating,Is.True);
            Assert.That(agent.RouteSnapshot.StepIndex,Is.EqualTo(before.StepIndex));
            Object.DestroyImmediate(enemy.gameObject); yield return Finished(agent); AssertComplete(agent);
            Assert.That(agent.RouteSnapshot.Request.RequestId,Is.EqualTo("auto-root"));
            Assert.That(agent.RouteSnapshot.RouteVersion,Is.EqualTo(before.RouteVersion)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DisablingActorCancelsActiveRootAndPendingReplacementOnce()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,0,false,false);
            Install(agent,Vector3.zero,Vector3.right*12); var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add;
            Send(agent,"n1","active"); yield return RuntimeWait.Until(()=>agent.RouteSnapshot.IsActive,"停用前根已接受");
            var old=agent.DirectiveLifecycle.Active.Value;
            Send(agent,"n0","pending"); agent.enabled=false; yield return null;
            Assert.That(agent.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Cancelled));
            Assert.That(agent.RouteSnapshot.HasPendingRequest,Is.False); Assert.That(agent.FinishDirective(old.CommandId),Is.False);
            Assert.That(results.Count(x=>x.Request.RequestId=="active"&&x.Stage==AgentRouteStage.Cancelled),Is.EqualTo(1));
            Assert.That(results.Count(x=>x.Request.RequestId=="pending"&&x.Stage==AgentRouteStage.Rejected),Is.EqualTo(1));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator UnreadyCostsEndPlanningWithinTheWallClockLimit()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,0,false,false);
            var binding=Install(agent,Vector3.zero); var ready=MapRouteFactory.Environment(binding,agent);
            agent.ConfigureRoutes(new AgentRouteEnvironment(ready.Graph,ready.GraphRevision,2,ready.Costs,ready.Targets,ready.Profile,false));
            var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add;
            Send(agent,"n0");
            yield return RuntimeWait.Until(()=>results.Any(x=>x.Stage==AgentRouteStage.Rejected),"导航成本等待有墙钟截止",10);
            Assert.That(results.Single(x=>x.Stage==AgentRouteStage.Rejected).Reason,Is.EqualTo(AgentRouteFailure.NavigationNotReady));
            Assert.That(agent.RoutePlanningQueryCount,Is.Zero); Assert.That(agent.RouteSnapshot.HasPendingRequest,Is.False);
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator CapacityEndsPlayerRootAndWithdrawalTraversesResourcesWithoutTakingLoot()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false); var screen=InventoryFactory.Create(World);
            var item=Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase").Items.First(x=>x.IncludeInRuntimeDatabase&&x.Width==1&&x.Height==1&&x.Type!=ItemType.Bag);
            LootBoxEntity Box(string name,float x) {
                var box=World.Root(name).AddComponent<LootBoxEntity>(); box.transform.position=Vector3.right*x; box.UseBoardGameResourceRules=false;
                box.SaveRuntimeState(new List<ContainerItemSaveData>{new ContainerItemSaveData{ItemData=item,Amount=1}},new List<ContainerCellStateSaveData>()); return box;
            }
            var first=Box("Full source",5); var second=Box("Withdrawal source",14);
            var exit=TargetFactory.Extraction(World,Vector3.right*24); exit.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds=0.3f;
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{TargetFactory.ResourceBoxes(World,first),TargetFactory.ResourceBoxes(World,second),exit},
                new[]{Vector3.right*3,Vector3.right*12,Vector3.right*24},MapRouteFactory.Chain(3));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent)); var raid=World.Root("Capacity route raid").AddComponent<RaidFlowController>();
            var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add; yield return null;
            Time.timeScale=4; Assert.That(Time.timeScale,Is.EqualTo(4)); Send(agent,"n0","player-source");
            yield return RuntimeWait.Until(()=>agent.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"玩家根等待背包");
            first.Interact();
            for(int y=0;y<6;y++) for(int x=0;x<5;x++) Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item,screen.BackpackGrid,x,y,item.IsStackable?item.MaxStack:1),Is.Not.Null);
            var view=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x=>x.CurrentGrid==screen.ActiveExternalGrid);
            yield return RuntimeWait.Until(()=>view.IsInteractionReady,"容量关闭前搜索完成"); screen.CloseInventory();
            yield return Finished(agent,"容量结束玩家根");
            Assert.That(agent.RouteSnapshot.Failure,Is.EqualTo(AgentRouteFailure.CapacityExtraction));
            Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(agent),Is.False);
            int extraSearches=0;
            agent.DirectiveLifecycle.ResultPublished+=result=> {if(result.Accepted&&result.Request.DirectiveType==AgentDirectiveType.Search) extraSearches++;};
            Send(agent,"n2","withdrawal",AgentRouteSource.Autonomous);
            yield return RuntimeWait.Until(()=>raid.IsInputLocked,"容量撤离沿途资源免处理",15);
            Assert.That(new SceneRaidReadModel(new SceneRaidIdentityMap(),null).Capture().missionCompleted,Is.True);
            Assert.That(first.GetSavedItems().Single().Amount,Is.EqualTo(1)); Assert.That(second.GetSavedItems().Single().Amount,Is.EqualTo(1));
            Assert.That(extraSearches,Is.Zero); Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Extracted),Is.EqualTo(1)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator FailedStorageEndsRootsOnceAndRetainsActorAndInventory()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"1",Vector3.left*5,8,false,false); var screen=InventoryFactory.Create(World);
            var exit=TargetFactory.Extraction(World,Vector3.zero); exit.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds=0.1f;
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{exit},new[]{Vector3.zero});
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding,agent)); var raid=World.Root("Storage failure route raid").AddComponent<RaidFlowController>();
            var results=new List<AgentRouteResult>(); agent.RouteResultPublished+=results.Add; yield return null;
            var unknown=World.Own(ScriptableObject.CreateInstance<InventoryItemData>()); unknown.ItemID="UnknownRouteStorageItem"; unknown.Width=unknown.Height=1;
            screen.OpenInventory(); Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(unknown,screen.BackpackGrid,0,0,1),Is.Not.Null); screen.CloseInventory();
            LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Failed to append extraction inventory"));
            Time.timeScale=4; Send(agent,"n0");
            yield return RuntimeWait.Until(()=>raid.IsInputLocked,"存储失败正式锁局");
            Assert.That(agent!=null,Is.True); Assert.That(agent.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Failed));
            Assert.That(agent.RouteSnapshot.Failure,Is.EqualTo(AgentRouteFailure.SettlementFailed));
            Assert.That(screen.TryCollectExtractableItemsForAgent("1",out var remaining,out int count,out _),Is.True);
            Assert.That(count,Is.EqualTo(1)); Assert.That(remaining.Single().ItemData,Is.SameAs(unknown));
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Failed),Is.EqualTo(1));
            Assert.That(results.Any(x=>x.Stage==AgentRouteStage.Extracted),Is.False);
            agent.enabled=false; yield return null;
            Assert.That(results.Count(x=>x.Stage==AgentRouteStage.Failed),Is.EqualTo(1)); ContractCompleted=true;
        }
    }
}
