using System.Collections;
using UnityEngine;

/// <summary>
/// 背包系统调试生成器。
/// 仅用于快速搭建测试数据，不参与正式业务流程。
/// </summary>
public class TestSpawner : MonoBehaviour
{
    [Header("Debug Item Data")]
    public InventoryItemData WeaponData;
    public InventoryItemData AmmoData;
    public InventoryItemData MedkitData;
    public InventoryItemData SmallBagData;

    [Header("Target Panels")]
    public InventoryUIController BackpackPanel;
    public InventoryUIController LootChestPanel;

    private IEnumerator Start()
    {
        yield return null;

        if (InventoryItemFactory.Instance == null)
        {
            Debug.LogError("InventoryItemFactory is missing.");
            yield break;
        }

        if (BackpackPanel == null || LootChestPanel == null)
        {
            Debug.LogError("Debug target panels are missing.");
            yield break;
        }

        SpawnBackpackItems();
        SpawnLootChestItems();
    }

    private void SpawnBackpackItems()
    {
        if (WeaponData != null)
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(WeaponData, BackpackPanel, 0, 0, 1, false);
        }

        if (AmmoData != null)
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(AmmoData, BackpackPanel, 0, 3, 30, false);
        }
    }

    private void SpawnLootChestItems()
    {
        if (AmmoData != null)
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(AmmoData, LootChestPanel, 0, 0, 50, false);
        }

        if (SmallBagData != null)
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(SmallBagData, LootChestPanel, 3, 0, 1, false);
        }
    }
}
