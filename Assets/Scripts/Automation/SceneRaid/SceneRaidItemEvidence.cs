#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;

namespace AnomalySearch.Automation.SceneRaid
{
    [Serializable]
    public sealed class SceneRaidItemEvidence
    {
        public string itemId, assetName, runtimeId, type;
        public int parentIndex, amount, x, y, width, height, sellPrice;
        public bool rotated, searched;
        public static string Id(InventoryItemData data) => string.IsNullOrWhiteSpace(data.ItemID) ? data.name : data.ItemID.Trim();
        public static SceneRaidItemEvidence[] Flatten(IEnumerable<ContainerItemSaveData> items)
        {
            var rows = new List<SceneRaidItemEvidence>();
            void Append(IEnumerable<ContainerItemSaveData> values, int parent, int depth)
            {
                if (values == null) return;
                if (depth > 64) throw new InvalidOperationException("Inventory evidence nesting exceeds its bound.");
                foreach (var item in values)
                {
                    if (item?.ItemData == null || item.Amount <= 0) throw new InvalidOperationException("Invalid inventory evidence item.");
                    int index = rows.Count;
                    var data = item.ItemData;
                    rows.Add(new SceneRaidItemEvidence { itemId = Id(data), assetName = data.name, runtimeId = item.RuntimeItemId,
                        type = data.Type.ToString(), parentIndex = parent, amount = item.Amount, x = item.X, y = item.Y,
                        width = data.Width, height = data.Height, sellPrice = data.SellPrice, rotated = item.IsRotated, searched = item.IsSearched });
                    Append(item.InternalItems, index, depth + 1);
                }
            }
            Append(items, -1, 0);
            return rows.ToArray();
        }
        public static SceneRaidItemEvidence[] Equipped(InventoryScreenController screen)
        {
            var items = new List<ContainerItemSaveData>();
            foreach (var slot in new[] { screen.HeadSlot, screen.BodySlot, screen.FaceSlot, screen.HeadphoneSlot, screen.TotemSlotA, screen.TotemSlotB })
                if (slot != null && slot.HasEquippedItem && slot.EquippedItemState != null)
                    items.Add(slot.EquippedItemState.CreateSaveDataSnapshot(UnityEngine.Vector2Int.zero, false));
            return Flatten(items);
        }
    }
}
#endif
