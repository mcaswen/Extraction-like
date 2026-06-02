using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 容器内一个物品的运行时摆放数据
/// 持有物品运行时状态和它在当前网格里的位置朝向
/// </summary>
[Serializable]
public sealed class InventoryContainerItemRuntimeState
{
    [SerializeField] private InventoryItemRuntimeState _itemState;
    [SerializeField] private Vector2Int _gridPosition;
    [SerializeField] private bool _isRotated;

    public InventoryItemRuntimeState ItemState => _itemState;
    public Vector2Int GridPosition => _gridPosition;
    public bool IsRotated => _isRotated;

    /// <summary>
    /// 构建一条容器物品摆放数据
    /// </summary>
    /// <param name="itemState">物品运行时状态</param>
    /// <param name="gridPosition">物品所在格子坐标</param>
    /// <param name="isRotated">物品是否旋转摆放</param>
    public InventoryContainerItemRuntimeState(
        InventoryItemRuntimeState itemState,
        Vector2Int gridPosition,
        bool isRotated)
    {
        _itemState = itemState;
        _gridPosition = gridPosition;
        _isRotated = isRotated;
    }

    /// <summary>
    /// 更新物品在容器中的摆放位置
    /// </summary>
    /// <param name="gridPosition">新的格子坐标</param>
    /// <param name="isRotated">新的旋转状态</param>
    public void SetPlacement(Vector2Int gridPosition, bool isRotated)
    {
        _gridPosition = gridPosition;
        _isRotated = isRotated;
    }

    /// <summary>
    /// 导出当前物品摆放的存档快照
    /// </summary>
    /// <returns>容器物品快照</returns>
    public ContainerItemSaveData CreateSaveDataSnapshot()
    {
        return _itemState != null
            ? _itemState.CreateSaveDataSnapshot(_gridPosition, _isRotated)
            : null;
    }
}

/// <summary>
/// 背包容器的运行时数据源
/// 负责维护容器中的物品集合和特殊格状态，避免关闭界面时再从 UI 层反向扫描数据
/// </summary>
[Serializable]
public sealed class InventoryContainerRuntimeState
{
    [SerializeField] private List<InventoryContainerItemRuntimeState> _items = new List<InventoryContainerItemRuntimeState>();
    [SerializeField] private List<ContainerCellStateSaveData> _cellStates = new List<ContainerCellStateSaveData>();

    public IReadOnlyList<InventoryContainerItemRuntimeState> Items => _items;

    /// <summary>
    /// 从容器存档快照构建运行时容器状态
    /// </summary>
    /// <param name="items">容器物品快照</param>
    /// <param name="cellStates">容器特殊格状态快照</param>
    /// <returns>新的运行时容器状态</returns>
    public static InventoryContainerRuntimeState CreateFromSaveData(
        List<ContainerItemSaveData> items,
        List<ContainerCellStateSaveData> cellStates)
    {
        InventoryContainerRuntimeState state = new InventoryContainerRuntimeState();
        state.LoadFromSaveData(items, cellStates);
        return state;
    }

    /// <summary>
    /// 使用容器存档快照覆盖当前运行时状态
    /// </summary>
    /// <param name="items">容器物品快照</param>
    /// <param name="cellStates">容器特殊格状态快照</param>
    public void LoadFromSaveData(
        List<ContainerItemSaveData> items,
        List<ContainerCellStateSaveData> cellStates)
    {
        _items.Clear();

        if (items != null)
        {
            foreach (ContainerItemSaveData item in items)
            {
                if (item?.ItemData == null)
                {
                    continue;
                }

                InventoryItemRuntimeState itemState = new InventoryItemRuntimeState();
                itemState.ApplyContainerSaveData(item);
                RegisterItem(itemState, new Vector2Int(item.X, item.Y), item.IsRotated);
            }
        }

        _cellStates = CloneCellStateList(cellStates);
    }

    /// <summary>
    /// 注册或更新一个物品在容器中的摆放状态
    /// </summary>
    /// <param name="itemState">物品运行时状态</param>
    /// <param name="gridPosition">物品所在格子坐标</param>
    /// <param name="isRotated">物品是否旋转摆放</param>
    public void RegisterItem(
        InventoryItemRuntimeState itemState,
        Vector2Int gridPosition,
        bool isRotated)
    {
        if (itemState == null || itemState.ItemData == null)
        {
            return;
        }

        InventoryContainerItemRuntimeState existingItem = FindItem(itemState);
        if (existingItem != null)
        {
            existingItem.SetPlacement(gridPosition, isRotated);
            return;
        }

        _items.Add(new InventoryContainerItemRuntimeState(itemState, gridPosition, isRotated));
    }

    /// <summary>
    /// 从容器中移除指定物品
    /// </summary>
    /// <param name="itemState">要移除的物品运行时状态</param>
    public void UnregisterItem(InventoryItemRuntimeState itemState)
    {
        if (itemState == null)
        {
            return;
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            InventoryContainerItemRuntimeState item = _items[i];
            if (item == null || ReferenceEquals(item.ItemState, itemState))
            {
                _items.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 覆盖当前容器的特殊格状态
    /// </summary>
    /// <param name="cellStates">新的特殊格状态快照</param>
    public void SetCellStates(List<ContainerCellStateSaveData> cellStates)
    {
        _cellStates = CloneCellStateList(cellStates);
    }

    /// <summary>
    /// 清空容器中的运行时内容
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        _cellStates.Clear();
    }

    /// <summary>
    /// 导出容器中的物品快照
    /// </summary>
    /// <returns>容器物品快照列表</returns>
    public List<ContainerItemSaveData> CreateItemSaveDataSnapshot()
    {
        List<ContainerItemSaveData> snapshots = new List<ContainerItemSaveData>();
        foreach (InventoryContainerItemRuntimeState item in _items)
        {
            ContainerItemSaveData snapshot = item?.CreateSaveDataSnapshot();
            if (snapshot != null && snapshot.ItemData != null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    /// <summary>
    /// 导出容器特殊格状态快照
    /// </summary>
    /// <returns>容器特殊格状态快照列表</returns>
    public List<ContainerCellStateSaveData> CreateCellStateSnapshot()
    {
        return CloneCellStateList(_cellStates);
    }

    private InventoryContainerItemRuntimeState FindItem(InventoryItemRuntimeState itemState)
    {
        foreach (InventoryContainerItemRuntimeState item in _items)
        {
            if (item != null && ReferenceEquals(item.ItemState, itemState))
            {
                return item;
            }
        }

        return null;
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
