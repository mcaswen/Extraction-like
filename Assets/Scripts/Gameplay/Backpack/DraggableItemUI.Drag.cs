using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class DraggableItemUI
{
    /// <summary>
    /// 开始拖拽物品时，先从原容器中临时摘除
    /// </summary>
    /// <param name="eventData">当前指针事件</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanInteractWithItem())
        {
            return;
        }

        if (!InventoryGridInteractionPolicy.CanBeginDragFrom(CurrentGrid))
        {
            return;
        }

        EnsureComponents();
        DestroyDragPlaceholder();
        _isDragActive = false;
        _activeDragLayer = null;
        CurrentlyDraggedItem = this;
        SplitUIController.Instance?.CloseWindow();
        InventoryItemInfoPanelController.Instance?.Hide();
        _originalParent = transform.parent;

        if (!TryGetDragLayer(out _activeDragLayer))
        {
            CurrentlyDraggedItem = null;
            return;
        }

        CreateDragPlaceholder();

        if (TryDetachFromEquipmentSlot(out bool blockedByLockedSlot))
        {
            CurrentGrid = null;
        }
        else if (blockedByLockedSlot)
        {
            DestroyDragPlaceholder();
            _activeDragLayer = null;
            CurrentlyDraggedItem = null;
            return;
        }
        else if (CurrentGrid != null)
        {
            CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);
        }

        PrepareDragVisual(eventData, _activeDragLayer);
        _isDragActive = true;
    }

    /// <summary>
    /// 拖拽过程中刷新悬停高亮和跟手视觉
    /// </summary>
    /// <param name="eventData">当前指针事件</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragActive || CurrentlyDraggedItem != this)
        {
            return;
        }

        UpdateHighlightPreview(eventData);
        UpdateDraggedVisual(eventData);
    }

    /// <summary>
    /// 结束拖拽时，按装备槽 网格的优先级结算落点
    /// </summary>
    /// <param name="eventData">当前指针事件</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragActive || CurrentlyDraggedItem != this)
        {
            return;
        }

        EquipmentSlotUI targetSlot = GetHoveredEquipmentSlot(eventData);
        InventoryUIController targetGrid = targetSlot == null ? GetHoveredGrid(eventData) : null;
        bool hasCachedPreviewPlacement = targetGrid != null && _hasPreviewPlacement && _lastPreviewGrid == targetGrid;
        Vector2Int cachedPreviewIndex = _lastPreviewIndex;
        int cachedPreviewWidth = _lastPreviewWidth;
        int cachedPreviewHeight = _lastPreviewHeight;

        RestoreDragVisualState();

        if (targetSlot != null && targetSlot.TryHandleDrop(this))
        {
            return;
        }

        if (targetGrid == null)
        {
            BounceBack();
            return;
        }

        if (!CanBePlacedInGrid(targetGrid))
        {
            BounceBack();
            return;
        }

        Vector2Int targetIndex;
        int width;
        int height;
        if (hasCachedPreviewPlacement)
        {
            targetIndex = cachedPreviewIndex;
            width = cachedPreviewWidth;
            height = cachedPreviewHeight;
        }
        else
        {
            ResolvePreviewPlacement(targetGrid, eventData, out targetIndex, out width, out height);
        }

        InventoryGridController targetController = targetGrid.GetGridController();

        if (TryPlaceInEmptySpace(targetGrid, targetController, targetIndex, width, height))
        {
            return;
        }

        if (TryMergeWithBlockingItem(targetController, targetIndex, width, height))
        {
            return;
        }

        if (TrySwapWithinSameGrid(targetGrid, targetController, targetIndex, width, height))
        {
            return;
        }

        BounceBack();
    }

    // 如果拖拽物原本挂在装备槽上，先把槽位中的运行时状态同步并卸下
    private bool TryDetachFromEquipmentSlot(out bool blockedByLockedSlot)
    {
        blockedByLockedSlot = false;

        EquipmentSlotUI sourceSlot = GetComponentInParent<EquipmentSlotUI>();
        if (sourceSlot == null || sourceSlot.EquippedItem != this)
        {
            return false;
        }

        if (sourceSlot.IsDefaultLockedContainerSlot)
        {
            blockedByLockedSlot = true;
            return false;
        }

        sourceSlot.Unequip();

        CurrentGrid = null;
        _currentPreviewIsRotated = false;
        UpdateVisualSize(false);

        return true;
    }

    private bool TryGetDragLayer(out RectTransform dragLayer)
    {
        dragLayer = InventoryItemFactory.Instance != null
            ? InventoryItemFactory.Instance.GlobalDragLayer as RectTransform
            : null;
        if (dragLayer != null)
        {
            return true;
        }

        Canvas ownerCanvas = GetComponentInParent<Canvas>();
        if (ownerCanvas != null && ownerCanvas.transform is RectTransform canvasRect)
        {
            dragLayer = canvasRect;
            return true;
        }

        dragLayer = _originalParent as RectTransform;
        return dragLayer != null;
    }

    private void CreateDragPlaceholder()
    {
        DestroyDragPlaceholder();

        RectTransform sourceRect = _rectTransform;
        RectTransform parentRect = _originalParent as RectTransform;
        if (sourceRect == null || parentRect == null)
        {
            return;
        }

        _dragPlaceholder = new GameObject(
            $"{name}_DragPlaceholder",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform placeholderRect = _dragPlaceholder.GetComponent<RectTransform>();
        placeholderRect.SetParent(_originalParent, false);
        placeholderRect.SetSiblingIndex(transform.GetSiblingIndex());
        placeholderRect.anchorMin = sourceRect.anchorMin;
        placeholderRect.anchorMax = sourceRect.anchorMax;
        placeholderRect.pivot = sourceRect.pivot;
        placeholderRect.sizeDelta = sourceRect.sizeDelta;
        placeholderRect.anchoredPosition = sourceRect.anchoredPosition;
        placeholderRect.localScale = Vector3.one;
        placeholderRect.localRotation = Quaternion.identity;

        Image placeholderBackground = _dragPlaceholder.GetComponent<Image>();
        placeholderBackground.sprite = _itemBackgroundImage != null ? _itemBackgroundImage.sprite : null;
        placeholderBackground.type = Image.Type.Simple;
        placeholderBackground.color = new Color(0.28f, 0.28f, 0.28f, 0.38f);
        placeholderBackground.raycastTarget = false;

        if (_itemImage == null || _itemImage.sprite == null)
        {
            return;
        }

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(placeholderRect, false);
        iconRect.anchorMin = _itemImage.rectTransform.anchorMin;
        iconRect.anchorMax = _itemImage.rectTransform.anchorMax;
        iconRect.offsetMin = _itemImage.rectTransform.offsetMin;
        iconRect.offsetMax = _itemImage.rectTransform.offsetMax;
        iconRect.localScale = _itemImage.rectTransform.localScale;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = _itemImage.sprite;
        iconImage.preserveAspect = _itemImage.preserveAspect;
        iconImage.color = new Color(0.55f, 0.55f, 0.55f, 0.36f);
        iconImage.raycastTarget = false;
    }

    private void DestroyDragPlaceholder()
    {
        if (_dragPlaceholder == null)
        {
            return;
        }

        Destroy(_dragPlaceholder);
        _dragPlaceholder = null;
    }

    // 将物品提升到全局拖拽层，并把锚点移动到鼠标附近
    private void PrepareDragVisual(PointerEventData eventData, RectTransform dragLayer)
    {
        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePosition);
        RefreshDragVisualOffsetForCenter();
        _rectTransform.position = worldMousePosition + _visualDragOffset;

        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;
    }

    // 让拖拽中的物品持续跟随鼠标移动
    private void UpdateDraggedVisual(PointerEventData eventData)
    {
        RectTransform dragLayer = _activeDragLayer;

        if (dragLayer == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, eventData.position, eventData.pressEventCamera, out Vector3 worldMousePosition))
        {
            _rectTransform.position = worldMousePosition + _visualDragOffset;
        }
    }

    // 根据鼠标所在网格刷新预览位置和合法性高亮
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
            _hasPreviewPlacement = false;
            _lastPreviewGrid = null;
            return;
        }

        ResolvePreviewPlacement(hoveredGrid, eventData, out Vector2Int hoverIndex, out int width, out int height);

        _hasPreviewPlacement = true;
        _lastPreviewGrid = hoveredGrid;
        _lastPreviewIndex = hoverIndex;
        _lastPreviewWidth = width;
        _lastPreviewHeight = height;

        bool canPlace = hoveredGrid.GetGridController().IsSpaceAvailable(hoverIndex.x, hoverIndex.y, width, height);
        hoveredGrid.ShowHighlight(_lastPreviewIndex.x, _lastPreviewIndex.y, _lastPreviewWidth, _lastPreviewHeight, canPlace);
    }

    // 统一计算当前拖拽物在目标网格中的预览坐标和占地尺寸
    private void ResolvePreviewPlacement(
        InventoryUIController hoveredGrid,
        PointerEventData eventData,
        out Vector2Int hoverIndex,
        out int width,
        out int height)
    {
        GetCurrentFootprint(_currentPreviewIsRotated, out width, out height);
        hoverIndex = GetPredictedGridIndex(hoveredGrid, eventData, width, height);
        UpdatePreviewRotationIfNeeded(hoveredGrid, eventData, ref hoverIndex, ref width, ref height);
    }

    // 按物品中心点预测它应该落在哪个格子，减少边缘吸附抖动
    private Vector2Int GetPredictedGridIndex(InventoryUIController grid, PointerEventData eventData, int width, int height)
    {
        if (grid == null)
        {
            return Vector2Int.zero;
        }

        Vector2 currentMouseLocal = grid.GetGridLocalPoint(eventData.position, eventData.pressEventCamera);
        Vector2 itemSize = grid.GetItemActualSize(width, height);
        float gridPitch = grid.CellSize + grid.Spacing;
        int predictedX = Mathf.RoundToInt((currentMouseLocal.x - itemSize.x * 0.5f) / gridPitch);
        int predictedY = Mathf.RoundToInt((-currentMouseLocal.y - itemSize.y * 0.5f) / gridPitch);
        return new Vector2Int(predictedX, predictedY);
    }

    // 当拖拽物明显越界时，尝试自动旋转一次来贴合可用空间
    private void UpdatePreviewRotationIfNeeded(
        InventoryUIController hoveredGrid,
        PointerEventData eventData,
        ref Vector2Int hoverIndex,
        ref int width,
        ref int height)
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
        RefreshDragVisualOffsetForCenter();
        GetCurrentFootprint(_currentPreviewIsRotated, out width, out height);
        hoverIndex = GetPredictedGridIndex(hoveredGrid, eventData, width, height);
    }

    // 目标区域完全空闲时，直接完成落位
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

    // 若目标区域只挡住一个同类可堆叠物品，则优先执行堆叠合并
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

    // 同一网格内若只挡住一个物品，则尝试做一次位置交换
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

        // 先临时移开被挡住的物品，验证拖拽物是否真的可以放下
        targetController.RemoveItem(blockingItem, blockingItem._originalGridIndex.x, blockingItem._originalGridIndex.y, blockingItem._originalIsRotated);
        if (!targetController.IsSpaceAvailable(targetIndex.x, targetIndex.y, width, height))
        {
            targetController.PlaceItem(blockingItem, blockingItem._originalGridIndex.x, blockingItem._originalGridIndex.y, blockingItem._originalIsRotated);
            return false;
        }

        targetController.PlaceItem(this, targetIndex.x, targetIndex.y, _currentPreviewIsRotated);

        // 优先尝试把被交换物放回拖拽前的位置，其次再找新的空位
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

    // 拖拽失败时，恢复到原网格中的原始占位
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

    // 拖拽失败且原位置是装备槽时，尝试重新装备回去
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

    // 根据当前朝向重算 UI 尺寸，未挂网格时退回默认像素尺寸
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
        UpdateItemIconStretch(width, height);
        ConfigureItemNameTextLayout();
        ResizeSearchOverlay();
    }

    // 以物品中心作为拖拽吸附点，避免不同尺寸物体拖拽手感不一致
    private void RefreshDragVisualOffsetForCenter()
    {
        if (_rectTransform == null)
        {
            return;
        }

        Vector3 centerWorldOffset = _rectTransform.TransformVector(
            new Vector3(_rectTransform.rect.width * 0.5f, -_rectTransform.rect.height * 0.5f, 0f));
        _visualDragOffset = -centerWorldOffset;
    }

    // 收尾拖拽视觉状态，并清除高亮与缓存预览数据
    private void RestoreDragVisualState()
    {
        DestroyDragPlaceholder();
        _isDragActive = false;
        _activeDragLayer = null;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        if (_lastHoveredGrid != null)
        {
            _lastHoveredGrid.HideHighlight();
        }

        _lastHoveredGrid = null;
        _lastPreviewGrid = null;
        _hasPreviewPlacement = false;
        CurrentlyDraggedItem = null;
    }

    // 通过主控制器寻找快捷转移目标，并直接执行网格内搬运
    public bool TryQuickTransfer(out InventoryQuickTransferFailure failure)
    {
        failure = InventoryQuickTransferFailure.None;
        if (!CanInteractWithItem()) { failure = InventoryQuickTransferFailure.SearchPending; return false; }
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || _isDragActive || CurrentlyDraggedItem != null)
        { failure = InventoryQuickTransferFailure.NotInteractive; return false; }
        var screen = InventoryScreenController.Instance;
        if (screen == null || !screen.IsInventoryOpen || !screen.HasActiveExternalContainer)
        { failure = InventoryQuickTransferFailure.NoOpenSession; return false; }
        if (CurrentGrid == null || ItemData == null ||
            (CurrentGrid != screen.ActiveExternalGrid && CurrentGrid != screen.BackpackGrid) ||
            !CurrentGrid.TryGetItemView(RuntimeState, out var currentView) || currentView != this)
        { failure = InventoryQuickTransferFailure.InvalidSource; return false; }
        var intendedGrid = CurrentGrid == screen.ActiveExternalGrid ? screen.BackpackGrid : screen.ActiveExternalGrid;
        if (!InventoryGridInteractionPolicy.CanBeginDragFrom(CurrentGrid) || !InventoryGridInteractionPolicy.CanDropInto(intendedGrid) ||
            !CanBePlacedInGrid(intendedGrid))
        { failure = InventoryQuickTransferFailure.GridPolicy; return false; }
        if (InventoryStackTransfer.TryTransfer(this, intendedGrid)) return true;
        if (!screen.TryFindQuickTransferTarget(CurrentGrid, this, out InventoryUIController targetGrid, out Vector2Int position, out bool needsRotation))
        { failure = InventoryQuickTransferFailure.NoSpace; return false; }

        InventoryItemInfoPanelController.Instance?.Hide();

        CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);
        transform.SetParent(targetGrid.ItemContainer, false);
        CurrentGrid = targetGrid;
        PlaceSuccessfully(position, needsRotation);
        return true;
    }

    // 从当前 UI 射线结果里找出鼠标悬停的背包网格
    private InventoryUIController GetHoveredGrid(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            InventoryUIController grid = result.gameObject.GetComponentInParent<InventoryUIController>();
            if (grid != null && IsPointerInsideGrid(grid, eventData))
            {
                return grid;
            }
        }

        if (TryGetVisibleGridByBounds(eventData, out InventoryUIController fallbackGrid))
        {
            return fallbackGrid;
        }

        return null;
    }

    private static bool TryGetVisibleGridByBounds(PointerEventData eventData, out InventoryUIController grid)
    {
        grid = null;
        InventoryScreenController screen = InventoryScreenController.Instance;
        if (screen == null)
        {
            return false;
        }

        return TryUseGridIfPointerInside(screen.ActiveExternalGrid, eventData, out grid) ||
            TryUseGridIfPointerInside(screen.BackpackGrid, eventData, out grid) ||
            TryUseGridIfPointerInside(screen.PocketGrid, eventData, out grid) ||
            TryUseGridIfPointerInside(screen.TacticalRigGrid, eventData, out grid) ||
            TryUseGridIfPointerInside(screen.LootChestGrid, eventData, out grid);
    }

    private static bool TryUseGridIfPointerInside(
        InventoryUIController candidate,
        PointerEventData eventData,
        out InventoryUIController grid)
    {
        grid = null;
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsPointerInsideGrid(candidate, eventData))
        {
            return false;
        }

        grid = candidate;
        return true;
    }

    // 只把真正落在格子区域内的射线结果当作有效网格，避免外层面板截走跨容器拖放
    private static bool IsPointerInsideGrid(InventoryUIController grid, PointerEventData eventData)
    {
        if (grid == null || eventData == null)
        {
            return false;
        }

        Camera eventCamera = eventData.pressEventCamera;
        return IsPointerInsideRect(grid.ItemContainer, eventData.position, eventCamera) ||
            IsPointerInsideRect(grid.GridBackground as RectTransform, eventData.position, eventCamera) ||
            IsPointerInsideRect(grid.transform as RectTransform, eventData.position, eventCamera);
    }

    private static bool IsPointerInsideRect(RectTransform rectTransform, Vector2 screenPosition, Camera eventCamera)
    {
        return rectTransform != null &&
            rectTransform.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    // 优先通过显式矩形检测命中装备槽，失败后再回退到普通 UI 射线
    private EquipmentSlotUI GetHoveredEquipmentSlot(PointerEventData eventData)
    {
        if (InventoryScreenController.Instance != null)
        {
            EquipmentSlotUI slotFromScreenPoint = InventoryScreenController.Instance.GetEquipmentSlotAtScreenPosition(
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

    // 获取当前物品在指定朝向下的占格尺寸
    private void GetCurrentFootprint(bool isRotated, out int width, out int height)
    {
        GetCurrentFootprint(isRotated, this, out width, out height);
    }

    // 供交换逻辑复用的静态占格计算
    private static void GetCurrentFootprint(bool isRotated, DraggableItemUI itemUI, out int width, out int height)
    {
        width = isRotated ? itemUI.ItemData.Height : itemUI.ItemData.Width;
        height = isRotated ? itemUI.ItemData.Width : itemUI.ItemData.Height;
    }

    // 从集合中安全取出唯一物品引用
    private static DraggableItemUI GetSingleItem(HashSet<DraggableItemUI> items)
    {
        foreach (DraggableItemUI item in items)
        {
            return item;
        }

        return null;
    }
}
