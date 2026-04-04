using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class DraggableItemUI
{
    /// <summary>
    /// 初始化运行时物品视图
    /// </summary>
    /// <param name="data">静态物品配置</param>
    /// <param name="startPos">初始格子坐标</param>
    /// <param name="isRotated">初始朝向是否旋转</param>
    public void InitializeItem(InventoryItemData data, Vector2Int startPos, bool isRotated)
    {
        EnsureComponents();

        ItemData = data;
        _originalGridIndex = startPos;
        _originalIsRotated = isRotated;
        _currentPreviewIsRotated = isRotated;

        if (data != null && data.ItemIcon != null)
        {
            _itemImage.sprite = data.ItemIcon;
        }

        UpdateVisualSize(isRotated);
        if (CurrentGrid != null)
        {
            _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(startPos.x, startPos.y);
        }

        UpdateAmountText();
        UpdateSearchVisualState();
    }

    /// <summary>
    /// 应用持久化的运行时状态，例如搜索进度
    /// </summary>
    /// <param name="saveData">运行时快照数据</param>
    public void ApplyContainerRuntimeState(ContainerItemSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        _requiresSearch = saveData.RequiresSearch;
        RuntimeItemId = saveData.RuntimeItemId;
        _searchDurationSeconds = saveData.SearchDurationSeconds > 0f
            ? saveData.SearchDurationSeconds
            : (ItemData != null ? ItemData.GetSearchDurationSeconds() : 0f);
        _searchProgressSeconds = Mathf.Clamp(saveData.SearchProgressSeconds, 0f, _searchDurationSeconds);
        _isSearched = !_requiresSearch || saveData.IsSearched || _searchProgressSeconds >= _searchDurationSeconds;
        _isRevealAnimating = false;
        _revealAnimationTimer = 0f;

        if (_isSearched)
        {
            _searchProgressSeconds = _searchDurationSeconds;
        }

        UpdateAmountText();
        UpdateSearchVisualState();
    }

    /// <summary>
    /// 物品成功放入新位置后，同步网格和视图状态
    /// </summary>
    /// <param name="index">目标格子坐标</param>
    /// <param name="isRotated">目标朝向是否旋转</param>
    public void PlaceSuccessfully(Vector2Int index, bool isRotated)
    {
        if (CurrentGrid == null)
        {
            return;
        }

        CurrentGrid.GetGridController().PlaceItem(this, index.x, index.y, isRotated);
        _originalGridIndex = index;
        _originalIsRotated = isRotated;
        _currentPreviewIsRotated = isRotated;
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(index.x, index.y);
        UpdateVisualSize(isRotated);
    }

    /// <summary>
    /// 将物品恢复到拖拽前的位置或槽位
    /// </summary>
    public void BounceBack()
    {
        if (TryRestoreToGrid())
        {
            return;
        }

        if (TryRestoreToEquipmentSlot())
        {
            return;
        }

        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = Vector2.zero;
            _currentPreviewIsRotated = _originalIsRotated;
            UpdateVisualSize(_originalIsRotated);
        }
    }

    /// <summary>
    /// 刷新堆叠数量文本
    /// </summary>
    public void UpdateAmountText()
    {
        if (AmountText == null)
        {
            return;
        }

        bool shouldShow = ItemData != null && ItemData.IsStackable && CurrentAmount > 0 && CanInteractWithItem();
        AmountText.gameObject.SetActive(shouldShow);
        if (shouldShow)
        {
            AmountText.text = CurrentAmount.ToString();
        }
    }

    /// <summary>
    /// 强制结束拖拽态，不改变物品最终位置
    /// </summary>
    public void ForceEndDrag()
    {
        RestoreDragVisualState();
    }

    /// <summary>
    /// 处理点击交互，支持快捷转移和拆分堆叠
    /// </summary>
    /// <param name="eventData">当前指针事件</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanInteractWithItem())
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            ExecuteQuickTransfer();
            return;
        }

        if (ItemData != null && ItemData.IsStackable && CurrentAmount > 1)
        {
            SplitUIController.Instance?.OpenSplitWindow(this);
        }
    }

    /// <summary>
    /// 执行堆叠拆分
    /// </summary>
    /// <param name="splitAmount">需要拆出的数量</param>
    public void ExecuteSplit(int splitAmount)
    {
        if (CurrentGrid == null || ItemData == null)
        {
            return;
        }

        if (!CurrentGrid.GetGridController().FindSpaceAround(_originalGridIndex.x, _originalGridIndex.y, ItemData.Width, ItemData.Height, out Vector2Int position, out bool needsRotation))
        {
            return;
        }

        CurrentAmount -= splitAmount;
        UpdateAmountText();

        // 复制一个新物品视图承接拆分出的那一部分数量
        GameObject clone = Instantiate(gameObject, CurrentGrid.ItemContainer, false);
        DraggableItemUI cloneItem = clone.GetComponent<DraggableItemUI>();
        cloneItem.CurrentGrid = CurrentGrid;
        cloneItem.IsDebugItem = false;
        cloneItem.name = name + "_Split";

        CanvasGroup cloneCanvasGroup = cloneItem.GetComponent<CanvasGroup>();
        if (cloneCanvasGroup != null)
        {
            cloneCanvasGroup.alpha = 1f;
            cloneCanvasGroup.blocksRaycasts = true;
        }

        cloneItem.CurrentAmount = splitAmount;
        cloneItem.InternalItems = CloneSaveDataList(InternalItems);
        cloneItem.InternalCellStates = CloneCellStateList(InternalCellStates);
        cloneItem.InitializeItem(ItemData, position, needsRotation);
        CurrentGrid.GetGridController().PlaceItem(cloneItem, position.x, position.y, needsRotation);
    }

    /// <summary>
    /// 导出当前物品的持久化快照
    /// </summary>
    public ContainerItemSaveData CreateSaveDataSnapshot()
    {
        return new ContainerItemSaveData
        {
            RuntimeItemId = RuntimeItemId,
            ItemData = ItemData,
            Amount = CurrentAmount,
            X = _originalGridIndex.x,
            Y = _originalGridIndex.y,
            IsRotated = _originalIsRotated,
            RequiresSearch = _requiresSearch,
            IsSearched = _isSearched,
            SearchProgressSeconds = _searchProgressSeconds,
            SearchDurationSeconds = _searchDurationSeconds,
            InternalItems = CloneSaveDataList(InternalItems),
            InternalCellStates = CloneCellStateList(InternalCellStates)
        };
    }

    /// <summary>
    /// 控制是否由组件自身自动推进搜索进度
    /// </summary>
    /// <param name="isEnabled">是否启用自动推进</param>
    public void SetSearchAutoTickEnabled(bool isEnabled)
    {
        AutoTickSearchProgress = isEnabled;
    }

    /// <summary>
    /// 手动推进搜索进度，供外部统一驱动搜索流程
    /// </summary>
    /// <param name="deltaSeconds">本次推进的时间增量</param>
    /// <returns>搜索是否在本次推进后完成</returns>
    public bool AdvanceSearchProgressManually(float deltaSeconds)
    {
        if (!_requiresSearch || _isSearched)
        {
            return false;
        }

        float previousProgress = _searchProgressSeconds;
        _searchProgressSeconds = Mathf.Min(_searchProgressSeconds + Mathf.Max(0f, deltaSeconds), _searchDurationSeconds);

        if (_searchProgressSeconds >= _searchDurationSeconds)
        {
            _isSearched = true;
            _isRevealAnimating = true;
            _revealAnimationTimer = 0f;
            UpdateAmountText();
        }

        if (!Mathf.Approximately(previousProgress, _searchProgressSeconds) || _isSearched)
        {
            UpdateSearchVisualState();
        }

        return _isSearched;
    }

    /// <summary>
    /// 判断当前容器物品的内部是否完全为空
    /// </summary>
    public bool IsContainerCompletelyEmpty()
    {
        bool hasInternalItems = InternalItems != null && InternalItems.Count > 0;
        bool hasInternalCellStates = InternalCellStates != null && InternalCellStates.Count > 0;
        return !hasInternalItems && !hasInternalCellStates;
    }

    /// <summary>
    /// 判断当前物品是否允许放入目标网格
    /// </summary>
    /// <param name="targetGrid">目标网格视图</param>
    /// <returns>是否允许放入</returns>
    public bool CanBePlacedInGrid(InventoryUIController targetGrid)
    {
        if (targetGrid == null || ItemData == null)
        {
            return false;
        }

        if (ItemData.Type == ItemType.Bag && IsBackpackGrid(targetGrid))
        {
            return false;
        }

        if (ItemData.Type == ItemType.Rig && IsBackpackGrid(targetGrid) && !IsContainerCompletelyEmpty())
        {
            return false;
        }

        return true;
    }

    // 缓存常用组件并统一校正 RectTransform 的布局基准
    private void EnsureComponents()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_itemImage == null)
        {
            _itemImage = GetComponent<Image>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        _rectTransform.anchorMin = new Vector2(0f, 1f);
        _rectTransform.anchorMax = new Vector2(0f, 1f);
        _rectTransform.pivot = new Vector2(0f, 1f);

        EnsureSearchOverlay();
    }

    // 深拷贝容器中的物品快照，避免不同运行时对象共享同一份列表引用
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

    // 深拷贝容器格子状态，避免 blocked cell 等运行时信息串改
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

    // 统一通过主控制器判断某个网格是否为角色背包网格
    private static bool IsBackpackGrid(InventoryUIController targetGrid)
    {
        return InventoryScreenController.Instance != null && InventoryScreenController.Instance.BackpackGrid == targetGrid;
    }
}
