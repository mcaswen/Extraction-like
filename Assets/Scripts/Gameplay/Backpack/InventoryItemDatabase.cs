using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime lookup table for inventory item definitions.
/// </summary>
[CreateAssetMenu(fileName = "InventoryItemDatabase", menuName = "HardcoreInventory/Item Database")]
public sealed class InventoryItemDatabase : ScriptableObject
{
    private const string DefaultResourcesPath = "Inventory/InventoryItemDatabase";

    public List<InventoryItemData> Items = new List<InventoryItemData>();

    private static InventoryItemDatabase _cachedDatabase;
    private static Dictionary<string, InventoryItemData> _itemsById;

    public static bool TryResolve(string itemId, out InventoryItemData itemData)
    {
        EnsureCache();
        if (string.IsNullOrWhiteSpace(itemId) || _itemsById == null)
        {
            itemData = null;
            return false;
        }

        return _itemsById.TryGetValue(itemId.Trim(), out itemData) && itemData != null;
    }

    public static void SetRuntimeDatabase(InventoryItemDatabase database)
    {
        _cachedDatabase = database;
        _itemsById = null;
        EnsureCache();
    }

    private static void EnsureCache()
    {
        if (_itemsById != null)
        {
            return;
        }

        if (_cachedDatabase == null)
        {
            _cachedDatabase = Resources.Load<InventoryItemDatabase>(DefaultResourcesPath);
        }

#if UNITY_EDITOR
        if (_cachedDatabase == null)
        {
            _cachedDatabase = BuildEditorFallbackDatabase();
        }
#endif

        _itemsById = new Dictionary<string, InventoryItemData>(System.StringComparer.Ordinal);
        if (_cachedDatabase == null || _cachedDatabase.Items == null)
        {
            return;
        }

        foreach (InventoryItemData item in _cachedDatabase.Items)
        {
            RegisterItem(item);
        }
    }

    private static void RegisterItem(InventoryItemData item)
    {
        if (item == null || !item.IncludeInRuntimeDatabase)
        {
            return;
        }

        TryAddKey(item.ItemID, item);
        TryAddKey(item.name, item);
    }

    private static void TryAddKey(string key, InventoryItemData item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null)
        {
            return;
        }

        string normalizedKey = key.Trim();
        if (!_itemsById.ContainsKey(normalizedKey))
        {
            _itemsById.Add(normalizedKey, item);
        }
    }

#if UNITY_EDITOR
    private static InventoryItemDatabase BuildEditorFallbackDatabase()
    {
        InventoryItemDatabase database = CreateInstance<InventoryItemDatabase>();
        string[] guids = AssetDatabase.FindAssets("t:InventoryItemData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            InventoryItemData item = AssetDatabase.LoadAssetAtPath<InventoryItemData>(path);
            if (item != null)
            {
                database.Items.Add(item);
            }
        }

        return database;
    }
#endif
}
