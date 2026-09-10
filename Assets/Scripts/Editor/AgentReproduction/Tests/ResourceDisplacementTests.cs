using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ResourceDisplacementTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator DisplacementInvalidatesInventoryCloseUntilAgentReturns()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"R5",Vector3.zero);
            var item=World.Root("Persistent loot").AddComponent<WorldLootItem>();
            item.ItemData=World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.CurrentAmount=1;
            var inventory=World.Root("Inventory").AddComponent<InventoryScreenController>();
            yield return null;
            var request=AgentDirectiveRequest.SearchConcreteResource(item.gameObject,"loot",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("loot"),1000);
            Assert.That(agent.TrySubmitDirective(request).Accepted,Is.True);
            for(int i=0;i<5;i++) yield return null;
            inventory.OpenInventory();
            for(int i=0;i<3;i++) yield return null;
            agent.NavMeshAgent.Warp(new Vector3(8,0,0));
            for(int i=0;i<3;i++) yield return null;
            inventory.CloseInventory();
            for(int i=0;i<3;i++) yield return null;
            Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.False,"Closing after displacement must not complete loot.");
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.True);
            agent.NavMeshAgent.Warp(Vector3.zero);
            for(int i=0;i<3;i++) yield return null;
            Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.False,"Returning alone must not consume stale open observation.");
            inventory.OpenInventory();
            for(int i=0;i<3;i++) yield return null;
            inventory.CloseInventory();
            for(int i=0;i<3;i++) yield return null;
            Assert.That(AgentSearchedResourceRegistry.IsSearched(item.gameObject),Is.True);
            ContractCompleted=true;
        }
    }
}
