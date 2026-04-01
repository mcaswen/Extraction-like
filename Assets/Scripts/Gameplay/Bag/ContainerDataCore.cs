using System;
using System.Collections.Generic;

/// <summary>
/// 容器内单个物品的存档快照。
/// </summary>
[Serializable]
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
/// 容器内特殊格状态的持久化快照。
/// 用于保留搜索读条、锁定格等非物品占用状态。
/// </summary>
[Serializable]
public class ContainerCellStateSaveData
{
    public int X;
    public int Y;
    public GridState State;

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
/// 所有可打开容器的统一数据接口。
/// </summary>
public interface IInteractableContainer
{
    string GetContainerName();

    List<ContainerItemSaveData> GetSavedItems();

    void SaveItems(List<ContainerItemSaveData> items);
}
