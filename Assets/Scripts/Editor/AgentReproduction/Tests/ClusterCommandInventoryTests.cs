using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ClusterCommandInventoryTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static string[] Replacements = { "Search", "Engage", "Extract" };
        public static bool[] FocusedFirst = { true, false };

        private InventoryItemData Item()
        {
            var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.name = "Cluster command item"; item.ItemID = "ClusterCommandItem";
            item.Width = 1; item.Height = 1; item.IsStackable = true; item.MaxStack = 10;
            return item;
        }

        private LootBoxEntity Box(string name, Vector3 position, InventoryItemData item, float duration)
        {
            var root = World.Root(name); root.transform.position = position;
            var box = root.AddComponent<LootBoxEntity>();
            box.UseBoardGameResourceRules = false;
            box.SaveRuntimeState(new List<ContainerItemSaveData> { new ContainerItemSaveData
                { ItemData = item, Amount = 3, RequiresSearch = true, IsSearched = false, SearchDurationSeconds = duration } },
                new List<ContainerCellStateSaveData>());
            return box;
        }

        [UnityTest]
        public IEnumerator LateRegisteredInventoryDoesNotChangeOpenSessionOrFocus()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            var screen = InventoryFactory.Create(World);
            var defaultBag = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            defaultBag.Type = ItemType.Bag; defaultBag.ContainerColumns = 5; defaultBag.ContainerRows = 6;
            defaultBag.ItemID = "DefaultFixtureBackpack";
            screen.DefaultBackpackItem = defaultBag;
            yield return null;
            var item = Item();
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item, screen.BackpackGrid, 0, 0, 2), Is.Not.Null);
            screen.OpenInventorySession(new InventoryScreenSessionContext { ExternalColumns = 2, ExternalRows = 2 });
            var session = screen.ActiveSessionContext;
            AgentFactory.Create(World, "2", new Vector3(0, 0, 10));
            yield return null;
            Assert.That(screen.ActiveSessionContext, Is.SameAs(session));
            Assert.That(screen.IsInventoryOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AgentRuntimeRegistry.ActiveInstance.FocusedAgentId.Value, Is.EqualTo("1"));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item), Is.EqualTo(2));
            var snapshots = RuntimeFixtureAccess.Read<IDictionary>(screen, "_inventorySnapshotsByAgentId");
            Assert.That(snapshots.Contains("2"), Is.True);
            var defaultInventory = snapshots["2"];
            var bag = RuntimeFixtureAccess.Read<ContainerItemSaveData>(defaultInventory, "BackpackItem");
            Assert.That(bag.ItemData, Is.SameAs(screen.DefaultBackpackItem));
            Assert.That(bag.InternalItems, Is.Empty, "Never clone another agent's contents.");
            screen.CloseInventory();
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator PreviouslyInitializedMissingSnapshotIsNotRecreatedAsEmpty()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            AgentFactory.Create(World, "2", new Vector3(0, 0, 10));
            var screen = InventoryFactory.Create(World);
            yield return null;
            var snapshots = RuntimeFixtureAccess.Read<IDictionary>(screen, "_inventorySnapshotsByAgentId");
            Assert.That(snapshots.Contains("2"), Is.True);
            snapshots.Remove("2"); // 明确的存储缺失故障注入，不能变成合法默认库存。
            yield return null;
            Assert.That(snapshots.Contains("2"), Is.False);
            Assert.That(screen.TryCollectExtractableItemsForAgent("2", out _, out _, out _), Is.False);
            Assert.That(screen.TryCollectExtractableItemsForAgent("unknown", out _, out _, out _), Is.False);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator UntouchedInventoriesSettleRegardlessOfExtractionOrder([ValueSource(nameof(FocusedFirst))] bool focusedFirst)
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            AgentFactory.Create(World, "2", new Vector3(0, 0, 10), 8, false, false);
            var screen = InventoryFactory.Create(World);
            var a = TargetFactory.Extraction(World, new Vector3(focusedFirst ? 10 : 30, 0, 0));
            var b = TargetFactory.Extraction(World, new Vector3(focusedFirst ? -30 : -10, 0, 10));
            a.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds = 0.1f;
            b.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>().ExtractionDurationSeconds = 0.1f;
            World.Root("Real early extraction raid").AddComponent<RaidFlowController>();
            var model = new SceneRaidReadModel(new SceneRaidIdentityMap(), null);
            yield return null;
            Assert.That(AgentRuntimeRegistry.ActiveInstance.FocusedAgentId.Value, Is.EqualTo("1"));
            Assert.That(screen.ActiveInventoryAgentId, Is.EqualTo("1"));
            Assert.That(screen.IsInventoryOpen, Is.False);
            Time.timeScale = 4;
            var dispatcher = new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(a, "1", out _), Is.True);
            Assert.That(dispatcher.TrySubmitClusterCommand(b, "2", out _), Is.True);
            yield return RuntimeWait.Until(() => RaidFlowController.Instance.IsInputLocked, "real early extraction terminal", 10);
            var result = model.Capture();
            Assert.That(result.missionCompleted, Is.True);
            Assert.That(result.missionFailed, Is.False);
            CollectionAssert.AreEquivalent(new[] { "1", "2" }, result.extractedAgents);
            CollectionAssert.AreEquivalent(result.extractedAgents, result.settledAgents);
            Assert.That(screen.IsInventoryOpen, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CommandDuringOpenBoxPreservesOldContentsAndNewTask(
            [ValueSource(nameof(Replacements))] string replacement, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var screen = InventoryFactory.Create(World);
            var item = Item();
            var a = Box("Old box", Vector3.zero, item, 5);
            var b = Box("New box", new Vector3(-25, 0, 0), item, 5);
            var first = TargetFactory.ResourceBoxes(World, a);
            GameplayTargetClusterAuthoringBase next = replacement == "Search" ? TargetFactory.ResourceBoxes(World, b)
                : replacement == "Extract" ? TargetFactory.Extraction(World, new Vector3(-25, 0, 0))
                : TargetFactory.Enemies(World, EnemyFactory.Passive(World, new Vector3(-25, 0, 0)));
            yield return null;
            using var writer = new SceneRaidEvidenceWriter(Path.Combine(TestRunContext.Load().outputPath, "cluster-open-box-" + replacement + speed));
            var identity = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identity);
            using var driver = new SceneRaidInventoryDriver(writer, identity, observer.LatestResource);
            Time.timeScale = speed;
            var dispatcher = new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(first, "1", out var old), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!screen.IsInventoryOpen && Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(screen.IsInventoryOpen, Is.True);
            Assert.That(screen.ActiveSessionContext.SourceObject, Is.SameAs(a.gameObject));
            Assert.That(Time.timeScale, Is.Zero);
            var session = screen.ActiveSessionContext;
            Assert.That(dispatcher.TrySubmitClusterCommand(next, "1", out var current), Is.True);
            Assert.That(agent.FinishDirective(old.CommandId), Is.False);
            while (screen.IsInventoryOpen && Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(screen.IsInventoryOpen, Is.False, "Driver must close a session whose command was replaced.");
            Assert.That(session.IsClosed, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(speed));
            Assert.That(driver.BlockedReason, Is.Null);
            Assert.That(SceneRaidInventoryLedger.Amount(a.GetSavedItems(), item), Is.EqualTo(3));
            Assert.That(SceneRaidInventoryLedger.Amount(b.GetSavedItems(), item), Is.EqualTo(3));
            Assert.That(SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item), Is.Zero);
            Assert.That(AgentSearchedResourceRegistry.IsSearched(b.gameObject), Is.False);
            yield return RuntimeWait.Until(() => agent.Position.x < -3, "new command continues after old close", 5);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(current.CommandId));
            Assert.That(driver.CompletedSessions, Is.Zero, "Invalidated session is not a completed loot task.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator SharedBoxContentsAreCollectedOnceAndBothCommandsRelease()
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", new Vector3(-5, 0, -4), 8, false, false);
            var second = AgentFactory.Create(World, "2", new Vector3(-5, 0, 4), 8, false, false);
            var screen = InventoryFactory.Create(World);
            var item = Item();
            var box = Box("Shared box", new Vector3(10, 0, 0), item, 0.3f);
            var collider = box.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(6, 2, 6);
            var cluster = TargetFactory.ResourceBoxes(World, box);
            yield return null;
            using var writer = new SceneRaidEvidenceWriter(Path.Combine(TestRunContext.Load().outputPath, "cluster-shared-box"));
            var identity = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identity);
            using var driver = new SceneRaidInventoryDriver(writer, identity, observer.LatestResource);
            Time.timeScale = 4;
            var dispatcher = new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(cluster, "1", out _), Is.True);
            Assert.That(dispatcher.TrySubmitClusterCommand(cluster, "2", out _), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while ((first.DirectiveLifecycle.Active.HasValue || second.DirectiveLifecycle.Active.HasValue || screen.IsInventoryOpen) &&
                Time.realtimeSinceStartupAsDouble < deadline) { yield return null; driver.Tick(); }
            Assert.That(driver.BlockedReason, Is.Null);
            Assert.That(first.DirectiveLifecycle.Active.HasValue, Is.False);
            Assert.That(second.DirectiveLifecycle.Active.HasValue, Is.False);
            Assert.That(box.GetSavedItems(), Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(4));
            Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(first), Is.False);
            Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(second), Is.False);
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("1"), Is.True);
            yield return null;
            long firstAmount = SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item);
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2"), Is.True);
            yield return null;
            long secondAmount = SceneRaidInventoryLedger.Amount(screen.BackpackGrid.ExtractSaveData(), item);
            Assert.That(firstAmount + secondAmount, Is.EqualTo(3), "Shared source cannot duplicate loot across agent snapshots.");
            ContractCompleted = true;
        }
    }
}
