using System.Collections;
using UnityEngine;

public class TestSpawner : MonoBehaviour
{
    [Header("测试物品数据")]
    public InventoryItemData WeaponData;
    public InventoryItemData AmmoData;
    public InventoryItemData MedkitData;

    // 【新增】：小背包的测试数据
    public InventoryItemData SmallBagData;

    [Header("目标容器面板")]
    public InventoryUIController BackpackPanel;
    public InventoryUIController LootChestPanel;

    IEnumerator Start()
    {
        yield return null;

        if (InventoryItemFactory.Instance == null) { Debug.LogError("缺失工厂！"); yield break; }
        if (BackpackPanel == null || LootChestPanel == null) { Debug.LogError("缺失面板！"); yield break; }

        // === 大背包生成区 ===
        if (WeaponData != null)
            InventoryItemFactory.Instance.SpawnItemInGrid(WeaponData, BackpackPanel, 0, 0, 1, false);

        if (AmmoData != null)
            InventoryItemFactory.Instance.SpawnItemInGrid(AmmoData, BackpackPanel, 0, 3, 30, false);

        // === 宝箱生成区 ===
        if (AmmoData != null)
            InventoryItemFactory.Instance.SpawnItemInGrid(AmmoData, LootChestPanel, 0, 0, 50, false);

        // 【新增】：在宝箱的右侧 [3, 0] 位置，刷出一个 SmallBag！
        if (SmallBagData != null)
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(SmallBagData, LootChestPanel, 3, 0, 1, false);
        }
    }
}