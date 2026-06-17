using System.Collections.Generic;
using UnityEngine;

public static class ShopEconomyPriceUtility
{
    private static readonly HashSet<string> WarnedMissingSellPrices = new HashSet<string>();

    public static int ResolveSellPrice(InventoryItemData itemData, Object context = null)
    {
        if (itemData == null)
        {
            return 0;
        }

        if (itemData.SellPrice > 0)
        {
            return itemData.SellPrice;
        }

        string key = !string.IsNullOrWhiteSpace(itemData.ItemID) ? itemData.ItemID : itemData.name;
        if (!WarnedMissingSellPrices.Contains(key))
        {
            WarnedMissingSellPrices.Add(key);
            Debug.LogWarning($"[ShopEconomy] Item '{key}' has no SellPrice. A temporary test value will be used.", context);
        }

        return ResolveFallbackSellPrice(itemData.Rarity);
    }

    public static int ResolveBuyPrice(TotemShopPoolEntry entry, Object context = null)
    {
        if (entry == null || entry.ItemData == null)
        {
            return 0;
        }

        if (entry.BuyPrice > 0)
        {
            return entry.BuyPrice;
        }

        return Mathf.Max(1, ResolveSellPrice(entry.ItemData, context) * 2);
    }

    private static int ResolveFallbackSellPrice(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return 100;
            case ItemRarity.Rare:
                return 300;
            case ItemRarity.Epic:
                return 800;
            case ItemRarity.Legendary:
                return 1500;
            case ItemRarity.Common:
            default:
                return 50;
        }
    }
}

