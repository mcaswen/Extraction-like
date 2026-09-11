using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AnomalySearch.Automation.SceneRaid;

namespace AgentReproduction.Tests
{
    public sealed class ResourceDisplacementTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator WaitingProbeUsesActualResourceWithoutInventoryAndDeduplicates()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Probe", Vector3.zero);
            var item = World.Root("Actual selected loot").AddComponent<WorldLootItem>();
            item.ItemData = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.CurrentAmount = 1;
            var facts = new List<AgentResourceInteractionEvent>();
            System.Action<AgentResourceInteractionEvent> observe = value => facts.Add(value);
            AgentResourceInteractionChannel.Published += observe;
            try
            {
                yield return null;
                var request = AgentDirectiveRequest.SearchConcreteResource(item.gameObject, "probe", agent.AgentId,
                    AgentManualDirectiveLock.CreateCommandId("probe"), 1000);
                Assert.That(agent.TrySubmitDirective(request).Accepted, Is.True);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(facts.Select(x => x.Stage), Is.EqualTo(new[] {
                    AgentResourceInteractionStage.Approaching, AgentResourceInteractionStage.WaitingForInventory }));
                Assert.That(facts.All(x => x.Resource == item.gameObject && x.AgentId == agent.AgentIdValue &&
                    x.CommandId == request.CommandId), Is.True, "Probe identity must come from the active node.");
                Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject), Is.False,
                    "Observing arrival must not complete loot or require a UI instance.");
                var model = new SceneRaidReadModel(new SceneRaidIdentityMap(), id => facts.Last());
                var snapshot = model.Capture();
                var state = snapshot.agents.Single(x => x.id == agent.AgentIdValue);
                Assert.That(state.hasEnemy, Is.False);
                Assert.That(state.hasResource, Is.True);
                Assert.That(state.resource.phase, Is.EqualTo("WaitingForInventory"));
                var serialized = JsonUtility.FromJson<SceneRaidReadModel.Snapshot>(JsonUtility.ToJson(snapshot));
                Assert.That(serialized.agents.Single(x => x.id == agent.AgentIdValue).hasEnemy, Is.False,
                    "JsonUtility materializes null inline objects; consumers must use explicit validity flags.");
                Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject), Is.False);
                agent.NavMeshAgent.Warp(new Vector3(8, 0, 0));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(facts.Skip(2).Select(x => x.Stage), Is.EqualTo(new[] {
                    AgentResourceInteractionStage.Left, AgentResourceInteractionStage.Approaching }));
                ContractCompleted = true;
            }
            finally { AgentResourceInteractionChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator DisplacementInvalidatesInventoryCloseUntilAgentReturns()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"R5",Vector3.zero);
            var item=World.Root("Persistent loot").AddComponent<LootBoxEntity>();
            item.UseBoardGameResourceRules=false;
            var data=World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            data.ItemID="R5_Displacement";
            item.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData { ItemData=data, Amount=1 } },
                new List<ContainerCellStateSaveData>());
            var inventory=InventoryFactory.Create(World);
            var facts=new List<AgentResourceInteractionEvent>();
            System.Action<AgentResourceInteractionEvent> observe=value => { if(value.AgentId==agent.AgentIdValue) facts.Add(value); };
            AgentResourceInteractionChannel.Published+=observe;
            try
            {
                yield return null;
                Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(agent.AgentIdValue),Is.True);
                yield return null;
                var request=AgentDirectiveRequest.SearchConcreteResource(item.gameObject,"loot",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("loot"),1000);
                Assert.That(agent.TrySubmitDirective(request).Accepted,Is.True);
                yield return RuntimeWait.Until(() => facts.Count>0 && facts.Last().Stage==AgentResourceInteractionStage.WaitingForInventory,"initial resource arrival",3);
                item.Interact();
                Assert.That(inventory.ActiveSessionContext.SourceObject,Is.SameAs(item.gameObject));
                Assert.That(inventory.ActiveSessionContext.AgentId,Is.EqualTo(agent.AgentIdValue));
                for(int i=0;i<3;i++) yield return null;
                agent.NavMeshAgent.Warp(new Vector3(8,0,0));
                yield return RuntimeWait.Until(() => facts.Last().Stage==AgentResourceInteractionStage.Approaching,"displacement invalidates arrival",3);
                inventory.CloseInventory();
                for(int i=0;i<3;i++) yield return null;
                Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.False,"Closing after displacement must not complete loot.");
                Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.True);
                agent.NavMeshAgent.Warp(Vector3.zero);
                yield return RuntimeWait.Until(() => facts.Last().Stage==AgentResourceInteractionStage.WaitingForInventory,"return to resource",3);
                Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.False,"Returning alone must not consume stale open observation.");
                item.Interact();
                Assert.That(inventory.ActiveSessionContext.SourceObject,Is.SameAs(item.gameObject));
                for(int i=0;i<3;i++) yield return null;
                inventory.CloseInventory();
                yield return RuntimeWait.Until(() => AgentSearchedResourceRegistry.IsSearched(item.gameObject),"fresh matching resource session completes",3);
                Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.True);
                ContractCompleted=true;
            }
            finally { AgentResourceInteractionChannel.Published-=observe; }
        }
    }
}
