using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using NUnit.Framework;
using UnityEditor;
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
            var staging = World.Root("Inventory fixture", false);
            var canvas = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Canvas.prefab"), staging.transform);
            // 夹具只需要正式背包，不带任务管理器进入合成世界。
            foreach (var raid in canvas.GetComponentsInChildren<RaidFlowController>(true)) Object.DestroyImmediate(raid);
            staging.SetActive(true);
            return canvas.GetComponentInChildren<InventoryScreenController>(true);
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
            Time.timeScale = 2;
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
            Assert.That(Time.timeScale, Is.EqualTo(2));
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
