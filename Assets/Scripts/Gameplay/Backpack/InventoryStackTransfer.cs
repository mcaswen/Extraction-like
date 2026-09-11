using UnityEngine;

/// <summary>正式快捷转移和容量评估共用的堆叠规则，不合并带内嵌内容的物品。</summary>
public static class InventoryStackTransfer
{
    public static DraggableItemUI FindDestination(DraggableItemUI source, InventoryUIController target)
    {
        if (source == null || target == null || source.ItemData == null || !source.ItemData.IsStackable ||
            !source.IsInteractionReady || !source.IsContainerCompletelyEmpty()) return null;
        foreach (var item in target.ItemContainer.GetComponentsInChildren<DraggableItemUI>())
            if (item != source && item.CurrentGrid == target && item.ItemData == source.ItemData &&
                item.IsInteractionReady && item.IsContainerCompletelyEmpty() && item.CurrentAmount < item.ItemData.MaxStack)
                return item;
        return null;
    }

    public static bool TryTransfer(DraggableItemUI source, InventoryUIController target)
    {
        var destination = FindDestination(source, target);
        if (destination == null) return false;
        int amount = Mathf.Min(source.CurrentAmount, destination.ItemData.MaxStack - destination.CurrentAmount);
        if (amount <= 0) return false;
        destination.CurrentAmount += amount;
        source.CurrentAmount -= amount;
        destination.UpdateAmountText();
        source.UpdateAmountText();
        if (source.CurrentAmount == 0)
        {
            source.CurrentGrid.GetGridController().RemoveItem(source, source._originalGridIndex.x, source._originalGridIndex.y, source._originalIsRotated);
            source.CurrentGrid = null;
            Object.Destroy(source.gameObject);
        }
        return true;
    }
}
