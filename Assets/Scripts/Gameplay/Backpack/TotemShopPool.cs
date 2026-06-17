using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TotemShopPool", menuName = "HardcoreInventory/Totem Shop Pool")]
public sealed class TotemShopPool : ScriptableObject
{
    [SerializeField] private List<TotemShopPoolEntry> _entries = new List<TotemShopPoolEntry>();

    public IReadOnlyList<TotemShopPoolEntry> Entries => _entries;

    public List<TotemShopPoolEntry> BuildRuntimeEntries(InventoryItemDatabase database)
    {
        List<TotemShopPoolEntry> result = new List<TotemShopPoolEntry>();
        if (_entries != null)
        {
            foreach (TotemShopPoolEntry entry in _entries)
            {
                if (entry?.ItemData != null &&
                    entry.ItemData.IncludeInRuntimeDatabase &&
                    entry.ItemData.IncludeInTotemShop)
                {
                    result.Add(entry);
                }
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        InventoryItemDatabase source = database != null
            ? database
            : Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase");
        if (source == null || source.Items == null)
        {
            return result;
        }

        foreach (InventoryItemData item in source.Items)
        {
            if (item == null ||
                item.EquipmentKind != EquipmentSlotKind.Totem ||
                !item.IncludeInRuntimeDatabase ||
                !item.IncludeInTotemShop)
            {
                continue;
            }

            result.Add(new TotemShopPoolEntry
            {
                ItemData = item,
                BuyPrice = 0,
                Weight = 1f
            });
        }

        return result;
    }
}

[Serializable]
public sealed class TotemShopPoolEntry
{
    public InventoryItemData ItemData;
    [Min(0)] public int BuyPrice;
    [Min(0.001f)] public float Weight = 1f;
}
