using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using ExtractionLike.Environment;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class RecoveryCaseOpeningTests : ReproductionTestFixture
    {
        LootBoxEntity Case(out RecoveryCaseOpeningFeedback feedback)
        {
            var box = World.Root("Articulated recovery case").AddComponent<LootBoxEntity>();
            box.UseBoardGameResourceRules = true;
            var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.ItemID = "Recovery_case_test"; item.ItemName = "测试样本";
            box.SaveRuntimeState(new List<ContainerItemSaveData> {
                new ContainerItemSaveData { RuntimeItemId="case_item", ItemData=item, Amount=1,
                    RequiresSearch=true, IsSearched=false, SearchDurationSeconds=8 }
            }, new List<ContainerCellStateSaveData>());
            var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                AerospaceSceneLayoutBuilder.RouteArtAssets+"/Prefabs/RecoveryCase.prefab"), box.transform);
            feedback = visual.GetComponent<RecoveryCaseOpeningFeedback>();
            Assert.That(feedback, Is.Not.Null);
            return box;
        }
        static IEnumerator Open(RecoveryCaseOpeningFeedback feedback) => RuntimeWait.Until(() => feedback.IsOpen, "case opening completion", 4);

        [UnityTest] public IEnumerator StateLampDimsOnlyAfterLootingWithoutClosingOrReopening()
        {
            var box=Case(out var feedback);yield return null;
            float closed=feedback.SignalEmission.maxColorComponent;
            feedback.BeginOpening();yield return Open(feedback);
            float opened=feedback.SignalEmission.maxColorComponent;
            Assert.That(opened,Is.GreaterThan(closed));
            Assert.That(box.GetSavedItems().Single().Amount,Is.EqualTo(1));
            box.SaveRuntimeState(new List<ContainerItemSaveData>(),new List<ContainerCellStateSaveData>());
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(feedback.SignalEmission.maxColorComponent,Is.LessThan(closed*.1f));
            Assert.That(feedback.IsOpen,Is.True);Assert.That(feedback.OpeningCount,Is.EqualTo(1));
            Assert.That(box.ResourceState,Is.EqualTo(SceneResourceStateType.Looted));ContractCompleted=true;
        }

        [UnityTest] public IEnumerator CompactVisualStillOpensBeforeSearchAndPreservesLoot()
        {
            var box=Case(out var feedback);var inventory=InventoryFactory.Create(World);
            feedback.transform.localScale=Vector3.one*AerospaceSceneLayoutBuilder.CompactRecoveryCaseScale;
            yield return null;box.Interact();
            Assert.That(inventory.IsWaitingForCaseOpening,Is.True);
            Assert.That(inventory.IsInventoryOpen,Is.False);
            Assert.That(box.GetSavedItems().Single().SearchProgressSeconds,Is.Zero);
            yield return RuntimeWait.Until(()=>inventory.IsInventoryOpen,"compact case ready after lid opening",4);
            Assert.That(feedback.IsOpen,Is.True);Assert.That(feedback.OpeningCount,Is.EqualTo(1));
            Assert.That(feedback.transform.localScale,Is.EqualTo(Vector3.one*.75f));
            Assert.That(inventory.ActiveSessionContext.SourceObject,Is.SameAs(box.gameObject));
            Assert.That(box.GetSavedItems().Single().Amount,Is.EqualTo(1));
            inventory.CloseInventory();ContractCompleted=true;
        }

        [UnityTest] public IEnumerator OneShotOpeningFinishesWithPausedGameAndPreservesItems()
        {
            var box = Case(out var feedback);
            Assert.That(RecoveryCaseOpeningFeedback.EnsureOpen(box.gameObject), Is.False);
            for (int i=0;i<20;i++) Assert.That(RecoveryCaseOpeningFeedback.EnsureOpen(box.gameObject), Is.False);
            Assert.That(feedback.OpeningCount, Is.EqualTo(1));
            Time.timeScale=0;
            yield return Open(feedback);
            Assert.That(RecoveryCaseOpeningFeedback.EnsureOpen(box.gameObject), Is.True);
            Assert.That(feedback.OpeningCount, Is.EqualTo(1));
            var item=box.GetSavedItems().Single();
            Assert.That(item.IsSearched, Is.False);Assert.That(item.SearchProgressSeconds, Is.Zero);Assert.That(item.Amount, Is.EqualTo(1));
            Assert.That(feedback.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(feedback.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            ContractCompleted=true;
        }

        [UnityTest] public IEnumerator DirectInteractionOpensLidBeforeInventoryAndSearchProgress()
        {
            var box=Case(out var feedback);var inventory=InventoryFactory.Create(World);
            yield return null;
            box.Interact();
            Assert.That(inventory.IsWaitingForCaseOpening, Is.True);
            Assert.That(inventory.IsInventoryOpen, Is.False);Assert.That(inventory.ActiveSessionContext, Is.Null);
            Assert.That(box.ResourceState, Is.EqualTo(SceneResourceStateType.Unsearched));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(inventory.IsInventoryOpen, Is.False);
            Assert.That(box.GetSavedItems().Single().SearchProgressSeconds, Is.Zero);
            yield return RuntimeWait.Until(()=>inventory.IsInventoryOpen,"post-animation inventory",4);
            Assert.That(feedback.IsOpen, Is.True);
            Assert.That(inventory.ActiveSessionContext.SourceObject, Is.EqualTo(box.gameObject));
            Assert.That(Time.timeScale, Is.Zero);
            inventory.CloseInventory();
            box.Interact();
            Assert.That(inventory.IsInventoryOpen, Is.True,"Already-open cases do not wait again.");
            Assert.That(feedback.OpeningCount, Is.EqualTo(1));
            inventory.CloseInventory();ContractCompleted=true;
        }

        [UnityTest] public IEnumerator CancelPendingInteractionDoesNotPopInventoryLater()
        {
            var box=Case(out var feedback);var inventory=InventoryFactory.Create(World);yield return null;
            box.Interact();yield return null;
            inventory.CloseInventory();
            Assert.That(inventory.IsWaitingForCaseOpening, Is.False);
            yield return Open(feedback);yield return null;
            Assert.That(inventory.IsInventoryOpen, Is.False);Assert.That(inventory.ActiveSessionContext, Is.Null);
            Assert.That(box.ResourceState, Is.EqualTo(SceneResourceStateType.Unsearched));
            box.Interact();Assert.That(inventory.IsInventoryOpen, Is.True);
            inventory.CloseInventory();ContractCompleted=true;
        }

        [UnityTest] public IEnumerator ChangingFocusDuringOpeningCannotOpenForAnotherAgent()
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"CaseA",Vector3.zero);
            var b=AgentFactory.Create(World,"CaseB",Vector3.right);
            var box=Case(out var feedback);var inventory=InventoryFactory.Create(World);yield return null;
            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(a.AgentIdValue);yield return null;
            box.Interact();Assert.That(inventory.IsWaitingForCaseOpening, Is.True);
            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(b.AgentIdValue);
            yield return Open(feedback);yield return null;
            Assert.That(inventory.IsInventoryOpen, Is.False);Assert.That(inventory.IsWaitingForCaseOpening, Is.False);
            ContractCompleted=true;
        }

        [UnityTest] public IEnumerator TwoAgentsShareOneOpeningAndReceiveReadyOnlyAfterOpen()
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"OpeningA",new Vector3(-3f,0,0),8);
            var b=AgentFactory.Create(World,"OpeningB",new Vector3(3f,0,0),8);
            var box=Case(out var feedback);
            box.gameObject.AddComponent<BoxCollider>().size=new Vector3(4,2,4);
            var events=new List<AgentResourceInteractionEvent>();bool earlyReady=false;
            System.Action<AgentResourceInteractionEvent> observe=e=>{
                if(e.Resource!=box.gameObject)return;events.Add(e);
                if(e.Stage==AgentResourceInteractionStage.WaitingForInventory && !feedback.IsOpen)earlyReady=true;
            };
            AgentResourceInteractionChannel.Published+=observe;
            try
            {
                yield return null;
                foreach(var agent in new[]{a,b}) Assert.That(agent.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(
                    box.gameObject,"case",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("case"),1000)).Accepted,Is.True);
                yield return RuntimeWait.Until(()=>feedback.IsOpening,"arrival before searching",3);
                Assert.That(events.Any(e=>e.Stage==AgentResourceInteractionStage.WaitingForInventory),Is.False);
                Assert.That(box.GetSavedItems().Single().SearchProgressSeconds,Is.Zero);
                yield return RuntimeWait.Until(()=>events.Count(e=>e.Stage==AgentResourceInteractionStage.WaitingForInventory)==2,"both agents ready",5);
                Assert.That(earlyReady,Is.False);Assert.That(feedback.OpeningCount,Is.EqualTo(1));
                Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject),Is.False,"Opening is not looting/completion.");
                ContractCompleted=true;
            }
            finally{AgentResourceInteractionChannel.Published-=observe;}
        }

        [UnityTest] public IEnumerator DisablingVisualMidOpeningCannotDeadlockInteraction()
        {
            var box=Case(out var feedback);feedback.BeginOpening();yield return null;
            feedback.gameObject.SetActive(false);
            Assert.That(RecoveryCaseOpeningFeedback.EnsureOpen(box.gameObject),Is.True);
            feedback.gameObject.SetActive(true);
            Assert.That(feedback.IsOpen,Is.True);Assert.That(feedback.BeginOpening(),Is.True);
            Assert.That(feedback.OpeningCount,Is.EqualTo(1));ContractCompleted=true;
        }

        [UnityTest] public IEnumerator WalkingAwayCancelsPendingDirectInteraction()
        {
            TestNavMeshBuilder.Flat(World);
            var actor=AgentFactory.Create(World,"Walking",Vector3.zero);
            var box=Case(out var feedback);var inventory=InventoryFactory.Create(World);yield return null;
            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(actor.AgentIdValue);yield return null;
            box.Interact();yield return null;actor.NavMeshAgent.Warp(Vector3.right*8);
            yield return Open(feedback);yield return null;
            Assert.That(inventory.IsInventoryOpen,Is.False);Assert.That(inventory.IsWaitingForCaseOpening,Is.False);
            Assert.That(box.GetSavedItems().Single().SearchProgressSeconds,Is.Zero);ContractCompleted=true;
        }

        [UnityTest] public IEnumerator SupersedingAgentSearchDoesNotPublishReadyAfterTheLidFinishes()
        {
            TestNavMeshBuilder.Flat(World);
            var actor=AgentFactory.Create(World,"Cancelled",Vector3.zero,8);
            var box=Case(out var feedback);var events=new List<AgentResourceInteractionEvent>();
            System.Action<AgentResourceInteractionEvent> observe=e=>{if(e.Resource==box.gameObject)events.Add(e);};
            AgentResourceInteractionChannel.Published+=observe;
            try
            {
                yield return null;
                Assert.That(actor.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(box.gameObject,"case",actor.AgentId,
                    AgentManualDirectiveLock.CreateCommandId("cancelled-search"),1000)).Accepted,Is.True);
                yield return RuntimeWait.Until(()=>feedback.IsOpening,"opening starts",3);
                var move=new AgentDirectiveRequest(AgentDirectiveType.MoveTo,
                    AgentTargetRef.FromAbstractPoint(AgentTargetKind.Location,"elsewhere",Vector3.left*8),
                    targetAgentId:actor.AgentId,commandId:AgentManualDirectiveLock.CreateCommandId("cancel-opening"),priority:1001);
                Assert.That(actor.TrySubmitDirective(move).Accepted,Is.True);
                yield return Open(feedback);yield return null;
                Assert.That(events.Any(e=>e.Stage==AgentResourceInteractionStage.Left),Is.True);
                Assert.That(events.Any(e=>e.Stage==AgentResourceInteractionStage.WaitingForInventory),Is.False);
                Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject),Is.False);ContractCompleted=true;
            }
            finally{AgentResourceInteractionChannel.Published-=observe;}
        }
    }
}
