using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个背包物品的运行时数据
/// 用于承载物品配置、堆叠数量、搜索进度和容器内部内容，避免真实数据散落在 UI View 字段上
/// </summary>
[Serializable]
public sealed class InventoryItemRuntimeState
{
    [SerializeField] private string _runtimeItemId;
    [SerializeField] private InventoryItemData _itemData;
    [SerializeField] private int _amount = 1;
    [SerializeField] private bool _requiresSearch;
    [SerializeField] private bool _isSearched = true;
    [SerializeField] private float _searchProgressSeconds;
    [SerializeField] private float _searchDurationSeconds;
    [SerializeField] private List<ContainerItemSaveData> _internalItems = new List<ContainerItemSaveData>();
    [SerializeField] private List<ContainerCellStateSaveData> _internalCellStates = new List<ContainerCellStateSaveData>();

    public string RuntimeItemId
    {
        get => _runtimeItemId;
        set => _runtimeItemId = value;
    }

    public InventoryItemData ItemData
    {
        get => _itemData;
        set => _itemData = value;
    }

    public int Amount
    {
        get => _amount;
        set => _amount = Mathf.Max(0, value);
    }

    public bool RequiresSearch
    {
        get => _requiresSearch;
        set => _requiresSearch = value;
    }

    public bool IsSearched
    {
        get => _isSearched;
        set => _isSearched = value;
    }

    public float SearchProgressSeconds
    {
        get => _searchProgressSeconds;
        set => _searchProgressSeconds = Mathf.Max(0f, value);
    }

    public float SearchDurationSeconds
    {
        get => _searchDurationSeconds;
        set => _searchDurationSeconds = Mathf.Max(0f, value);
    }

    public List<ContainerItemSaveData> InternalItems
    {
        get => _internalItems;
        set => _internalItems = CloneSaveDataList(value);
    }

    public List<ContainerCellStateSaveData> InternalCellStates
    {
        get => _internalCellStates;
        set => _internalCellStates = CloneCellStateList(value);
    }

    public bool CanInteract => !_requiresSearch || _isSearched;

    /// <summary>
    /// 构建一个普通运行时物品状态
    /// </summary>
    /// <param name="itemData">静态物品配置</param>
    /// <param name="amount">物品数量</param>
    /// <param name="internalItems">容器类物品的内部物品快照</param>
    /// <param name="internalCellStates">容器类物品的内部格子状态快照</param>
    /// <returns>新的运行时物品状态</returns>
    public static InventoryItemRuntimeState Create(
        InventoryItemData itemData,
        int amount,
        List<ContainerItemSaveData> internalItems = null,
        List<ContainerCellStateSaveData> internalCellStates = null)
    {
        InventoryItemRuntimeState state = new InventoryItemRuntimeState();
        state.Configure(itemData, amount, internalItems, internalCellStates);
        return state;
    }

    /// <summary>
    /// 用基础物品信息重置当前运行时状态
    /// </summary>
    /// <param name="itemData">静态物品配置</param>
    /// <param name="amount">物品数量</param>
    /// <param name="internalItems">容器类物品的内部物品快照</param>
    /// <param name="internalCellStates">容器类物品的内部格子状态快照</param>
    public void Configure(
        InventoryItemData itemData,
        int amount,
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        _itemData = itemData;
        _amount = Mathf.Max(0, amount);
        _internalItems = CloneSaveDataList(internalItems);
        _internalCellStates = CloneCellStateList(internalCellStates);
        _requiresSearch = false;
        _isSearched = true;
        _searchProgressSeconds = 0f;
        _searchDurationSeconds = 0f;
    }

    /// <summary>
    /// 套用容器快照中的搜索状态和运行时 ID
    /// </summary>
    /// <param name="saveData">容器物品快照</param>
    public void ApplyContainerSaveData(ContainerItemSaveData saveData)
    {
        if (saveData == null)
            return;

        _runtimeItemId = saveData.RuntimeItemId;
        if (saveData.ItemData != null)
            _itemData = saveData.ItemData;

        _amount = Mathf.Max(0, saveData.Amount);
        _internalItems = CloneSaveDataList(saveData.InternalItems);
        _internalCellStates = CloneCellStateList(saveData.InternalCellStates);
        _requiresSearch = saveData.RequiresSearch;
        _searchDurationSeconds = saveData.SearchDurationSeconds > 0f
            ? saveData.SearchDurationSeconds
            : (_itemData != null ? _itemData.GetSearchDurationSeconds() : 0f);
        _searchProgressSeconds = Mathf.Clamp(saveData.SearchProgressSeconds, 0f, _searchDurationSeconds);
        _isSearched = !_requiresSearch || saveData.IsSearched || _searchProgressSeconds >= _searchDurationSeconds;

        if (_isSearched)
            _searchProgressSeconds = _searchDurationSeconds;
    }

    /// <summary>
    /// 导出当前运行时物品的容器快照
    /// </summary>
    /// <param name="gridPosition">物品所在格子坐标</param>
    /// <param name="isRotated">物品是否旋转摆放</param>
    /// <returns>可持久化的容器物品快照</returns>
    public ContainerItemSaveData CreateSaveDataSnapshot(Vector2Int gridPosition, bool isRotated)
    {
        return new ContainerItemSaveData
        {
            RuntimeItemId = _runtimeItemId,
            ItemData = _itemData,
            Amount = _amount,
            X = gridPosition.x,
            Y = gridPosition.y,
            IsRotated = isRotated,
            RequiresSearch = _requiresSearch,
            IsSearched = _isSearched,
            SearchProgressSeconds = _searchProgressSeconds,
            SearchDurationSeconds = _searchDurationSeconds,
            InternalItems = CloneSaveDataList(_internalItems),
            InternalCellStates = CloneCellStateList(_internalCellStates)
        };
    }

    /// <summary>
    /// 推进搜索进度
    /// </summary>
    /// <param name="deltaSeconds">本次推进的秒数</param>
    /// <returns>本次推进后是否已经完成搜索</returns>
    public bool AdvanceSearchProgress(float deltaSeconds)
    {
        if (!_requiresSearch || _isSearched)
            return false;

        _searchProgressSeconds = Mathf.Min(
            _searchProgressSeconds + Mathf.Max(0f, deltaSeconds),
            _searchDurationSeconds);

        if (_searchProgressSeconds < _searchDurationSeconds)
            return false;

        _isSearched = true;
        _searchProgressSeconds = _searchDurationSeconds;
        return true;
    }

    /// <summary>
    /// 查询容器类物品内部是否没有任何运行时内容
    /// </summary>
    /// <returns>内部物品和内部格子状态是否都为空</returns>
    public bool IsContainerCompletelyEmpty()
    {
        bool hasInternalItems = _internalItems != null && _internalItems.Count > 0;
        bool hasInternalCellStates = _internalCellStates != null && _internalCellStates.Count > 0;
        return !hasInternalItems && !hasInternalCellStates;
    }

    /// <summary>
    /// 深拷贝当前运行时物品状态
    /// </summary>
    /// <returns>新的独立运行时物品状态</returns>
    public InventoryItemRuntimeState DeepCopy()
    {
        InventoryItemRuntimeState copy = new InventoryItemRuntimeState
        {
            _runtimeItemId = _runtimeItemId,
            _itemData = _itemData,
            _amount = _amount,
            _requiresSearch = _requiresSearch,
            _isSearched = _isSearched,
            _searchProgressSeconds = _searchProgressSeconds,
            _searchDurationSeconds = _searchDurationSeconds,
            _internalItems = CloneSaveDataList(_internalItems),
            _internalCellStates = CloneCellStateList(_internalCellStates)
        };
        return copy;
    }

    private static List<ContainerItemSaveData> CloneSaveDataList(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> clone = new List<ContainerItemSaveData>();
        if (source == null)
            return clone;

        foreach (ContainerItemSaveData item in source)
        {
            if (item != null)
                clone.Add(item.DeepCopy());
        }

        return clone;
    }

    private static List<ContainerCellStateSaveData> CloneCellStateList(List<ContainerCellStateSaveData> source)
    {
        List<ContainerCellStateSaveData> clone = new List<ContainerCellStateSaveData>();
        if (source == null)
            return clone;

        foreach (ContainerCellStateSaveData item in source)
        {
            if (item != null)
                clone.Add(item.DeepCopy());
        }

        return clone;
    }
}
