using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中的地面掉落物。
/// </summary>
public class WorldLootItem : MonoBehaviour, IInteractable, ISecondaryInteractable
{
    [Header("Runtime Data")]
    public InventoryItemData ItemData;
    public int CurrentAmount;
    public List<ContainerItemSaveData> InternalItems = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> InternalCellStates = new List<ContainerCellStateSaveData>();

    /// <summary>
    /// 初始化一个掉落物实体。
    /// </summary>
    public void InitializeDrop(
        InventoryItemData data,
        int amount,
        List<ContainerItemSaveData> internalItems = null,
        List<ContainerCellStateSaveData> internalCellStates = null)
    {
        ItemData = data;
        CurrentAmount = amount;
        InternalItems = CloneSaveDataList(internalItems);
        InternalCellStates = CloneCellStateList(internalCellStates);
        ApplyRandomImpulse();
    }

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

    public void Interact()
    {
        if (GameUIController.Instance == null || !GameUIController.Instance.TryStoreWorldItem(this))
        {
            return;
        }

        Debug.Log($"Picked up {ItemData.ItemName}.");
        Destroy(gameObject);
    }

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

    public void SecondaryInteract()
    {
        if (ItemData == null || (ItemData.Type != ItemType.Bag && ItemData.Type != ItemType.Rig))
        {
            return;
        }

        if (GameUIController.Instance == null || !GameUIController.Instance.TryEquipWorldContainer(this))
        {
            return;
        }

        Debug.Log($"Equipped {ItemData.ItemName}.");
        Destroy(gameObject);
    }

    private void ApplyRandomImpulse()
    {
        Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent == null)
        {
            return;
        }

        Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f)).normalized;
        rigidbodyComponent.AddForce(randomDirection * 3f, ForceMode.Impulse);
    }

    private static List<ContainerItemSaveData> CloneSaveDataList(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> clone = new List<ContainerItemSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerItemSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }

    private static List<ContainerCellStateSaveData> CloneCellStateList(List<ContainerCellStateSaveData> source)
    {
        List<ContainerCellStateSaveData> clone = new List<ContainerCellStateSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerCellStateSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }
}
