using System.Collections.Generic;
using UnityEngine;

public enum InventoryLootCapacity
{
    Unavailable, Empty, SearchPending, CanTransfer, CanTransferAfterSorting, CapacityBlocked, NoCompatibleItems
}

/// <summary>只读评估一次真实背包会话；算法归属 Backpack，不接管 Agent 撤离。</summary>
public static class InventoryLootCapacityAssessment
{
    public static InventoryLootCapacity Evaluate(InventoryScreenController screen)
    {
        if (screen == null || screen.ActiveSessionContext == null || screen.UsesCustomPlayerInventory ||
            screen.ActiveExternalGrid == null || screen.BackpackGrid == null) return InventoryLootCapacity.Unavailable;
        var source = screen.ActiveExternalGrid;
        var target = screen.BackpackGrid;
        var grid = target.GetGridController();
        var eligible = new List<DraggableItemUI>();
        var empty = new InventoryGridModel();
        empty.Configure(grid.Columns, grid.Rows, grid.BlockedCells, true);
        // 特殊格也不是空位；不能靠评估或整理清除其状态获得容量。
        empty.ApplyRuntimeCellStates(target.ExtractCellStateData());
        bool remaining = false;
        foreach (var item in source.ItemContainer.GetComponentsInChildren<DraggableItemUI>())
        {
            if (item.CurrentGrid != source || item.ItemData == null || item.CurrentAmount <= 0) continue;
            remaining = true;
            if (!item.IsInteractionReady) return InventoryLootCapacity.SearchPending;
            if (!InventoryGridInteractionPolicy.CanBeginDragFrom(source) || !InventoryGridInteractionPolicy.CanDropInto(target) ||
                !item.CanBePlacedInGrid(target)) continue;
            if (InventoryStackTransfer.FindDestination(item, target) != null ||
                grid.FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out _, out _))
                return InventoryLootCapacity.CanTransfer;
            if (empty.FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out _, out _)) eligible.Add(item);
        }
        if (!remaining) return InventoryLootCapacity.Empty;
        if (eligible.Count == 0) return InventoryLootCapacity.NoCompatibleItems;

        // 使用玩家已有整理算法预演，不修改 UI。特殊格存在时不走会清除格状态的旧整理入口。
        if (target.ExtractCellStateData().Count == 0 && InventoryAutoSortService.TryBuildSortedLayout(
            grid.Columns, grid.Rows, grid.BlockedCells, target.ExtractSaveData(), out var sorted))
        {
            foreach (var item in sorted)
                empty.PlaceItem(null, item.X, item.Y, item.IsRotated ? item.ItemData.Height : item.ItemData.Width,
                    item.IsRotated ? item.ItemData.Width : item.ItemData.Height, item.IsRotated);
            foreach (var item in eligible)
                if (empty.FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out _, out _))
                    return InventoryLootCapacity.CanTransferAfterSorting;
        }
        return InventoryLootCapacity.CapacityBlocked;
    }
}
