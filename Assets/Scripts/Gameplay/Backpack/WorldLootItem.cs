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
    /// 初始化一个掉落物实体
    /// </summary>
    /// <param name="data">掉落物对应的静态物品配置</param>
    /// <param name="amount">掉落数量</param>
    /// <param name="internalItems">容器类掉落物的内部物品快照</param>
    /// <param name="internalCellStates">容器类掉落物的内部格子状态快照</param>
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
        Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance?.NotifyResourceCompleted(gameObject);
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
        Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance?.NotifyResourceCompleted(gameObject);
        Destroy(gameObject);
    }

    // 给新生成的地面掉落物一个轻微随机抛散力，避免物体完全重叠
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

    // 深拷贝掉落物内部物品快照，保证世界掉落和角色容器不会共用引用
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

    // 深拷贝掉落物内部格子状态快照
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
