#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    /// <summary>对照正式容器快照核对守恒；不写物品、格子或存档。</summary>
    public sealed class SceneRaidInventoryLedger
    {
        [Serializable] private sealed class ItemRow
        {
            public string itemId, assetName, runtimeId;
            public int amount, x, y;
            public bool rotated, searched;
            public ItemRow[] contents;
        }
        [Serializable] private sealed class Record
        {
            public string agent, resource, stage;
            public ItemRow[] source, backpack;
        }
        private readonly SceneRaidEvidenceWriter _writer;
        private Dictionary<InventoryItemData, long> _initial;
        private string _agent, _resource;
        public SceneRaidInventoryLedger(SceneRaidEvidenceWriter writer) => _writer = writer;
        public void Begin(string agent, string resource, List<ContainerItemSaveData> source, List<ContainerItemSaveData> backpack)
        {
            _agent = agent; _resource = resource;
            _initial = Count(source.Concat(backpack));
            RecordState("opened", source, backpack);
        }
        public void Check(string stage, List<ContainerItemSaveData> source, List<ContainerItemSaveData> backpack)
        {
            RequireSame(_initial, Count(source.Concat(backpack)), "Session inventory conservation");
            RecordState(stage, source, backpack);
        }
        public static void RequireSame(IEnumerable<ContainerItemSaveData> expected, IEnumerable<ContainerItemSaveData> actual, string reason) =>
            RequireSame(Count(expected), Count(actual), reason);
        public static long Amount(IEnumerable<ContainerItemSaveData> values, InventoryItemData item) =>
            values.Where(x => x.ItemData == item).Sum(x => (long)x.Amount);
        private static Dictionary<InventoryItemData, long> Count(IEnumerable<ContainerItemSaveData> values)
        {
            var result = new Dictionary<InventoryItemData, long>();
            void Append(IEnumerable<ContainerItemSaveData> items)
            {
                if (items == null) return;
                foreach (var item in items)
                {
                    if (item == null || item.ItemData == null || item.Amount <= 0)
                        throw new InvalidOperationException("Invalid item in inventory evidence.");
                    result.TryGetValue(item.ItemData, out long amount);
                    result[item.ItemData] = checked(amount + item.Amount);
                    Append(item.InternalItems);
                }
            }
            Append(values);
            return result;
        }
        private static void RequireSame(Dictionary<InventoryItemData, long> expected, Dictionary<InventoryItemData, long> actual, string reason)
        {
            if (expected == null || expected.Count != actual.Count || expected.Any(x => !actual.TryGetValue(x.Key, out long count) || count != x.Value))
                throw new InvalidOperationException(reason + " failed.");
        }
        private static ItemRow[] Rows(IEnumerable<ContainerItemSaveData> items) => items == null ? Array.Empty<ItemRow>() : items.Select(x => new ItemRow
        {
            itemId = x.ItemData.ItemID, assetName = x.ItemData.name, runtimeId = x.RuntimeItemId, amount = x.Amount,
            x = x.X, y = x.Y, rotated = x.IsRotated, searched = x.IsSearched, contents = Rows(x.InternalItems)
        }).ToArray();
        private void RecordState(string stage, List<ContainerItemSaveData> source, List<ContainerItemSaveData> backpack) =>
            _writer.Add("inventory.ledger", JsonUtility.ToJson(new Record
            { agent = _agent, resource = _resource, stage = stage, source = Rows(source), backpack = Rows(backpack) }));
    }
}
#endif
