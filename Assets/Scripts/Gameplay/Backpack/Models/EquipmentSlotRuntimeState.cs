using System.Collections.Generic;

/// <summary>
/// 装备槽的运行时数据
/// 负责记录槽位当前装备的物品状态，避免装备数据只存在于 UI 物体引用上
/// </summary>
public sealed class EquipmentSlotRuntimeState
{
    private InventoryItemRuntimeState _equippedItemState;

    public InventoryItemRuntimeState EquippedItemState => _equippedItemState;
    public bool HasEquippedItem => _equippedItemState != null && _equippedItemState.ItemData != null;

    /// <summary>
    /// 装备指定物品状态
    /// </summary>
    /// <param name="itemState">要装备的物品状态</param>
    public void Equip(InventoryItemRuntimeState itemState)
    {
        _equippedItemState = itemState;
    }

    /// <summary>
    /// 清空当前槽位装备状态
    /// </summary>
    public void Clear()
    {
        _equippedItemState = null;
    }

    /// <summary>
    /// 把关联内部容器的快照写回当前装备物品
    /// </summary>
    /// <param name="internalItems">内部容器物品快照</param>
    /// <param name="internalCellStates">内部容器特殊格状态快照</param>
    public void SyncInternalContainerState(
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        if (!HasEquippedItem)
        {
            return;
        }

        _equippedItemState.InternalItems = internalItems;
        _equippedItemState.InternalCellStates = internalCellStates;
    }
}
