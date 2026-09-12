using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class AgentClusterStepTests : ReproductionTestFixture
    {
        private AgentClusterStepExecutor Step(AgentPawnRoot agent, GameplayTargetClusterAuthoringBase cluster, Vector3 anchor, bool final = true)
        {
            var resolver = new MapGraphRouteTargetResolver(MapRouteFactory.Bind(World, new[]{cluster},new[]{anchor}));
            var step = new AgentClusterStepExecutor(agent,agent.DirectiveLifecycle,resolver);
            Assert.That(step.TryBegin(new AgentDirectiveRouteContext("step-test",1,"n0",0,true),final),Is.True,step.Snapshot.Failure.ToString());
            return step;
        }
        private static IEnumerator Until(AgentClusterStepExecutor step, Func<bool> predicate, string message, double timeout=12)
        { yield return RuntimeWait.Until(() => { step.Tick(); return predicate(); }, message, (float)timeout); }
        private static void Completed(AgentClusterStepExecutor step)
        {
            CaseArtifactWriter.Trace("step-terminal",step.Snapshot.Phase+"; "+step.Snapshot.Failure+"; "+step.Snapshot.CommandId);
            Assert.That(step.Snapshot.Phase,Is.EqualTo(AgentClusterStepPhase.Completed),step.Snapshot.Failure.ToString());
        }
        private LootBoxEntity Box(Vector3 position, out InventoryItemData item)
        {
            item=World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.ItemID="StepLoot"; item.Width=1; item.Height=1; item.IsStackable=false;
            var box=World.Root("Route box").AddComponent<LootBoxEntity>(); box.transform.position=position;
            box.UseBoardGameResourceRules=false;
            box.SaveRuntimeState(new List<ContainerItemSaveData>{new ContainerItemSaveData{ItemData=item,Amount=1}},new List<ContainerCellStateSaveData>());
            return box;
        }

        [UnityTest]
        public IEnumerator PreclearedEnemyClusterStillRequiresPhysicalAnchorArrival()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var enemy=EnemyFactory.Passive(World,Vector3.right*15);
            var cluster=TargetFactory.Enemies(World,enemy); Object.DestroyImmediate(enemy.gameObject);
            using var step=Step(agent,cluster,Vector3.right*12);
            Assert.That(step.Snapshot.Phase,Is.EqualTo(AgentClusterStepPhase.Travelling));
            yield return Until(step,()=>step.Snapshot.IsTerminal,"清空群仍需实际到达");
            Completed(step); Assert.That(agent.Position.x,Is.GreaterThan(11)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ActualCombatClearsAllMembersBeforeCompletingCluster()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8);
            var first=EnemyFactory.Passive(World,new Vector3(8,0,2),40);
            var second=EnemyFactory.Passive(World,new Vector3(20,0,2),40);
            var cluster=TargetFactory.Enemies(World,first,second);
            var engaged=new HashSet<GameObject>();
            void Observe(AgentDirectiveResult result)
            { if(result.Accepted && result.Request.DirectiveType==AgentDirectiveType.Engage) engaged.Add(result.Request.TargetObject); }
            agent.DirectiveLifecycle.ResultPublished+=Observe;
            using var step=Step(agent,cluster,Vector3.right*5);
            yield return Until(step,()=>step.Snapshot.IsTerminal,"真实清掉两名敌人",20);
            Completed(step); Assert.That(cluster.CountLivingEnemies(),Is.Zero);
            Assert.That(engaged.Count,Is.EqualTo(2),"分别处理群内成员，不能一次 Engage 结束整群");
            agent.DirectiveLifecycle.ResultPublished-=Observe; ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator AllyClearingSuspendedMemberPreservesClusterAndProcessesTheNext()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var first=EnemyFactory.Passive(World,new Vector3(8,0,2));
            var second=EnemyFactory.Passive(World,new Vector3(18,0,2));
            var cluster=TargetFactory.Enemies(World,first,second);
            using var step=Step(agent,cluster,Vector3.right*5);
            yield return Until(step,()=>agent.DirectiveLifecycle.Active?.DirectiveType==AgentDirectiveType.Engage,"第一个群成员");
            var selected=agent.DirectiveLifecycle.Active.Value.TargetObject;
            var attacker=EnemyFactory.Passive(World,new Vector3(5,0,-12));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,attacker.gameObject);
            step.Tick(); Assert.That(step.Snapshot.IsRetaliating,Is.True);
            Object.DestroyImmediate(selected); Object.DestroyImmediate(attacker.gameObject);
            yield return Until(step,()=>agent.DirectiveLifecycle.Active?.DirectiveType==AgentDirectiveType.Engage &&
                agent.DirectiveLifecycle.Active.Value.RouteContext.IsValid,"恢复后处理剩余成员");
            Assert.That(step.Snapshot.IsTerminal,Is.False); Assert.That(cluster.CountLivingEnemies(),Is.EqualTo(1));
            Object.DestroyImmediate(agent.DirectiveLifecycle.Active.Value.TargetObject);
            yield return Until(step,()=>step.Snapshot.IsTerminal,"同伴清掉最后敌人");
            Completed(step); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DisabledLivingMemberFailsBoundedlyWithoutPretendingItDied()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var enemy=EnemyFactory.Passive(World,Vector3.right*8); var cluster=TargetFactory.Enemies(World,enemy);
            enemy.gameObject.SetActive(false);
            using var step=Step(agent,cluster,Vector3.right*3);
            yield return Until(step,()=>step.Snapshot.IsTerminal,"暂不可执行成员有限失败",6);
            Assert.That(step.Snapshot.Phase,Is.EqualTo(AgentClusterStepPhase.Failed));
            Assert.That(step.Snapshot.Failure,Is.EqualTo(AgentRouteFailure.NoExecutableMember));
            Assert.That(cluster.CountLivingEnemies(),Is.EqualTo(1)); Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator UnregisteredGroupWaitsThenFailsWithoutInventingCompletion()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var cluster=TargetFactory.Enemies(World);
            using var step=Step(agent,cluster,Vector3.right*3);
            yield return Until(step,()=>step.Snapshot.Phase==AgentClusterStepPhase.WaitingSpawn,"尚未注册的群等待出生");
            Assert.That(step.Snapshot.IsTerminal,Is.False);
            yield return Until(step,()=>step.Snapshot.IsTerminal,"出生等待有截止",7);
            Assert.That(step.Snapshot.Phase,Is.EqualTo(AgentClusterStepPhase.Failed));
            Assert.That(step.Snapshot.Failure,Is.EqualTo(AgentRouteFailure.SpawnFailed)); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ResourceWaitsForFormalInventoryAndCompletesAfterTransfer()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var screen=InventoryFactory.Create(World); var box=Box(Vector3.right*8,out var item);
            var cluster=TargetFactory.ResourceBoxes(World,box); yield return null;
            using var step=Step(agent,cluster,Vector3.right*5);
            yield return Until(step,()=>step.Snapshot.Phase==AgentClusterStepPhase.WaitingForInventory,"等待真实背包操作");
            double hold=Time.realtimeSinceStartupAsDouble+0.3;
            yield return Until(step,()=>Time.realtimeSinceStartupAsDouble>=hold,"未操作时保持等待");
            Assert.That(box.GetSavedItems().Single().Amount,Is.EqualTo(1)); Assert.That(step.Snapshot.IsTerminal,Is.False);
            box.Interact(); Assert.That(screen.IsInventoryOpen,Is.True);
            var view=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x=>x.CurrentGrid==screen.ActiveExternalGrid);
            yield return Until(step,()=>view.IsInteractionReady,"真实搜索就绪");
            Assert.That(view.TryQuickTransfer(out _),Is.True); screen.CloseInventory();
            yield return Until(step,()=>step.Snapshot.IsTerminal,"取物后完成资源群");
            Completed(step); Assert.That(box.GetSavedItems(),Is.Empty);
            Assert.That(screen.BackpackGrid.ExtractSaveData().Single(x=>x.ItemData==item).Amount,Is.EqualTo(1));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator FullInventoryEndsResourceStepAndPreservesRemainingLoot()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var screen=InventoryFactory.Create(World); var box=Box(Vector3.right*8,out var item);
            var cluster=TargetFactory.ResourceBoxes(World,box); yield return null;
            using var step=Step(agent,cluster,Vector3.right*5);
            yield return Until(step,()=>step.Snapshot.Phase==AgentClusterStepPhase.WaitingForInventory,"满包前到箱旁");
            box.Interact();
            for(int y=0;y<6;y++) for(int x=0;x<5;x++)
                Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item,screen.BackpackGrid,x,y,1),Is.Not.Null);
            var view=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x=>x.CurrentGrid==screen.ActiveExternalGrid);
            yield return Until(step,()=>view.IsInteractionReady,"容量判定前真实搜索");
            var session=screen.ActiveSessionContext; screen.CloseInventory();
            Assert.That(session.CloseResult.LootCapacity,Is.EqualTo(InventoryLootCapacity.CapacityBlocked));
            yield return Until(step,()=>step.Snapshot.IsTerminal,"满包结束原步骤");
            Assert.That(step.Snapshot.Failure,Is.EqualTo(AgentRouteFailure.CapacityExtraction));
            Assert.That(box.GetSavedItems().Single().Amount,Is.EqualTo(1)); Assert.That(box.IsResourcePointLooted,Is.False);
            Assert.That(agent.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.InventoryRequiresExtraction),Is.True);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator IntermediateExtractionArrivesWithoutSubmittingExtraction()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var cluster=TargetFactory.Extraction(World,Vector3.right*10); bool extracted=false;
            void Observe(AgentDirectiveResult result) { if(result.Request.DirectiveType==AgentDirectiveType.Extract) extracted=true; }
            agent.DirectiveLifecycle.ResultPublished+=Observe;
            using var step=Step(agent,cluster,Vector3.right*10,false);
            yield return Until(step,()=>step.Snapshot.IsTerminal,"中间撤离节点通行");
            Completed(step); Assert.That(agent.Position.x,Is.GreaterThan(9)); Assert.That(extracted,Is.False);
            agent.DirectiveLifecycle.ResultPublished-=Observe; ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator CancellingOldRootDuringRetaliationDoesNotCancelTheAttacker()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var agent=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var cluster=TargetFactory.Extraction(World,Vector3.right*20);
            using var step=Step(agent,cluster,Vector3.right*20);
            yield return Until(step,()=>agent.Position.x>2,"路线行进");
            var enemy=EnemyFactory.Passive(World,new Vector3(5,0,10));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            var attack=agent.DirectiveLifecycle.Active.Value.CommandId;
            step.Cancel(); Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(attack));
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue,Is.False);
            Object.DestroyImmediate(enemy.gameObject);
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"反击结束不复活旧路线");
            Assert.That(step.Snapshot.Phase,Is.EqualTo(AgentClusterStepPhase.Cancelled)); ContractCompleted=true;
        }
    }
}
