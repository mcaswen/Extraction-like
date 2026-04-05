using System;
using System.Collections.Generic;

/// <summary>
/// 容器内单个物品的存档快照
/// </summary>
public class ContainerItemSaveData
{
    public string RuntimeItemId;
    public InventoryItemData ItemData;
    public int Amount;
    public int X;
    public int Y;
    public bool IsRotated;
    public bool RequiresSearch;
    public bool IsSearched = true;
    public float SearchProgressSeconds;
    public float SearchDurationSeconds;
    public List<ContainerItemSaveData> InternalItems = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> InternalCellStates = new List<ContainerCellStateSaveData>();

    /// <summary>
    /// 深拷贝当前物品快照，避免多个运行时容器共享同一份引用数据
    /// </summary>
    /// <returns>新的独立物品快照副本</returns>
    public ContainerItemSaveData DeepCopy()
    {
        ContainerItemSaveData copy = new ContainerItemSaveData
        {
            RuntimeItemId = RuntimeItemId,
            ItemData = ItemData,
            Amount = Amount,
            X = X,
            Y = Y,
            IsRotated = IsRotated,
            RequiresSearch = RequiresSearch,
            IsSearched = IsSearched,
            SearchProgressSeconds = SearchProgressSeconds,
            SearchDurationSeconds = SearchDurationSeconds,
            InternalItems = new List<ContainerItemSaveData>(),
            InternalCellStates = new List<ContainerCellStateSaveData>()
        };

        if (InternalItems != null)
        {
            foreach (ContainerItemSaveData internalItem in InternalItems)
            {
                if (internalItem != null)
                {
                    copy.InternalItems.Add(internalItem.DeepCopy());
                }
            }
        }

        if (InternalCellStates != null)
        {
            foreach (ContainerCellStateSaveData cellState in InternalCellStates)
            {
                if (cellState != null)
                {
                    copy.InternalCellStates.Add(cellState.DeepCopy());
                }
            }
        }

        return copy;
    }
}

/// <summary>
/// 容器内特殊格状态的持久化快照
/// 用于保留搜索读条、锁定格等非物品占用状态
/// </summary>
[Serializable]
public class ContainerCellStateSaveData
{
    public int X;
    public int Y;
    public GridState State;

    /// <summary>
    /// 深拷贝当前格子状态快照
    /// </summary>
    /// <returns>新的独立格子状态快照副本</returns>
    public ContainerCellStateSaveData DeepCopy()
    {
        return new ContainerCellStateSaveData
        {
            X = X,
            Y = Y,
            State = State
        };
    }
}

/// <summary>
/// 所有可打开容器的统一数据接口
/// </summary>
public interface IInteractableContainer
{
    /// <summary>
    /// 获取容器名称，用于 UI 或调试展示
    /// </summary>
    /// <returns>当前容器名称</returns>
    string GetContainerName();

    /// <summary>
    /// 获取容器当前保存的物品快照
    /// </summary>
    /// <returns>物品快照列表</returns>
    List<ContainerItemSaveData> GetSavedItems();

    /// <summary>
    /// 用新的物品快照覆盖容器当前内容
    /// </summary>
    /// <param name="items">新的物品快照列表</param>
    void SaveItems(List<ContainerItemSaveData> items);
}
