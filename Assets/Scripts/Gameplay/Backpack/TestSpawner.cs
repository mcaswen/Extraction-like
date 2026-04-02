using System.Collections;
using UnityEngine;

/// <summary>
/// 背包系统调试生成器
/// 仅用于快速搭建测试数据，不参与正式业务流程
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

    /// <summary>
    /// 延迟一帧等待运行时 UI 初始化完成，再注入测试数据
    /// </summary>
    /// <returns>等待初始化完成的协程</returns>
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

    // 往角色背包区域生成一组基础测试物品
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

    // 往右侧战利品区域生成一组测试掉落
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
