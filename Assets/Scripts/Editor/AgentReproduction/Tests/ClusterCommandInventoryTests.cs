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
