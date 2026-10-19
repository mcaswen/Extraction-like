using System.Collections;
using System.IO;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    /// <summary>周期调用的调试与决策读取不得回写游戏状态。</summary>
    public sealed class DebugReadOnlyTests : ReproductionTestFixture
    {
        // 这些查询会刷新群状态、推进修订号、关闭会话或覆盖射击诊断，周期性调试视图不得调用。
        private static readonly string[] StateChangingQueries =
        {
            "TryGetNearestReachableIncompleteResource", "HasPendingResources", "AppendNavigationDebugSnapshot",
            "GetOrCreate(", "TryGetFacts", "TryEstimateRoute", "TryCopyRouteNodeEnemies", ".CanShootAt(",
            "TryCollectExtractableItemsForAgent", "TryGetExtractionInventorySummary", "RefreshTarget(",
            "SyncEquippedItemRuntimeDataFromGrid"
        };

        [UnityTest]
        public IEnumerator CarriedLootSummaryReadsLiveGridsWithoutSyncingRuntimeState()
        {
            var screen = InventoryFactory.Create(World);
            yield return null;
            screen.GetCurrentBackpackOccupiedCells();
            var slot = screen.BackpackSlot;
            Assert.That(slot != null && slot.HasEquippedItem, Is.True, "夹具应装备默认背包");
            var cheap = Item("summary-probe-a", 35);
            var dear = Item("summary-probe-b", 120);
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(cheap, screen.BackpackGrid, 0, 0, 1), Is.Not.Null);
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(dear, screen.BackpackGrid, 1, 0, 1), Is.Not.Null);
            string agentId = string.IsNullOrEmpty(screen.ActiveInventoryAgentId) ? "Player" : screen.ActiveInventoryAgentId;

            var cachedItems = slot.EquippedItemState.InternalItems;
            Assert.That(screen.TryGetCarriedLootSummary(agentId, out int count, out int value), Is.True);
            Assert.That(slot.EquippedItemState.InternalItems, Is.SameAs(cachedItems), "只读汇总不得回写装备的运行时物品");
            Assert.That(count, Is.EqualTo(2));
            Assert.That(value, Is.EqualTo(155));
            Assert.That(screen.GetCurrentRunRevenueValue(), Is.EqualTo(value), "与同步快照的结算口径一致");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator PeriodicDebugViewAvoidsStateChangingQueries()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Gameplay/Agent/Runtime/AgentRuntimeDebugView.cs"));
            foreach (string query in StateChangingQueries)
                Assert.That(source.Contains(query), Is.False, "调试视图调用了会改变状态的查询：" + query);
            ContractCompleted = true;
            yield return null;
        }

        private InventoryItemData Item(string id, int price)
        {
            var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.ItemID = id;
            item.Width = item.Height = 1;
            item.SellPrice = price;
            item.Type = ItemType.Junk;
            return item;
        }
    }
}
