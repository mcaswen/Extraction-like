using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中的地面掉落物
/// </summary>
public class WorldLootItem : MonoBehaviour, IInteractable, ISecondaryInteractable
{
    [Header("Runtime Data")]
    public InventoryItemData ItemData;
    public int CurrentAmount;
    public List<ContainerItemSaveData> InternalItems = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> InternalCellStates = new List<ContainerCellStateSaveData>();

    /// <summary>
    /// 获取主交互提示文本
    /// </summary>
    /// <returns>展示给玩家的主交互文案</returns>
    public string GetPromptText()
    {
        if (ItemData == null)
        {
            return "[F] 拾取";
        }

        if (ItemData.Type == ItemType.Bag || ItemData.Type == ItemType.Rig)
        {
            return $"[F] 收纳 {ItemData.ItemName}";
        }

        if (ItemData.IsStackable)
        {
            return $"[F] 拾取 {ItemData.ItemName} x{CurrentAmount}";
        }

        return $"[F] 拾取 {ItemData.ItemName}";
    }

    /// <summary>
    /// 执行主交互逻辑，尝试把地面物品收纳进角色容器
    /// </summary>
    public void Interact()
    {
        if (InventoryScreenController.Instance == null || !InventoryScreenController.Instance.TryStoreWorldItem(this))
        {
            return;
        }

        Debug.Log($"Picked up {ItemData.ItemName}.");
        Destroy(gameObject);
    }

    /// <summary>
    /// 获取第二交互提示文本
    /// </summary>
    /// <returns>展示给玩家的第二交互文案</returns>
    public string GetSecondaryPromptText()
    {
        if (ItemData == null)
        {
            return string.Empty;
        }

        if (ItemData.Type == ItemType.Bag || ItemData.Type == ItemType.Rig)
        {
            return $"[E] 装备/替换 {ItemData.ItemName}";
        }

        return string.Empty;
    }

    /// <summary>
    /// 执行第二交互逻辑，尝试直接装备或替换容器类掉落物
    /// </summary>
    public void SecondaryInteract()
    {
        if (ItemData == null || (ItemData.Type != ItemType.Bag && ItemData.Type != ItemType.Rig))
        {
            return;
        }

        if (InventoryScreenController.Instance == null || !InventoryScreenController.Instance.TryEquipWorldContainer(this))
        {
            return;
        }

        Debug.Log($"Equipped {ItemData.ItemName}.");
        Destroy(gameObject);
    }

}
