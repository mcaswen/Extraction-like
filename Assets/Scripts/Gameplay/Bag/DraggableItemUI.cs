using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单个背包物品的运行时视图。
/// 负责拖拽交互与基础显示，不直接管理网格规则本身。
/// </summary>
[RequireComponent(typeof(Image))]
public class DraggableItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IInventoryItemView
{
    [Header("Ownership")]
    public InventoryUIController CurrentGrid;

    [Header("Item Data")]
    public InventoryItemData ItemData;
    public bool IsDebugItem;

    [Header("Stack")]
    public int CurrentAmount = 1;
    public Text AmountText;

    public Vector2Int _originalGridIndex;
    public bool _originalIsRotated;
    public List<ContainerItemSaveData> InternalItems = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> InternalCellStates = new List<ContainerCellStateSaveData>();

    private InventoryUIController _lastHoveredGrid;
    private bool _currentPreviewIsRotated;
    private Vector2 _localGridOffset;
    private Vector3 _visualDragOffset;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Image _itemImage;
    private Transform _originalParent;
    private bool _requiresSearch;
    private bool _isSearched = true;
    private float _searchProgressSeconds;
    private float _searchDurationSeconds;
    private RectTransform _searchOverlayRoot;
    private CanvasGroup _searchOverlayCanvasGroup;
    private Image _searchBackdropImage;
    private Image _searchOuterTrackImage;
    private Image _searchProgressImage;
    private Image _searchPulseRingImage;
    private Image _searchSweepImage;
    private Image _searchCenterGlowImage;
    private Image _searchRevealFlashImage;
    private Text _searchStateText;
    private Text _searchRarityText;
    private bool _isRevealAnimating;
    private float _revealAnimationTimer;

    private static Sprite _defaultSearchSprite;
    private static Font _defaultSearchFont;
    private const float RevealAnimationDuration = 0.32f;

    public static DraggableItemUI CurrentlyDraggedItem;

    InventoryItemData IInventoryItemView.ItemData => ItemData;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Update()
    {
        TickSearchProgress();
        TickSearchRevealAnimation();
    }

    /// <summary>
    /// 初始化运行时物品视图。
    /// </summary>
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
    /// 应用持久化的运行时状态，例如搜索进度。
    /// </summary>
    public void ApplyContainerRuntimeState(ContainerItemSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        _requiresSearch = saveData.RequiresSearch;
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanInteractWithItem())
        {
            return;
        }

        EnsureComponents();
        CurrentlyDraggedItem = this;
        SplitUIController.Instance?.CloseWindow();
        _originalParent = transform.parent;

        if (TryDetachFromEquipmentSlot())
        {
            CurrentGrid = null;
            _localGridOffset = Vector2.zero;
        }
        else if (CurrentGrid != null)
        {
            CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);
            Vector2 startMouseLocal = CurrentGrid.GetGridLocalPoint(eventData.position, eventData.pressEventCamera);
            Vector2 startItemLocal = CurrentGrid.GetLocalPosition(_originalGridIndex.x, _originalGridIndex.y);
            _localGridOffset = startItemLocal - startMouseLocal;
        }

        PrepareDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDraggedVisual(eventData);
        UpdateHighlightPreview(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryUIController sourceGrid = CurrentGrid;
        EquipmentSlotUI targetSlot = GetHoveredEquipmentSlot(eventData);
        InventoryUIController targetGrid = targetSlot == null ? GetHoveredGrid(eventData) : null;

        RestoreDragVisualState();

        if (targetSlot != null && targetSlot.TryHandleDrop(this))
        {
            return;
        }

        if (targetGrid == null)
        {
            DropToWorld();
            return;
        }

        if (!CanBePlacedInGrid(targetGrid))
        {
            BounceBack();
            return;
        }

        Vector2Int targetIndex = GetPredictedGridIndex(targetGrid, eventData);
        GetCurrentFootprint(_currentPreviewIsRotated, out int width, out int height);
        InventoryGridController targetController = targetGrid.GetGridController();

        if (TryPlaceInEmptySpace(targetGrid, targetController, targetIndex, width, height))
        {
            NotifyLootTransferredFromLootChest(sourceGrid, targetGrid);
            return;
        }

        if (TryMergeWithBlockingItem(targetController, targetIndex, width, height))
        {
            NotifyLootTransferredFromLootChest(sourceGrid, targetGrid);
            return;
        }

        if (TrySwapWithinSameGrid(targetGrid, targetController, targetIndex, width, height))
        {
            return;
        }

        BounceBack();
    }

    /// <summary>
    /// 物品成功放入新位置后，同步网格和视图状态。
    /// </summary>
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
    /// 将物品恢复到拖拽前的位置或槽位。
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
    /// 刷新堆叠数量文本。
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
    /// 强制结束拖拽态，不改变物品最终位置。
    /// </summary>
    public void ForceEndDrag()
    {
        RestoreDragVisualState();
    }

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
    /// 执行堆叠拆分。
    /// </summary>
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
    /// 导出当前物品的持久化快照。
    /// </summary>
    public ContainerItemSaveData CreateSaveDataSnapshot()
    {
        return new ContainerItemSaveData
        {
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
    /// 判断当前容器物品的内部是否完全为空。
    /// </summary>
    public bool IsContainerCompletelyEmpty()
    {
        bool hasInternalItems = InternalItems != null && InternalItems.Count > 0;
        bool hasInternalCellStates = InternalCellStates != null && InternalCellStates.Count > 0;
        return !hasInternalItems && !hasInternalCellStates;
    }

    /// <summary>
    /// 判断当前物品是否允许放入目标网格。
    /// </summary>
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

    private bool TryDetachFromEquipmentSlot()
    {
        EquipmentSlotUI sourceSlot = GetComponentInParent<EquipmentSlotUI>();
        if (sourceSlot == null || sourceSlot.EquippedItem != this)
        {
            return false;
        }

        sourceSlot.Unequip();
        return true;
    }

    private void PrepareDragVisual(PointerEventData eventData)
    {
        RectTransform dragLayer = InventoryItemFactory.Instance != null
            ? InventoryItemFactory.Instance.GlobalDragLayer as RectTransform
            : null;

        if (dragLayer == null)
        {
            return;
        }

        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePosition);
        _visualDragOffset = _rectTransform.position - worldMousePosition;

        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;
    }

    private void UpdateDraggedVisual(PointerEventData eventData)
    {
        RectTransform dragLayer = InventoryItemFactory.Instance != null
            ? InventoryItemFactory.Instance.GlobalDragLayer as RectTransform
            : null;

        if (dragLayer == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePosition))
        {
            _rectTransform.position = worldMousePosition + _visualDragOffset;
        }
    }

    private void UpdateHighlightPreview(PointerEventData eventData)
    {
        InventoryUIController hoveredGrid = GetHoveredGrid(eventData);
        if (_lastHoveredGrid != null && _lastHoveredGrid != hoveredGrid)
        {
            _lastHoveredGrid.HideHighlight();
        }

        _lastHoveredGrid = hoveredGrid;
        if (hoveredGrid == null || ItemData == null)
        {
            return;
        }

        Vector2Int hoverIndex = GetPredictedGridIndex(hoveredGrid, eventData);
        GetCurrentFootprint(_currentPreviewIsRotated, out int width, out int height);
        UpdatePreviewRotationIfNeeded(hoveredGrid, hoverIndex, ref width, ref height);

        bool canPlace = hoveredGrid.GetGridController().IsSpaceAvailable(hoverIndex.x, hoverIndex.y, width, height);
        hoveredGrid.ShowHighlight(hoverIndex.x, hoverIndex.y, width, height, canPlace);
    }

    private Vector2Int GetPredictedGridIndex(InventoryUIController grid, PointerEventData eventData)
    {
        Vector2 currentMouseLocal = grid.GetGridLocalPoint(eventData.position, eventData.pressEventCamera);
        Vector2 predictedLocalPosition = currentMouseLocal + _localGridOffset;
        return grid.GetGridIndex(predictedLocalPosition);
    }

    private void UpdatePreviewRotationIfNeeded(InventoryUIController hoveredGrid, Vector2Int hoverIndex, ref int width, ref int height)
    {
        int cols = hoveredGrid.GetGridController().Columns;
        int rows = hoveredGrid.GetGridController().Rows;

        int overX = hoverIndex.x < 0 ? -hoverIndex.x : Mathf.Max(0, hoverIndex.x + width - cols);
        int overY = hoverIndex.y < 0 ? -hoverIndex.y : Mathf.Max(0, hoverIndex.y + height - rows);

        bool nextRotation = _currentPreviewIsRotated;
        if (overX > 0 && overX >= overY && width > height)
        {
            nextRotation = !_currentPreviewIsRotated;
        }
        else if (overY > 0 && overY > overX && height > width)
        {
            nextRotation = !_currentPreviewIsRotated;
        }

        if (nextRotation == _currentPreviewIsRotated)
        {
            return;
        }

        _currentPreviewIsRotated = nextRotation;
        UpdateVisualSize(_currentPreviewIsRotated);
        GetCurrentFootprint(_currentPreviewIsRotated, out width, out height);
    }

    private bool TryPlaceInEmptySpace(InventoryUIController targetGrid, InventoryGridController targetController, Vector2Int targetIndex, int width, int height)
    {
        if (!targetController.IsSpaceAvailable(targetIndex.x, targetIndex.y, width, height))
        {
            return false;
        }

        transform.SetParent(targetGrid.ItemContainer, false);
        CurrentGrid = targetGrid;
        PlaceSuccessfully(targetIndex, _currentPreviewIsRotated);
        return true;
    }

    private bool TryMergeWithBlockingItem(InventoryGridController targetController, Vector2Int targetIndex, int width, int height)
    {
        HashSet<DraggableItemUI> blockingItems = targetController.GetItemsInArea(targetIndex.x, targetIndex.y, width, height);
        if (blockingItems.Count != 1)
        {
            return false;
        }

        DraggableItemUI blockingItem = GetSingleItem(blockingItems);
        if (blockingItem == null || ItemData != blockingItem.ItemData || ItemData == null || !ItemData.IsStackable)
        {
            return false;
        }

        int totalAmount = CurrentAmount + blockingItem.CurrentAmount;
        if (totalAmount <= ItemData.MaxStack)
        {
            blockingItem.CurrentAmount = totalAmount;
            blockingItem.UpdateAmountText();
            Destroy(gameObject);
        }
        else
        {
            CurrentAmount = totalAmount - ItemData.MaxStack;
            blockingItem.CurrentAmount = ItemData.MaxStack;
            UpdateAmountText();
            blockingItem.UpdateAmountText();
            BounceBack();
        }

        return true;
    }

    private bool TrySwapWithinSameGrid(InventoryUIController targetGrid, InventoryGridController targetController, Vector2Int targetIndex, int width, int height)
    {
        if (targetGrid != CurrentGrid)
        {
            return false;
        }

        HashSet<DraggableItemUI> blockingItems = targetController.GetItemsInArea(targetIndex.x, targetIndex.y, width, height);
        if (blockingItems.Count != 1)
        {
            return false;
        }

        DraggableItemUI blockingItem = GetSingleItem(blockingItems);
        if (blockingItem == null)
        {
            return false;
        }

        targetController.RemoveItem(blockingItem, blockingItem._originalGridIndex.x, blockingItem._originalGridIndex.y, blockingItem._originalIsRotated);
        if (!targetController.IsSpaceAvailable(targetIndex.x, targetIndex.y, width, height))
        {
            targetController.PlaceItem(blockingItem, blockingItem._originalGridIndex.x, blockingItem._originalGridIndex.y, blockingItem._originalIsRotated);
            return false;
        }

        targetController.PlaceItem(this, targetIndex.x, targetIndex.y, _currentPreviewIsRotated);

        GetCurrentFootprint(blockingItem._originalIsRotated, blockingItem, out int blockingWidth, out int blockingHeight);
        if (targetController.IsSpaceAvailable(_originalGridIndex.x, _originalGridIndex.y, blockingWidth, blockingHeight))
        {
            blockingItem.PlaceSuccessfully(_originalGridIndex, blockingItem._originalIsRotated);
            transform.SetParent(targetGrid.ItemContainer, false);
            PlaceSuccessfully(targetIndex, _currentPreviewIsRotated);
            return true;
        }

        if (targetController.FindFirstAvailableSpace(blockingItem.ItemData.Width, blockingItem.ItemData.Height, out Vector2Int newPosition, out bool needsRotation))
        {
            blockingItem.PlaceSuccessfully(newPosition, needsRotation);
            transform.SetParent(targetGrid.ItemContainer, false);
            PlaceSuccessfully(targetIndex, _currentPreviewIsRotated);
            return true;
        }

        targetController.RemoveItem(this, targetIndex.x, targetIndex.y, _currentPreviewIsRotated);
        targetController.PlaceItem(blockingItem, blockingItem._originalGridIndex.x, blockingItem._originalGridIndex.y, blockingItem._originalIsRotated);
        return false;
    }

    private bool TryRestoreToGrid()
    {
        if (CurrentGrid == null)
        {
            return false;
        }

        transform.SetParent(CurrentGrid.ItemContainer, false);
        CurrentGrid.GetGridController().PlaceItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(_originalGridIndex.x, _originalGridIndex.y);
        _currentPreviewIsRotated = _originalIsRotated;
        UpdateVisualSize(_originalIsRotated);
        return true;
    }

    private bool TryRestoreToEquipmentSlot()
    {
        EquipmentSlotUI originalSlot = _originalParent != null ? _originalParent.GetComponent<EquipmentSlotUI>() : null;
        if (originalSlot == null)
        {
            return false;
        }

        bool equipped = originalSlot.TryEquip(this);
        if (equipped)
        {
            _currentPreviewIsRotated = _originalIsRotated;
            UpdateVisualSize(_originalIsRotated);
        }

        return equipped;
    }

    private void UpdateVisualSize(bool isRotated)
    {
        if (ItemData == null)
        {
            return;
        }

        GetCurrentFootprint(isRotated, out int width, out int height);
        if (CurrentGrid != null)
        {
            _rectTransform.sizeDelta = CurrentGrid.GetItemActualSize(width, height);
        }
        else
        {
            _rectTransform.sizeDelta = new Vector2(width * 50 + (width - 1) * 2, height * 50 + (height - 1) * 2);
        }

        _rectTransform.localEulerAngles = Vector3.zero;
        ResizeSearchOverlay();
    }

    private void DropToWorld()
    {
        if (ItemData == null || ItemData.WorldPrefab == null)
        {
            BounceBack();
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 spawnPosition = player != null
            ? player.transform.position + player.transform.forward * 1.5f + Vector3.up
            : Vector3.zero;

        GameObject droppedObject = Instantiate(ItemData.WorldPrefab, spawnPosition, Quaternion.identity);
        droppedObject.GetComponent<WorldLootItem>()?.InitializeDrop(
            ItemData,
            CurrentAmount,
            CloneSaveDataList(InternalItems),
            CloneCellStateList(InternalCellStates));
        Destroy(gameObject);
    }

    private void RestoreDragVisualState()
    {
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        if (_lastHoveredGrid != null)
        {
            _lastHoveredGrid.HideHighlight();
        }

        _lastHoveredGrid = null;
        CurrentlyDraggedItem = null;
    }

    private bool CanInteractWithItem()
    {
        return (!_requiresSearch || _isSearched) && !_isRevealAnimating;
    }

    private void ExecuteQuickTransfer()
    {
        if (CurrentGrid == null || ItemData == null || GameUIController.Instance == null)
        {
            return;
        }

        InventoryUIController sourceGrid = CurrentGrid;
        if (!GameUIController.Instance.TryFindQuickTransferTarget(CurrentGrid, this, out InventoryUIController targetGrid, out Vector2Int position, out bool needsRotation))
        {
            return;
        }

        CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);
        transform.SetParent(targetGrid.ItemContainer, false);
        CurrentGrid = targetGrid;
        PlaceSuccessfully(position, needsRotation);
        NotifyLootTransferredFromLootChest(sourceGrid, targetGrid);
    }

    private InventoryUIController GetHoveredGrid(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            InventoryUIController grid = result.gameObject.GetComponentInParent<InventoryUIController>();
            if (grid != null)
            {
                return grid;
            }
        }

        return null;
    }

    private EquipmentSlotUI GetHoveredEquipmentSlot(PointerEventData eventData)
    {
        if (GameUIController.Instance != null)
        {
            EquipmentSlotUI slotFromScreenPoint = GameUIController.Instance.GetEquipmentSlotAtScreenPosition(
                eventData.position,
                eventData.pressEventCamera);
            if (slotFromScreenPoint != null)
            {
                return slotFromScreenPoint;
            }
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            EquipmentSlotUI slot = result.gameObject.GetComponentInParent<EquipmentSlotUI>();
            if (slot != null)
            {
                return slot;
            }
        }

        return null;
    }

    private void GetCurrentFootprint(bool isRotated, out int width, out int height)
    {
        GetCurrentFootprint(isRotated, this, out width, out height);
    }

    private static void GetCurrentFootprint(bool isRotated, DraggableItemUI itemUI, out int width, out int height)
    {
        width = isRotated ? itemUI.ItemData.Height : itemUI.ItemData.Width;
        height = isRotated ? itemUI.ItemData.Width : itemUI.ItemData.Height;
    }

    private static DraggableItemUI GetSingleItem(HashSet<DraggableItemUI> items)
    {
        foreach (DraggableItemUI item in items)
        {
            return item;
        }

        return null;
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

    private static bool IsBackpackGrid(InventoryUIController targetGrid)
    {
        return GameUIController.Instance != null && GameUIController.Instance.BackpackGrid == targetGrid;
    }

    private void NotifyLootTransferredFromLootChest(InventoryUIController sourceGrid, InventoryUIController targetGrid)
    {
        if (GameUIController.Instance == null || sourceGrid == null || targetGrid == null)
        {
            return;
        }

        if (sourceGrid != GameUIController.Instance.LootChestGrid)
        {
            return;
        }

        if (targetGrid == GameUIController.Instance.LootChestGrid)
        {
            return;
        }

        RaidFlowController.Instance?.NotifyLootCollected(ItemData != null ? ItemData.ItemName : "Unknown Loot");
    }

    private void TickSearchProgress()
    {
        if (!_requiresSearch || _isSearched || !gameObject.activeInHierarchy)
        {
            return;
        }

        _searchProgressSeconds = Mathf.Min(_searchProgressSeconds + Time.unscaledDeltaTime, _searchDurationSeconds);
        if (_searchProgressSeconds >= _searchDurationSeconds)
        {
            _isSearched = true;
            _isRevealAnimating = true;
            _revealAnimationTimer = 0f;
            UpdateAmountText();
        }

        UpdateSearchVisualState();
    }

    private void TickSearchRevealAnimation()
    {
        if (!_isRevealAnimating)
        {
            return;
        }

        _revealAnimationTimer += Time.unscaledDeltaTime;
        if (_revealAnimationTimer >= RevealAnimationDuration)
        {
            _isRevealAnimating = false;
            _revealAnimationTimer = 0f;
        }

        UpdateSearchVisualState();
    }

    private void EnsureSearchOverlay()
    {
        if (_searchOverlayRoot != null)
        {
            return;
        }

        if (_defaultSearchSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            _defaultSearchSprite = Sprite.Create(
                whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        if (_defaultSearchFont == null)
        {
            _defaultSearchFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        GameObject overlayRootObject = new GameObject("SearchOverlay", typeof(RectTransform));
        overlayRootObject.transform.SetParent(transform, false);
        _searchOverlayRoot = overlayRootObject.GetComponent<RectTransform>();
        _searchOverlayCanvasGroup = overlayRootObject.AddComponent<CanvasGroup>();
        _searchOverlayRoot.anchorMin = Vector2.zero;
        _searchOverlayRoot.anchorMax = Vector2.one;
        _searchOverlayRoot.offsetMin = Vector2.zero;
        _searchOverlayRoot.offsetMax = Vector2.zero;
        _searchOverlayRoot.pivot = new Vector2(0.5f, 0.5f);

        GameObject backdropObject = new GameObject("Backdrop", typeof(Image));
        backdropObject.transform.SetParent(_searchOverlayRoot, false);
        _searchBackdropImage = backdropObject.GetComponent<Image>();
        RectTransform backdropRect = _searchBackdropImage.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        _searchBackdropImage.sprite = _defaultSearchSprite;
        _searchBackdropImage.type = Image.Type.Simple;
        _searchBackdropImage.color = new Color(0.02f, 0.03f, 0.04f, 0.78f);

        GameObject outerTrackObject = new GameObject("OuterTrack", typeof(Image));
        outerTrackObject.transform.SetParent(_searchOverlayRoot, false);
        _searchOuterTrackImage = outerTrackObject.GetComponent<Image>();
        RectTransform outerTrackRect = _searchOuterTrackImage.rectTransform;
        outerTrackRect.anchorMin = new Vector2(0.08f, 0.08f);
        outerTrackRect.anchorMax = new Vector2(0.92f, 0.92f);
        outerTrackRect.offsetMin = Vector2.zero;
        outerTrackRect.offsetMax = Vector2.zero;
        _searchOuterTrackImage.sprite = _defaultSearchSprite;
        _searchOuterTrackImage.type = Image.Type.Simple;
        _searchOuterTrackImage.color = new Color(0.15f, 0.18f, 0.22f, 0.9f);

        GameObject progressObject = new GameObject("Progress", typeof(Image));
        progressObject.transform.SetParent(_searchOverlayRoot, false);
        _searchProgressImage = progressObject.GetComponent<Image>();
        RectTransform progressRect = _searchProgressImage.rectTransform;
        progressRect.anchorMin = new Vector2(0.12f, 0.12f);
        progressRect.anchorMax = new Vector2(0.88f, 0.88f);
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = Vector2.zero;
        _searchProgressImage.sprite = _defaultSearchSprite;
        _searchProgressImage.type = Image.Type.Filled;
        _searchProgressImage.fillMethod = Image.FillMethod.Radial360;
        _searchProgressImage.fillOrigin = 2;
        _searchProgressImage.fillClockwise = false;
        _searchProgressImage.color = new Color(0.98f, 0.92f, 0.52f, 0.95f);

        GameObject pulseRingObject = new GameObject("PulseRing", typeof(Image));
        pulseRingObject.transform.SetParent(_searchOverlayRoot, false);
        _searchPulseRingImage = pulseRingObject.GetComponent<Image>();
        RectTransform pulseRect = _searchPulseRingImage.rectTransform;
        pulseRect.anchorMin = new Vector2(0.16f, 0.16f);
        pulseRect.anchorMax = new Vector2(0.84f, 0.84f);
        pulseRect.offsetMin = Vector2.zero;
        pulseRect.offsetMax = Vector2.zero;
        _searchPulseRingImage.sprite = _defaultSearchSprite;
        _searchPulseRingImage.type = Image.Type.Simple;
        _searchPulseRingImage.color = new Color(1f, 1f, 1f, 0.15f);

        GameObject sweepObject = new GameObject("Sweep", typeof(Image));
        sweepObject.transform.SetParent(_searchOverlayRoot, false);
        _searchSweepImage = sweepObject.GetComponent<Image>();
        RectTransform sweepRect = _searchSweepImage.rectTransform;
        sweepRect.anchorMin = new Vector2(0.485f, 0.1f);
        sweepRect.anchorMax = new Vector2(0.515f, 0.9f);
        sweepRect.offsetMin = Vector2.zero;
        sweepRect.offsetMax = Vector2.zero;
        _searchSweepImage.sprite = _defaultSearchSprite;
        _searchSweepImage.type = Image.Type.Simple;
        _searchSweepImage.color = new Color(0.96f, 0.99f, 1f, 0.18f);

        GameObject centerGlowObject = new GameObject("CenterGlow", typeof(Image));
        centerGlowObject.transform.SetParent(_searchOverlayRoot, false);
        _searchCenterGlowImage = centerGlowObject.GetComponent<Image>();
        RectTransform centerGlowRect = _searchCenterGlowImage.rectTransform;
        centerGlowRect.anchorMin = new Vector2(0.28f, 0.28f);
        centerGlowRect.anchorMax = new Vector2(0.72f, 0.72f);
        centerGlowRect.offsetMin = Vector2.zero;
        centerGlowRect.offsetMax = Vector2.zero;
        _searchCenterGlowImage.sprite = _defaultSearchSprite;
        _searchCenterGlowImage.type = Image.Type.Simple;
        _searchCenterGlowImage.color = new Color(1f, 1f, 1f, 0.08f);

        GameObject revealFlashObject = new GameObject("RevealFlash", typeof(Image));
        revealFlashObject.transform.SetParent(_searchOverlayRoot, false);
        _searchRevealFlashImage = revealFlashObject.GetComponent<Image>();
        RectTransform revealFlashRect = _searchRevealFlashImage.rectTransform;
        revealFlashRect.anchorMin = Vector2.zero;
        revealFlashRect.anchorMax = Vector2.one;
        revealFlashRect.offsetMin = Vector2.zero;
        revealFlashRect.offsetMax = Vector2.zero;
        _searchRevealFlashImage.sprite = _defaultSearchSprite;
        _searchRevealFlashImage.type = Image.Type.Simple;
        _searchRevealFlashImage.color = new Color(1f, 1f, 1f, 0f);

        GameObject labelObject = new GameObject("StateText", typeof(Text));
        labelObject.transform.SetParent(_searchOverlayRoot, false);
        _searchStateText = labelObject.GetComponent<Text>();
        RectTransform labelRect = _searchStateText.rectTransform;
        labelRect.anchorMin = new Vector2(0.1f, 0.26f);
        labelRect.anchorMax = new Vector2(0.9f, 0.74f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        _searchStateText.alignment = TextAnchor.MiddleCenter;
        _searchStateText.font = _defaultSearchFont;
        _searchStateText.fontSize = 17;
        _searchStateText.fontStyle = FontStyle.Bold;
        _searchStateText.color = new Color(0.96f, 0.97f, 0.98f, 0.98f);
        _searchStateText.text = "搜索中";

        GameObject rarityLabelObject = new GameObject("RarityText", typeof(Text));
        rarityLabelObject.transform.SetParent(_searchOverlayRoot, false);
        _searchRarityText = rarityLabelObject.GetComponent<Text>();
        RectTransform rarityLabelRect = _searchRarityText.rectTransform;
        rarityLabelRect.anchorMin = new Vector2(0.08f, 0.04f);
        rarityLabelRect.anchorMax = new Vector2(0.92f, 0.22f);
        rarityLabelRect.offsetMin = Vector2.zero;
        rarityLabelRect.offsetMax = Vector2.zero;
        _searchRarityText.alignment = TextAnchor.MiddleCenter;
        _searchRarityText.font = _defaultSearchFont;
        _searchRarityText.fontSize = 11;
        _searchRarityText.fontStyle = FontStyle.Bold;
        _searchRarityText.color = new Color(0.8f, 0.84f, 0.9f, 0.9f);
        _searchRarityText.text = "SCANNING";

        _searchOverlayRoot.gameObject.SetActive(false);
    }

    private void ResizeSearchOverlay()
    {
        if (_searchOverlayRoot == null)
        {
            return;
        }

        _searchOverlayRoot.SetAsLastSibling();
    }

    private void UpdateSearchVisualState()
    {
        EnsureSearchOverlay();

        bool showOverlay = _requiresSearch && !_isSearched;
        bool showReveal = _isRevealAnimating;
        if (_searchOverlayRoot != null)
        {
            _searchOverlayRoot.gameObject.SetActive(showOverlay || showReveal);
        }

        if (!showOverlay && !showReveal)
        {
            if (_itemImage != null)
            {
                _itemImage.color = Color.white;
            }

            return;
        }

        Color rarityColor = GetSearchRarityColor();
        float normalizedProgress = _searchDurationSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(_searchProgressSeconds / _searchDurationSeconds);
        float revealNormalized = _isRevealAnimating
            ? Mathf.Clamp01(_revealAnimationTimer / RevealAnimationDuration)
            : 0f;

        if (_searchOverlayCanvasGroup != null)
        {
            _searchOverlayCanvasGroup.alpha = _isRevealAnimating ? 1f - revealNormalized : 1f;
        }

        if (_itemImage != null)
        {
            Color hiddenColor = new Color(0.16f, 0.18f, 0.2f, 0.94f);
            _itemImage.color = Color.Lerp(hiddenColor, Color.white, revealNormalized);
        }

        if (_searchBackdropImage != null)
        {
            float backdropPulse = 0.76f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.06f;
            _searchBackdropImage.color = new Color(0.02f, 0.03f, 0.04f, backdropPulse);
        }

        if (_searchOuterTrackImage != null)
        {
            _searchOuterTrackImage.color = new Color(0.15f, 0.18f, 0.22f, 0.92f);
        }

        if (_searchProgressImage != null)
        {
            _searchProgressImage.fillAmount = normalizedProgress;
            _searchProgressImage.color = Color.Lerp(rarityColor * 0.75f, rarityColor, 0.5f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.15f);
            _searchProgressImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 210f);
        }

        if (_searchPulseRingImage != null)
        {
            float pulseScale = 0.94f + Mathf.Sin(Time.unscaledTime * 5.1f) * 0.06f;
            _searchPulseRingImage.rectTransform.localScale = new Vector3(pulseScale, pulseScale, 1f);
            _searchPulseRingImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.08f + (1f - normalizedProgress) * 0.12f);
        }

        if (_searchSweepImage != null)
        {
            _searchSweepImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 280f);
            _searchSweepImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.16f);
        }

        if (_searchCenterGlowImage != null)
        {
            float glowStrength = 0.08f + normalizedProgress * 0.18f;
            _searchCenterGlowImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, glowStrength);
        }

        if (_searchRevealFlashImage != null)
        {
            if (_isRevealAnimating)
            {
                float flashAlpha = Mathf.Clamp01(1f - revealNormalized) * 0.85f;
                _searchRevealFlashImage.color = new Color(1f, 1f, 1f, flashAlpha);
                float flashScale = 0.88f + revealNormalized * 0.25f;
                _searchRevealFlashImage.rectTransform.localScale = new Vector3(flashScale, flashScale, 1f);
            }
            else
            {
                _searchRevealFlashImage.color = new Color(1f, 1f, 1f, 0f);
                _searchRevealFlashImage.rectTransform.localScale = Vector3.one;
            }
        }

        if (_searchStateText != null)
        {
            float remainingSeconds = Mathf.Max(0f, _searchDurationSeconds - _searchProgressSeconds);
            _searchStateText.text = _isRevealAnimating
                ? "完成"
                : (remainingSeconds > 0.08f ? $"{remainingSeconds:0.0}s" : "解析中");
            _searchStateText.color = Color.Lerp(new Color(0.92f, 0.96f, 1f, 0.98f), rarityColor, 0.25f);
        }

        if (_searchRarityText != null)
        {
            _searchRarityText.text = _isRevealAnimating ? "REVEALED" : GetSearchRarityLabel();
            _searchRarityText.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.92f);
        }
    }

    private Color GetSearchRarityColor()
    {
        if (ItemData == null)
        {
            return new Color(0.95f, 0.92f, 0.52f, 1f);
        }

        return ItemData.Rarity switch
        {
            ItemRarity.Common => new Color(0.78f, 0.84f, 0.9f, 1f),
            ItemRarity.Uncommon => new Color(0.53f, 0.9f, 0.66f, 1f),
            ItemRarity.Rare => new Color(0.45f, 0.72f, 1f, 1f),
            ItemRarity.Epic => new Color(0.93f, 0.54f, 1f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.8f, 0.34f, 1f),
            _ => new Color(0.95f, 0.92f, 0.52f, 1f)
        };
    }

    private string GetSearchRarityLabel()
    {
        if (ItemData == null)
        {
            return "SCANNING";
        }

        return ItemData.Rarity switch
        {
            ItemRarity.Common => "COMMON SIGNAL",
            ItemRarity.Uncommon => "UNCOMMON SIGNAL",
            ItemRarity.Rare => "RARE SIGNAL",
            ItemRarity.Epic => "EPIC SIGNAL",
            ItemRarity.Legendary => "LEGENDARY SIGNAL",
            _ => "SCANNING"
        };
    }
}
