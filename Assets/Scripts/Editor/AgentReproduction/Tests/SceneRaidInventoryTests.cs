using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidInventoryTests : ReproductionTestFixture
    {
        private InventoryScreenController CreateInventory()
        {
            return InventoryFactory.Create(World);
        }
        private InventoryItemData Item(int width = 1, int height = 1)
        {
            var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.name = "SceneRaid probe"; item.ItemID = "SceneRaidProbe";
            item.Width = width; item.Height = height;
            item.IsStackable = true; item.MaxStack = 10;
            return item;
        }
        private LootBoxEntity Box(string name, Vector3 position, InventoryItemData item, int amount, float duration)
        {
            var root = World.Root(name); root.transform.position = position;
            var box = root.AddComponent<LootBoxEntity>();
            box.UseBoardGameResourceRules = true;
            box.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData
                { ItemData = item, Amount = amount, RequiresSearch = true, IsSearched = false, SearchDurationSeconds = duration } },
                new List<ContainerCellStateSaveData>());
            return box;
        }
        private static DraggableItemUI SourceItem(InventoryScreenController screen) =>
            screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x => x.CurrentGrid == screen.ActiveExternalGrid);

        [UnityTest]
        public IEnumerator QuickTransferWaitsForSearchUsesRotationAndPreservesCloseData()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var data = Item(6, 1);
            InventoryScreenSessionResult closed = null;
            screen.OpenInventorySession(new InventoryScreenSessionContext
            {
                DisplayName = "Search probe", ExternalColumns = 8, ExternalRows = 2,
                ExternalItems = new List<ContainerItemSaveData> { new ContainerItemSaveData
                    { ItemData = data, Amount = 3, RequiresSearch = true, IsSearched = false, SearchDurationSeconds = 0.4f } },
                OnClose = result => closed = result
            });
            var view = SourceItem(screen);
            Assert.That(view.TryQuickTransfer(out var earlyFailure), Is.False);
            Assert.That(earlyFailure, Is.EqualTo(InventoryQuickTransferFailure.SearchPending));
            double deadline = Time.realtimeSinceStartupAsDouble + 4;
            bool transferred = false;
            while (!transferred && Time.realtimeSinceStartupAsDouble < deadline)
            { yield return null; transferred = view.TryQuickTransfer(out _); }
            Assert.That(transferred, Is.True, "Search and reveal must advance through the real UI Update while paused.");
            Assert.That(view.CurrentGrid, Is.SameAs(screen.BackpackGrid));
            Assert.That(view._originalIsRotated, Is.True);
            Assert.That(screen.ActiveExternalGrid.ExtractSaveData(), Is.Empty);
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), data), Is.EqualTo(3));
            screen.CloseInventory();
            Assert.That(closed, Is.Not.Null);
            Assert.That(closed.ExternalItems, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), data), Is.EqualTo(3));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator OversizedItemAndPolicyRejectionPreserveOriginalContainer()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var data = Item(7, 7);
            InventoryScreenSessionResult closed = null;
            screen.OpenInventorySession(new InventoryScreenSessionContext
            {
                ExternalColumns = 8, ExternalRows = 8,
                ExternalItems = new List<ContainerItemSaveData> { new ContainerItemSaveData { ItemData = data, Amount = 1 } },
                OnClose = result => closed = result
            });
            var view = SourceItem(screen);
            Assert.That(view.TryQuickTransfer(out var fullFailure), Is.False);
            Assert.That(fullFailure, Is.EqualTo(InventoryQuickTransferFailure.NoSpace));
            var policy = screen.ActiveExternalGrid.gameObject.AddComponent<InventoryGridInteractionPolicy>();
            policy.AllowItemDragStart = false;
            Assert.That(view.TryQuickTransfer(out var policyFailure), Is.False);
            Assert.That(policyFailure, Is.EqualTo(InventoryQuickTransferFailure.GridPolicy));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.ActiveExternalGrid.ExtractSaveData(), data), Is.EqualTo(1));
            screen.CloseInventory();
            Assert.That(closed.ExternalItems.Single().ItemData, Is.SameAs(data));
            Assert.That(closed.ExternalItems.Single().Amount, Is.EqualTo(1));
            Assert.That(screen.BackpackGrid.ExtractSaveData().Any(x => x.ItemData == data), Is.False);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DriverServesTwoWaitingAgentsWithoutCrossingInventories()
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", Vector3.zero);
            var second = AgentFactory.Create(World, "2", new Vector3(8, 0, 0));
            var screen = CreateInventory();
            yield return null;
            var firstItem = Item(); firstItem.ItemID = "FirstAgentLoot";
            var secondItem = Item(); secondItem.ItemID = "SecondAgentLoot";
            var firstBox = Box("First box", Vector3.zero, firstItem, 2, 0.2f);
            var secondBox = Box("Second box", new Vector3(8, 0, 0), secondItem, 3, 0.2f);
            using var writer = new SceneRaidEvidenceWriter(Path.Combine(TestRunContext.Load().outputPath, "two-agent-inventory"));
            var identities = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identities);
            using var driver = new SceneRaidInventoryDriver(writer, identities, observer.LatestResource);
            Time.timeScale = 4;
            Assert.That(first.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(firstBox.gameObject, "first", first.AgentId,
                AgentManualDirectiveLock.CreateCommandId("first"), 1000)).Accepted, Is.True);
            Assert.That(second.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(secondBox.gameObject, "second", second.AgentId,
                AgentManualDirectiveLock.CreateCommandId("second"), 1000)).Accepted, Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (driver.CompletedSessions < 2 && Time.realtimeSinceStartupAsDouble < deadline)
            { yield return null; driver.Tick(); }
            Assert.That(driver.BlockedReason, Is.Null);
            Assert.That(driver.ServedAgents.OrderBy(x => x), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(firstBox.GetSavedItems(), Is.Empty);
            Assert.That(secondBox.GetSavedItems(), Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(4));
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("1"), Is.True);
            yield return null;
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), firstItem), Is.EqualTo(2));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), secondItem), Is.EqualTo(0));
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2"), Is.True);
            yield return null;
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), secondItem), Is.EqualTo(3));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), firstItem), Is.EqualTo(0));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator UnrelatedInventoryCannotCompleteTheWaitingResource()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var box = Box("Waiting box", Vector3.zero, Item(), 1, 0);
            box.UseBoardGameResourceRules = false;
            var other = Box("Other box", Vector3.zero, Item(), 1, 0);
            Assert.That(agent.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(box.gameObject, "waiting", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("waiting"), 1000)).Accepted, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            screen.OpenInventory();
            yield return null;
            screen.CloseInventory();
            yield return null;
            Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject), Is.False, "Plain backpack is not the waiting box.");
            other.Interact();
            yield return null;
            screen.CloseInventory();
            yield return null;
            Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject), Is.False, "Another box cannot complete this search.");
            box.Interact();
            yield return null;
            screen.CloseInventory();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject), Is.True, "The matching session still completes normally.");
            Assert.That(box.GetSavedItems().Count, Is.EqualTo(1), "Closing must preserve loot the player left behind.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator PartialStackTransferPreservesRemainderAndDetectsCapacity()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var item = Item(5, 6);
            var box = Box("Stack remainder", Vector3.zero, item, 5, 0);
            box.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData { ItemData = item, Amount = 5 } }, new List<ContainerCellStateSaveData>());
            screen.OpenLootBox(box);
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item, screen.BackpackGrid, 0, 0, 8), Is.Not.Null);
            var source = SourceItem(screen);
            Assert.That(InventoryLootCapacityAssessment.Evaluate(screen), Is.EqualTo(InventoryLootCapacity.CanTransfer));
            Assert.That(source.TryQuickTransfer(out _), Is.True);
            Assert.That(source.CurrentAmount, Is.EqualTo(3));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item), Is.EqualTo(10));
            Assert.That(source.TryQuickTransfer(out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(InventoryQuickTransferFailure.NoSpace));
            var session = screen.ActiveSessionContext;
            screen.CloseInventory();
            Assert.That(session.CloseResult.LootCapacity, Is.EqualTo(InventoryLootCapacity.CapacityBlocked));
            Assert.That(box.GetSavedItems().Single().Amount, Is.EqualTo(3));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CapacityChecksEveryCandidateAndRejectsOversizedOrPolicyOnly()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var large = Item(7, 7); var small = Item();
            screen.OpenInventorySession(new InventoryScreenSessionContext { ExternalColumns = 8, ExternalRows = 8,
                ExternalItems = new List<ContainerItemSaveData> {
                    new ContainerItemSaveData { ItemData = large, Amount = 1 },
                    new ContainerItemSaveData { ItemData = small, Amount = 1, X = 7 } } });
            Assert.That(InventoryLootCapacityAssessment.Evaluate(screen), Is.EqualTo(InventoryLootCapacity.CanTransfer));
            var view = screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(x => x.ItemData == small);
            Assert.That(view.TryQuickTransfer(out _), Is.True);
            Assert.That(InventoryLootCapacityAssessment.Evaluate(screen), Is.EqualTo(InventoryLootCapacity.NoCompatibleItems));
            screen.ActiveExternalGrid.gameObject.AddComponent<InventoryGridInteractionPolicy>().AllowItemDragStart = false;
            Assert.That(InventoryLootCapacityAssessment.Evaluate(screen), Is.EqualTo(InventoryLootCapacity.NoCompatibleItems));
            screen.CloseInventory();
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator FragmentedBackpackUsesFormalSortingBeforeCapacityFailure()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var peg = Item(); peg.IsStackable = false;
            var large = Item(3, 3);
            screen.OpenInventorySession(new InventoryScreenSessionContext { ExternalColumns = 4, ExternalRows = 4,
                ExternalItems = new List<ContainerItemSaveData> { new ContainerItemSaveData { ItemData = large, Amount = 1 } } });
            for (int y = 0; y < 6; y++) Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(peg, screen.BackpackGrid, 2, y, 1), Is.Not.Null);
            Assert.That(SourceItem(screen).TryQuickTransfer(out _), Is.False);
            Assert.That(InventoryLootCapacityAssessment.Evaluate(screen), Is.EqualTo(InventoryLootCapacity.CanTransferAfterSorting));
            screen.BackpackGrid.AutoSort();
            Assert.That(SourceItem(screen).TryQuickTransfer(out _), Is.True);
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), peg), Is.EqualTo(6));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), large), Is.EqualTo(1));
            screen.CloseInventory();
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator FullAgentChoosesExitWhileOtherAgentKeepsSearching()
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", Vector3.zero, 8, true, false);
            var second = AgentFactory.Create(World, "2", new Vector3(0, 0, 8), 0, true, false);
            var screen = CreateInventory();
            var exit = TargetFactory.Extraction(World, new Vector3(10, 0, 0));
            TargetFactory.Resources(World, new Vector3(0, 0, 9));
            yield return null;
            var item = Item(); item.IsStackable = false;
            var box = Box("Full agent box", Vector3.zero, item, 1, 0);
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("1"), Is.True);
            yield return null;
            Assert.That(first.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(box.gameObject, "full", first.AgentId,
                AgentManualDirectiveLock.CreateCommandId("full"), 1000)).Accepted, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            box.Interact();
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 5; x++)
                    Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item, screen.BackpackGrid, x, y, 1), Is.Not.Null);
            yield return RuntimeWait.Until(() => SourceItem(screen).IsInteractionReady, "real loot search", 5);
            screen.CloseInventory();
            yield return RuntimeWait.Until(() => first.DirectiveLifecycle.Active?.DirectiveType == AgentDirectiveType.Extract, "autonomous capacity extraction", 5);
            Assert.That(first.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.InventoryRequiresExtraction), Is.True);
            Assert.That(first.DirectiveLifecycle.Active.Value.TargetObject, Is.SameAs(exit.ExtractionMembers[0].EntityObject));
            Assert.That(box.GetSavedItems().Single().Amount, Is.EqualTo(1));
            Assert.That(box.IsResourcePointLooted, Is.False);
            Assert.That(AgentSearchedResourceRegistry.IsSearched(box.gameObject), Is.False);
            Assert.That(second.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.InventoryRequiresExtraction), Is.False);
            yield return RuntimeWait.Until(() => second.DirectiveLifecycle.Active?.DirectiveType == AgentDirectiveType.Search, "other agent continues search", 5);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator InvalidatedResourceClosesSessionAndReopeningResumesRealSearch()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero);
            var screen = CreateInventory();
            yield return null;
            var item = Item();
            var box = Box("Invalidation box", Vector3.zero, item, 1, 1.5f);
            using var writer = new SceneRaidEvidenceWriter(Path.Combine(TestRunContext.Load().outputPath, "invalidated-inventory"));
            var identities = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identities);
            using var driver = new SceneRaidInventoryDriver(writer, identities, observer.LatestResource);
            Assert.That(agent.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(box.gameObject, "box", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("old"), 1000)).Accepted, Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 6;
            while (!screen.IsInventoryOpen && Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(screen.IsInventoryOpen, Is.True);
            while (SourceItem(screen).SearchProgressSeconds < 0.2f && Time.realtimeSinceStartupAsDouble < deadline)
            { yield return null; driver.Tick(); }
            box.gameObject.SetActive(false);
            while (screen.IsInventoryOpen && Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(screen.IsInventoryOpen, Is.False);
            var saved = box.GetSavedItems().Single();
            Assert.That(saved.SearchProgressSeconds, Is.GreaterThan(0).And.LessThan(saved.SearchDurationSeconds));
            Assert.That(saved.IsSearched, Is.False);
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item), Is.EqualTo(0));
            box.gameObject.SetActive(true);
            Assert.That(agent.TrySubmitDirective(AgentDirectiveRequest.SearchConcreteResource(box.gameObject, "box", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("new"), 1000)).Accepted, Is.True);
            deadline = Time.realtimeSinceStartupAsDouble + 6;
            while (driver.CompletedSessions == 0 && Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(driver.CompletedSessions, Is.EqualTo(1));
            Assert.That(box.GetSavedItems(), Is.Empty);
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item), Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            ContractCompleted = true;
        }
    }
}
