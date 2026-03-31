using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包网格视图。
/// 负责坐标换算、高亮显示、格子背景绘制，以及和物品 view 的装配。
/// </summary>
public class InventoryUIController : MonoBehaviour
{
    [Header("View References")]
    public RectTransform ItemContainer;
    public Transform GridBackground;
    public float CellSize = 50f;
    public float Spacing = 2f;

    [Header("Highlight")]
    public RectTransform Highlighter;

    private Image _highlighterImage;
    private InventoryGridController _gridController;

    private void Awake()
    {
        _gridController = GetComponent<InventoryGridController>();
        ConfigureHighlighter();
        ConfigureGridLayerTransforms(Vector2.zero);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        _gridController = GetComponent<InventoryGridController>();
        ConfigureHighlighter();

        int cols = _gridController != null ? Mathf.Max(1, _gridController.Columns) : 1;
        int rows = _gridController != null ? Mathf.Max(1, _gridController.Rows) : 1;
        ConfigureGridLayerTransforms(GetItemActualSize(cols, rows));
    }

    private void Start()
    {
        RefreshBlockedCellVisuals();
    }

    /// <summary>
    /// 将格子索引换算为 ItemContainer 内左上角坐标系下的局部坐标。
    /// </summary>
    public Vector2 GetLocalPosition(int x, int y)
    {
        float posX = x * (CellSize + Spacing);
        float posY = -y * (CellSize + Spacing);
        return new Vector2(posX, posY);
    }

    /// <summary>
    /// 将局部坐标换算回格子索引。
    /// </summary>
    public Vector2Int GetGridIndex(Vector2 localPosition)
    {
        int x = Mathf.FloorToInt(localPosition.x / (CellSize + Spacing));
        int y = Mathf.FloorToInt(-localPosition.y / (CellSize + Spacing));
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 将屏幕坐标转换为以网格左上角为原点的局部坐标。
    /// </summary>
    public Vector2 GetGridLocalPoint(Vector2 screenPosition, Camera eventCamera)
    {
        if (ItemContainer == null)
        {
            return Vector2.zero;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(ItemContainer, screenPosition, eventCamera, out Vector2 pivotLocalPoint);

        Rect rect = ItemContainer.rect;
        Vector2 pivot = ItemContainer.pivot;
        Vector2 topLeftOffset = new Vector2(rect.width * pivot.x, -rect.height * (1f - pivot.y));
        return pivotLocalPoint + topLeftOffset;
    }

    /// <summary>
    /// 计算物品在 UI 中应占据的实际像素尺寸。
    /// </summary>
    public Vector2 GetItemActualSize(int width, int height)
    {
        float actualWidth = width * CellSize + (width - 1) * Spacing;
        float actualHeight = height * CellSize + (height - 1) * Spacing;
        return new Vector2(actualWidth, actualHeight);
    }

    /// <summary>
    /// 显示拖拽预测高亮。
    /// </summary>
    public void ShowHighlight(int x, int y, int width, int height, bool isValid)
    {
        if (Highlighter == null)
        {
            return;
        }

        Highlighter.gameObject.SetActive(true);
        Highlighter.transform.SetAsFirstSibling();
        Highlighter.anchoredPosition = GetLocalPosition(x, y);
        Highlighter.sizeDelta = GetItemActualSize(width, height);

        if (_highlighterImage == null)
        {
            _highlighterImage = Highlighter.GetComponent<Image>();
        }

        if (_highlighterImage != null)
        {
            _highlighterImage.color = isValid
                ? new Color(0f, 1f, 0f, 0.35f)
                : new Color(1f, 0f, 0f, 0.35f);
        }
    }

    /// <summary>
    /// 隐藏拖拽预测高亮。
    /// </summary>
    public void HideHighlight()
    {
        if (Highlighter != null)
        {
            Highlighter.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 获取绑定在当前对象上的网格控制器。
    /// </summary>
    public InventoryGridController GetGridController()
    {
        if (_gridController == null)
        {
            _gridController = GetComponent<InventoryGridController>();
        }

        return _gridController;
    }

    /// <summary>
    /// 用存档数据重建当前网格里的物品视图。
    /// </summary>
    public void LoadFromData(List<ContainerItemSaveData> saveDataList)
    {
        LoadFromRuntimeState(saveDataList, null);
    }

    /// <summary>
    /// 用完整容器快照重建当前网格里的物品和特殊格状态。
    /// </summary>
    public void LoadFromRuntimeState(List<ContainerItemSaveData> saveDataList, List<ContainerCellStateSaveData> cellStates)
    {
        ClearUI();

        if (saveDataList != null)
        {
            foreach (ContainerItemSaveData data in saveDataList)
            {
                if (data?.ItemData == null)
                {
                    continue;
                }

                DraggableItemUI itemView = InventoryItemFactory.Instance.SpawnItemInGrid(
                    data.ItemData,
                    this,
                    data.X,
                    data.Y,
                    data.Amount,
                    data.IsRotated,
                    CloneSaveDataList(data.InternalItems),
                    CloneCellStateList(data.InternalCellStates));
                if (itemView != null)
                {
                    itemView.ApplyContainerRuntimeState(data);
                }
            }
        }

        GetGridController().ApplyRuntimeCellStates(CloneCellStateList(cellStates));
    }

    /// <summary>
    /// 从当前物品视图提取可持久化数据。
    /// </summary>
    public List<ContainerItemSaveData> ExtractSaveData()
    {
        List<ContainerItemSaveData> saveDataList = new List<ContainerItemSaveData>();
        if (ItemContainer == null)
        {
            return saveDataList;
        }

        foreach (Transform child in ItemContainer)
        {
            if (child == Highlighter)
            {
                continue;
            }

            DraggableItemUI itemView = child.GetComponent<DraggableItemUI>();
            if (itemView != null && itemView.ItemData != null)
            {
                saveDataList.Add(itemView.CreateSaveDataSnapshot());
            }
        }

        return saveDataList;
    }

    /// <summary>
    /// 提取当前容器内需要持久化的特殊格状态。
    /// </summary>
    public List<ContainerCellStateSaveData> ExtractCellStateData()
    {
        return GetGridController().ExtractRuntimeCellStates();
    }

    /// <summary>
    /// 清空当前网格里的所有物品 view，并同步清空动态占用数据。
    /// </summary>
    public void ClearUI()
    {
        if (ItemContainer != null)
        {
            List<GameObject> objectsToDestroy = new List<GameObject>();
            foreach (Transform child in ItemContainer)
            {
                if (child == Highlighter)
                {
                    continue;
                }

                if (child.GetComponent<DraggableItemUI>() != null)
                {
                    objectsToDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject target in objectsToDestroy)
            {
                Destroy(target);
            }
        }

        GetGridController().ClearDynamicCells();
        HideHighlight();
    }

    /// <summary>
    /// 根据当前物品数据重新计算更优摆放方案。
    /// </summary>
    public void AutoSort()
    {
        List<ContainerItemSaveData> currentItems = ExtractSaveData();
        if (currentItems.Count == 0)
        {
            return;
        }

        if (!InventoryAutoSortService.TryBuildSortedLayout(
                GetGridController().Columns,
                GetGridController().Rows,
                GetGridController().BlockedCells,
                currentItems,
                out List<ContainerItemSaveData> sortedLayout))
        {
            Debug.LogError($"[{name}] Auto sort failed because at least one item could not be placed.");
            return;
        }

        ClearUI();
        LoadFromData(sortedLayout);
    }

    /// <summary>
    /// 按新的列数、行数和阻塞格重建网格视图。
    /// </summary>
    public void RebuildGridUI(int cols, int rows, List<Vector2Int> blockedCells)
    {
        gameObject.SetActive(true);
        GetGridController().ConfigureGrid(cols, rows, blockedCells);

        Vector2 gridSize = GetItemActualSize(cols, rows);
        ResizeGrid(gridSize);
        ConfigureGridLayerTransforms(gridSize);
        RebuildBackgroundCells(cols, rows, blockedCells);
        ForceLayoutRefresh();
        HideHighlight();
    }

    private void ConfigureHighlighter()
    {
        if (Highlighter == null)
        {
            return;
        }

        _highlighterImage = Highlighter.GetComponent<Image>();
        Highlighter.gameObject.SetActive(false);
        Highlighter.anchorMin = new Vector2(0f, 1f);
        Highlighter.anchorMax = new Vector2(0f, 1f);
        Highlighter.pivot = new Vector2(0f, 1f);
    }

    private void RefreshBlockedCellVisuals()
    {
        if (GridBackground == null)
        {
            return;
        }

        HashSet<Vector2Int> blockedSet = new HashSet<Vector2Int>(GetGridController().BlockedCells);
        for (int index = 0; index < GridBackground.childCount; index++)
        {
            int x = index % GetGridController().Columns;
            int y = index / GetGridController().Columns;
            Image cellImage = GridBackground.GetChild(index).GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.enabled = !blockedSet.Contains(new Vector2Int(x, y));
            }
        }
    }

    private void ResizeGrid(Vector2 gridSize)
    {
        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            selfRect.sizeDelta = gridSize;
        }

        if (GridBackground is RectTransform backgroundRect)
        {
            backgroundRect.sizeDelta = gridSize;
        }

        if (ItemContainer != null)
        {
            ItemContainer.sizeDelta = gridSize;
        }
    }

    private void ConfigureGridLayerTransforms(Vector2 gridSize)
    {
        if (GridBackground is RectTransform backgroundRect)
        {
            ConfigureTopLeftLayer(backgroundRect, gridSize);
        }

        if (ItemContainer != null)
        {
            ConfigureTopLeftLayer(ItemContainer, gridSize);
        }
    }

    private static void ConfigureTopLeftLayer(RectTransform rectTransform, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private void RebuildBackgroundCells(int cols, int rows, List<Vector2Int> blockedCells)
    {
        if (GridBackground == null)
        {
            return;
        }

        foreach (Transform child in GridBackground)
        {
            Destroy(child.gameObject);
        }

        GridLayoutGroup layout = GridBackground.GetComponent<GridLayoutGroup>();
        if (layout != null)
        {
            layout.cellSize = new Vector2(CellSize, CellSize);
            layout.spacing = new Vector2(Spacing, Spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = cols;
        }

        HashSet<Vector2Int> blockedSet = new HashSet<Vector2Int>(blockedCells ?? new List<Vector2Int>());
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}", typeof(Image));
                cell.transform.SetParent(GridBackground, false);

                Image image = cell.GetComponent<Image>();
                image.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
                image.enabled = !blockedSet.Contains(new Vector2Int(x, y));
            }
        }
    }

    private void ForceLayoutRefresh()
    {
        if (GridBackground is RectTransform backgroundRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
        }

        if (transform.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
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
}

/// <summary>
/// 背包整理服务。
/// 只负责根据当前数据计算新的摆放结果，不直接操作 UI。
/// </summary>
public static class InventoryAutoSortService
{
    public static List<ContainerItemSaveData> BuildPackedLayout(
        int columns,
        int rows,
        List<Vector2Int> blockedCells,
        IReadOnlyList<ContainerItemSaveData> sourceItems)
    {
        List<ContainerItemSaveData> packedLayout = new List<ContainerItemSaveData>();
        if (sourceItems == null || sourceItems.Count == 0)
        {
            return packedLayout;
        }

        List<ContainerItemSaveData> itemsToPlace = BuildSortedPlacementCandidates(sourceItems);
        InventoryGridModel layoutModel = new InventoryGridModel();
        layoutModel.Configure(columns, rows, blockedCells, true);

        foreach (ContainerItemSaveData item in itemsToPlace)
        {
            if (!layoutModel.FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out Vector2Int position, out bool needsRotation))
            {
                continue;
            }

            int width = needsRotation ? item.ItemData.Height : item.ItemData.Width;
            int height = needsRotation ? item.ItemData.Width : item.ItemData.Height;

            layoutModel.PlaceItem(null, position.x, position.y, width, height, needsRotation);
            item.X = position.x;
            item.Y = position.y;
            item.IsRotated = needsRotation;
            packedLayout.Add(item);
        }

        return packedLayout;
    }

    public static bool TryBuildSortedLayout(
        int columns,
        int rows,
        List<Vector2Int> blockedCells,
        IReadOnlyList<ContainerItemSaveData> sourceItems,
        out List<ContainerItemSaveData> sortedLayout)
    {
        sortedLayout = new List<ContainerItemSaveData>();
        if (sourceItems == null || sourceItems.Count == 0)
        {
            return true;
        }

        List<ContainerItemSaveData> itemsToPlace = BuildSortedPlacementCandidates(sourceItems);

        InventoryGridModel layoutModel = new InventoryGridModel();
        layoutModel.Configure(columns, rows, blockedCells, true);

        foreach (ContainerItemSaveData item in itemsToPlace)
        {
            if (!layoutModel.FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out Vector2Int position, out bool needsRotation))
            {
                sortedLayout.Clear();
                return false;
            }

            int width = needsRotation ? item.ItemData.Height : item.ItemData.Width;
            int height = needsRotation ? item.ItemData.Width : item.ItemData.Height;

            layoutModel.PlaceItem(null, position.x, position.y, width, height, needsRotation);
            item.X = position.x;
            item.Y = position.y;
            item.IsRotated = needsRotation;
            sortedLayout.Add(item);
        }

        return true;
    }

    private static List<ContainerItemSaveData> BuildSortedPlacementCandidates(IReadOnlyList<ContainerItemSaveData> sourceItems)
    {
        Dictionary<InventoryItemData, ContainerItemSaveData> mergedStackables = new Dictionary<InventoryItemData, ContainerItemSaveData>();
        List<ContainerItemSaveData> nonStackables = new List<ContainerItemSaveData>();

        foreach (ContainerItemSaveData item in sourceItems)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            if (item.ItemData.IsStackable)
            {
                if (!mergedStackables.TryGetValue(item.ItemData, out ContainerItemSaveData mergedItem))
                {
                    mergedItem = item.DeepCopy();
                    mergedItem.Amount = 0;
                    mergedStackables[item.ItemData] = mergedItem;
                }

                mergedItem.Amount += item.Amount;
                mergedItem.RequiresSearch |= item.RequiresSearch;
                mergedItem.IsSearched &= item.IsSearched;
                mergedItem.SearchProgressSeconds = Mathf.Max(mergedItem.SearchProgressSeconds, item.SearchProgressSeconds);
                mergedItem.SearchDurationSeconds = Mathf.Max(mergedItem.SearchDurationSeconds, item.SearchDurationSeconds);
                continue;
            }

            nonStackables.Add(item.DeepCopy());
        }

        List<ContainerItemSaveData> itemsToPlace = new List<ContainerItemSaveData>(nonStackables);
        foreach (KeyValuePair<InventoryItemData, ContainerItemSaveData> stackableGroup in mergedStackables)
        {
            int remainingAmount = stackableGroup.Value.Amount;
            int maxStack = Mathf.Max(1, stackableGroup.Key.MaxStack);

            while (remainingAmount > 0)
            {
                int amountToCreate = Mathf.Min(remainingAmount, maxStack);
                ContainerItemSaveData stackItem = stackableGroup.Value.DeepCopy();
                stackItem.Amount = amountToCreate;
                itemsToPlace.Add(stackItem);
                remainingAmount -= amountToCreate;
            }
        }

        itemsToPlace.Sort(CompareItemPriority);
        return itemsToPlace;
    }

    private static int CompareItemPriority(ContainerItemSaveData left, ContainerItemSaveData right)
    {
        int leftArea = left.ItemData.Width * left.ItemData.Height;
        int rightArea = right.ItemData.Width * right.ItemData.Height;

        if (leftArea != rightArea)
        {
            return rightArea.CompareTo(leftArea);
        }

        return left.ItemData.Type.CompareTo(right.ItemData.Type);
    }
}
