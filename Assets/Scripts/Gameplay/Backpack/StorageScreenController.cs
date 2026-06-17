using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coordinates the warehouse version of the inventory screen.
/// </summary>
[DisallowMultipleComponent]
public sealed class StorageScreenController : MonoBehaviour
{
    private const string StorageTitle = "\u4ed3\u5e93";
    private const int StorageColumns = 6;
    private const int StorageRows = 10;
    private const int StorageMinimumPageCount = 10;
    private const float PageSelectorGap = 12f;
    private const float PageSelectorWidth = 64f;
    private const float PageSelectorHeight = 560f;
    private const float PageButtonWidth = 52f;
    private const float PageButtonHeight = 38f;
    private const float PageButtonSpacing = 10f;

    [Header("References")]
    public InventoryScreenController InventoryScreen;
    public PlayerStorageService StorageService;
    public InventoryUIController StorageGrid;
    public Text StorageHeaderText;
    public Button StorageSortButton;
    public RectTransform PageButtonContainer;
    public Button PageButtonTemplate;

    [Header("Storage Grid")]
    public int Columns = 6;
    public int Rows = 10;
    public int MinimumPageCount = 10;
    public float CellSize = 70f;
    public float Spacing = 4f;

    [Header("Runtime")]
    public bool OpenOnStart = true;
    public string FallbackAgentId = "default_player";
    public float HeaderRefreshInterval = 0.2f;

    [Header("Test Seed")]
    public bool SeedBackpackWithTestItems = true;
    public bool SeedOnlyWhenBackpackIsEmpty = true;
    [Min(1)] public int TestBackpackItemCount = 6;

    private readonly List<Button> _pageButtons = new List<Button>();
    private InventoryScreenSessionContext _sessionContext;
    private int _currentPageIndex;
    private int _lastHeaderPage = -1;
    private int _lastHeaderPageCount = -1;
    private int _lastHeaderOccupied = -1;
    private float _nextHeaderRefreshTime;
    private bool _isSwitchingPage;
    private bool _hasSeededBackpackTestItems;

    private void Awake()
    {
        RemoveRaidOnlyHud();
        ApplyStorageShapeDefaults();
        ResolveReferences();
        ConfigureStorageService();
        ConfigureStorageGridMetrics();
        if (PageButtonTemplate != null)
        {
            PageButtonTemplate.gameObject.SetActive(false);
        }

        RepairPageButtonViewport();
        ConfigureStorageSortButton();
    }

    private void OnEnable()
    {
        RemoveRaidOnlyHud();
    }

    private void Start()
    {
        if (OpenOnStart)
        {
            OpenStorage();
        }
    }

    private void Update()
    {
        if (!IsSessionOpen())
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now < _nextHeaderRefreshTime)
        {
            return;
        }

        _nextHeaderRefreshTime = now + Mathf.Max(0.05f, HeaderRefreshInterval);
        SaveCurrentPageFromGrid();
        if (StorageService != null && StorageService.EnsureTrailingBlankPage())
        {
            RebuildPageButtons();
        }

        RefreshHeader(false);
    }

    private void LateUpdate()
    {
        if (_sessionContext != null)
        {
            ForceBackpackGridOpen(false);
        }
    }

    private void OnDisable()
    {
        if (IsSessionOpen())
        {
            SaveCurrentPageFromGrid();
            StorageService?.Save();
        }
    }

    public void OpenStorage()
    {
        ApplyStorageShapeDefaults();
        ResolveReferences();
        ConfigureStorageService();
        ConfigureStorageGridMetrics();

        if (InventoryScreen == null || StorageService == null || StorageGrid == null)
        {
            Debug.LogWarning("[StorageScreenController] Cannot open storage because required references are missing.", this);
            return;
        }

        string agentId = ResolveAgentId();
        StorageService.LoadForAgent(agentId);
        StorageService.EnsurePageCount(MinimumPageCount);
        StorageService.EnsureTrailingBlankPage();
        _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, StorageService.PageCount - 1);

        InventoryScreenSessionContext context = null;
        context = new InventoryScreenSessionContext
        {
            DisplayName = StorageTitle,
            ExternalContainerName = StorageTitle,
            ExternalContainerKind = InventoryExternalContainerKind.Storage,
            ExternalColumns = Mathf.Max(1, Columns),
            ExternalRows = Mathf.Max(1, Rows),
            ExternalBlockedCells = new List<Vector2Int>(),
            ExternalItems = StorageService.GetPageItems(_currentPageIndex),
            ExternalCellStates = StorageService.GetPageCellStates(_currentPageIndex),
            OnClose = result => HandleStorageSessionClosed(context, result)
        };

        _sessionContext = context;
        InventoryScreen.OpenInventorySession(context);
        InventoryScreen.RefreshBackpackGridForExternalSession();
        ForceBackpackGridOpen(true);
        SeedBackpackTestItemsIfNeeded();
        RebuildPageButtons();
        RefreshHeader(true);
    }

    public void SelectPage(int pageIndex)
    {
        if (_isSwitchingPage || StorageService == null || StorageGrid == null)
        {
            return;
        }

        int targetPage = Mathf.Clamp(pageIndex, 0, Mathf.Max(0, StorageService.PageCount - 1));
        if (targetPage == _currentPageIndex)
        {
            return;
        }

        _isSwitchingPage = true;
        SaveCurrentPageFromGrid();
        StorageService.EnsureTrailingBlankPage();
        StorageService.Save();
        _currentPageIndex = targetPage;
        LoadCurrentPageIntoGrid();
        RebuildPageButtons();
        RefreshHeader(true);
        _isSwitchingPage = false;
    }

    public void SortCurrentPage()
    {
        if (!IsSessionOpen() || StorageGrid == null)
        {
            return;
        }

        StorageGrid.AutoSort();
        SaveCurrentPageFromGrid();
        StorageService?.EnsureTrailingBlankPage();
        StorageService?.Save();
        RebuildPageButtons();
        RefreshHeader(true);
    }

    private void HandleStorageSessionClosed(
        InventoryScreenSessionContext context,
        InventoryScreenSessionResult result)
    {
        if (context == null || _sessionContext != context)
        {
            return;
        }

        if (StorageService != null && result != null)
        {
            StorageService.SetPageRuntimeState(
                _currentPageIndex,
                result.ExternalItems,
                result.ExternalCellStates);
            StorageService.EnsureTrailingBlankPage();
            StorageService.Save();
        }

        _sessionContext = null;
        RefreshHeader(true);
    }

    private void SaveCurrentPageFromGrid()
    {
        if (StorageService == null || StorageGrid == null)
        {
            return;
        }

        StorageService.SetPageRuntimeState(
            _currentPageIndex,
            StorageGrid.ExtractSaveData(),
            StorageGrid.ExtractCellStateData());
    }

    private void LoadCurrentPageIntoGrid()
    {
        if (StorageService == null || StorageGrid == null)
        {
            return;
        }

        ConfigureStorageGridMetrics();
        StorageGrid.RebuildGridUI(
            Mathf.Max(1, Columns),
            Mathf.Max(1, Rows),
            new List<Vector2Int>());
        StorageGrid.LoadFromRuntimeState(
            StorageService.GetPageItems(_currentPageIndex),
            StorageService.GetPageCellStates(_currentPageIndex));
    }

    private void RebuildPageButtons()
    {
        if (PageButtonContainer == null || PageButtonTemplate == null || StorageService == null)
        {
            return;
        }

        RepairPageButtonViewport();
        for (int i = _pageButtons.Count - 1; i >= 0; i--)
        {
            Button button = _pageButtons[i];
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        _pageButtons.Clear();
        PageButtonTemplate.gameObject.SetActive(false);

        int pageCount = Mathf.Max(1, StorageService.PageCount);
        for (int i = 0; i < pageCount; i++)
        {
            Button button = Instantiate(PageButtonTemplate, PageButtonContainer);
            button.name = $"Page_{i + 1}";
            button.gameObject.SetActive(true);
            NormalizePageButtonRect(button);
            button.onClick.RemoveAllListeners();
            int capturedPage = i;
            button.onClick.AddListener(() => SelectPage(capturedPage));

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = (i + 1).ToString();
            }

            ApplyPageButtonVisual(button, i == _currentPageIndex);
            _pageButtons.Add(button);
        }

        UpdatePageButtonContainerSize(pageCount);
    }

    private void UpdatePageButtonContainerSize(int pageCount)
    {
        GridLayoutGroup grid = PageButtonContainer.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            return;
        }

        int columns = Mathf.Max(1, grid.constraintCount);
        int rowsNeeded = Mathf.CeilToInt(pageCount / (float)columns);
        float width = columns * grid.cellSize.x + Mathf.Max(0, columns - 1) * grid.spacing.x;
        float height = rowsNeeded * grid.cellSize.y + Mathf.Max(0, rowsNeeded - 1) * grid.spacing.y;
        PageButtonContainer.sizeDelta = new Vector2(width, height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(PageButtonContainer);

        ScrollRect scrollRect = PageButtonContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            RectTransform viewport = scrollRect.viewport;
            if (viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            }
        }
    }

    private static void ApplyPageButtonVisual(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Color normal = selected
            ? new Color(0.2f, 0.66f, 0.95f, 1f)
            : new Color(0.56f, 0.84f, 1f, 1f);
        Color highlighted = selected
            ? new Color(0.3f, 0.76f, 1f, 1f)
            : new Color(0.68f, 0.91f, 1f, 1f);
        Color pressed = new Color(0.12f, 0.48f, 0.78f, 1f);

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = true;
            image.raycastTarget = true;
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        colors.disabledColor = new Color(0.3f, 0.38f, 0.45f, 0.55f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = selected ? Color.white : new Color(0.08f, 0.21f, 0.32f, 1f);
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private void RefreshHeader(bool force)
    {
        if (StorageHeaderText == null || StorageService == null)
        {
            return;
        }

        int pageCount = Mathf.Max(1, StorageService.PageCount);
        int occupied = IsSessionOpen() && StorageGrid != null
            ? CountOccupiedCells(StorageGrid.ExtractSaveData())
            : StorageService.CountOccupiedCells(_currentPageIndex);

        if (!force &&
            _lastHeaderPage == _currentPageIndex &&
            _lastHeaderPageCount == pageCount &&
            _lastHeaderOccupied == occupied)
        {
            return;
        }

        _lastHeaderPage = _currentPageIndex;
        _lastHeaderPageCount = pageCount;
        _lastHeaderOccupied = occupied;
        StorageHeaderText.text = $"{StorageTitle} {_currentPageIndex + 1}/{pageCount} ({occupied}/{StorageService.PageCapacity})";
    }

    private int CountOccupiedCells(List<ContainerItemSaveData> items)
    {
        int occupied = 0;
        if (items == null)
        {
            return occupied;
        }

        foreach (ContainerItemSaveData item in items)
        {
            if (item?.ItemData == null)
            {
                continue;
            }

            int width = item.IsRotated ? item.ItemData.Height : item.ItemData.Width;
            int height = item.IsRotated ? item.ItemData.Width : item.ItemData.Height;
            occupied += Mathf.Max(1, width) * Mathf.Max(1, height);
        }

        return occupied;
    }

    private bool IsSessionOpen()
    {
        return _sessionContext != null &&
            InventoryScreen != null &&
            InventoryScreen.IsSessionContextActive(_sessionContext);
    }

    private string ResolveAgentId()
    {
        if (InventoryScreen != null && !string.IsNullOrWhiteSpace(InventoryScreen.ActiveInventoryAgentId))
        {
            return InventoryScreen.ActiveInventoryAgentId;
        }

        return string.IsNullOrWhiteSpace(FallbackAgentId) ? "default_player" : FallbackAgentId.Trim();
    }

    private void ConfigureStorageService()
    {
        if (StorageService == null)
        {
            return;
        }

        StorageService.FallbackAgentId = FallbackAgentId;
        StorageService.Configure(Columns, Rows, MinimumPageCount);
    }

    private void ConfigureStorageGridMetrics()
    {
        if (StorageGrid == null)
        {
            return;
        }

        StorageGrid.CellSize = Mathf.Max(12f, CellSize);
        StorageGrid.Spacing = Mathf.Max(0f, Spacing);
    }

    private void ApplyStorageShapeDefaults()
    {
        Columns = StorageColumns;
        Rows = StorageRows;
        MinimumPageCount = Mathf.Max(StorageMinimumPageCount, MinimumPageCount);
    }

    private void ResolveReferences()
    {
        if (InventoryScreen == null)
        {
            InventoryScreen = GetComponent<InventoryScreenController>() ?? GetComponentInChildren<InventoryScreenController>(true);
        }

        if (StorageService == null)
        {
            StorageService = GetComponent<PlayerStorageService>() ?? GetComponentInChildren<PlayerStorageService>(true);
        }

        if (InventoryScreen != null && StorageGrid == null)
        {
            StorageGrid = InventoryScreen.LootChestGrid;
        }

        if (StorageHeaderText == null)
        {
            StorageHeaderText = FindTextByName("StorageHeaderText") ?? FindTextByName("LootHeaderText");
        }

        if (StorageSortButton == null)
        {
            StorageSortButton = FindButtonByName("StorageSortButton");
        }
    }

    private Text FindTextByName(string objectName)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text != null && text.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private void ConfigureStorageSortButton()
    {
        if (StorageSortButton == null)
        {
            return;
        }

        StorageSortButton.onClick.RemoveListener(SortCurrentPage);
        StorageSortButton.onClick.AddListener(SortCurrentPage);
    }

    private void ForceBackpackGridOpen(bool refreshBackground)
    {
        if (InventoryScreen == null || InventoryScreen.BackpackGrid == null)
        {
            return;
        }

        InventoryUIController backpackGrid = InventoryScreen.BackpackGrid;
        ActivateTransformChain(backpackGrid.transform, InventoryScreen.InventoryPanel != null
            ? InventoryScreen.InventoryPanel.transform
            : null);

        backpackGrid.gameObject.SetActive(true);
        backpackGrid.enabled = true;

        if (backpackGrid.GridBackground != null)
        {
            backpackGrid.GridBackground.gameObject.SetActive(true);
        }

        if (backpackGrid.ItemContainer != null)
        {
            backpackGrid.ItemContainer.gameObject.SetActive(true);
        }

        if (!refreshBackground)
        {
            return;
        }

        InventoryGridController gridController = backpackGrid.GetGridController();
        if (gridController != null && (gridController.Columns != 5 || gridController.Rows != 6))
        {
            backpackGrid.RebuildGridUI(5, 6, new List<Vector2Int>());
            return;
        }

        backpackGrid.RefreshBackgroundCellsFromCurrentConfig();
    }

    private void SeedBackpackTestItemsIfNeeded()
    {
        if (!SeedBackpackWithTestItems ||
            _hasSeededBackpackTestItems ||
            InventoryScreen == null ||
            InventoryScreen.BackpackGrid == null)
        {
            return;
        }

        InventoryUIController backpackGrid = InventoryScreen.BackpackGrid;
        if (SeedOnlyWhenBackpackIsEmpty && backpackGrid.ExtractSaveData().Count > 0)
        {
            _hasSeededBackpackTestItems = true;
            return;
        }

        if (InventoryItemFactory.Instance == null)
        {
            Debug.LogWarning("[StorageScreenController] Cannot seed backpack test items because InventoryItemFactory is missing.", this);
            return;
        }

        InventoryItemDatabase database = Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase");
        if (database == null || database.Items == null || database.Items.Count == 0)
        {
            Debug.LogWarning("[StorageScreenController] Cannot seed backpack test items because InventoryItemDatabase is empty.", this);
            return;
        }

        int placedCount = 0;
        foreach (InventoryItemData itemData in database.Items)
        {
            if (!IsBackpackTestSeedCandidate(itemData))
            {
                continue;
            }

            if (!backpackGrid.GetGridController().FindFirstAvailableSpace(
                    Mathf.Max(1, itemData.Width),
                    Mathf.Max(1, itemData.Height),
                    out Vector2Int position,
                    out bool needsRotation))
            {
                continue;
            }

            int amount = itemData.IsStackable
                ? Mathf.Clamp(3, 1, Mathf.Max(1, itemData.MaxStack))
                : 1;
            DraggableItemUI itemView = InventoryItemFactory.Instance.SpawnItemInGrid(
                itemData,
                backpackGrid,
                position.x,
                position.y,
                amount,
                needsRotation);

            if (itemView == null)
            {
                continue;
            }

            placedCount++;
            if (placedCount >= Mathf.Max(1, TestBackpackItemCount))
            {
                break;
            }
        }

        _hasSeededBackpackTestItems = placedCount > 0;
    }

    private static bool IsBackpackTestSeedCandidate(InventoryItemData itemData)
    {
        if (itemData == null || itemData.Width <= 0 || itemData.Height <= 0)
        {
            return false;
        }

        return itemData.Type != ItemType.Bag && itemData.Type != ItemType.Rig;
    }

    private static void ActivateTransformChain(Transform child, Transform stopAt)
    {
        Transform current = child;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            if (current == stopAt)
            {
                return;
            }

            current = current.parent;
        }
    }

    private void RepairPageButtonViewport()
    {
        if (PageButtonContainer == null)
        {
            return;
        }

        ScrollRect scrollRect = PageButtonContainer.GetComponentInParent<ScrollRect>(true);
        if (scrollRect == null)
        {
            return;
        }

        RectTransform scrollTransform = scrollRect.GetComponent<RectTransform>();
        if (scrollTransform != null)
        {
            scrollTransform.anchorMin = new Vector2(1f, 0.5f);
            scrollTransform.anchorMax = new Vector2(1f, 0.5f);
            scrollTransform.pivot = new Vector2(0f, 0.5f);
            scrollTransform.anchoredPosition = new Vector2(PageSelectorGap, 0f);
            scrollTransform.sizeDelta = new Vector2(PageSelectorWidth, PageSelectorHeight);
            scrollTransform.localScale = Vector3.one;
            scrollTransform.localRotation = Quaternion.identity;
        }

        Image scrollBackground = scrollRect.GetComponent<Image>();
        if (scrollBackground != null && scrollBackground.color.a < 0.35f)
        {
            scrollBackground.color = new Color(0.04f, 0.1f, 0.14f, 0.88f);
        }

        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
        {
            return;
        }

        Mask mask = viewport.GetComponent<Mask>();
        if (mask != null)
        {
            mask.enabled = false;
        }

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.raycastTarget = false;
        }

        PageButtonContainer.anchorMin = new Vector2(0f, 1f);
        PageButtonContainer.anchorMax = new Vector2(0f, 1f);
        PageButtonContainer.pivot = new Vector2(0f, 1f);
        PageButtonContainer.anchoredPosition = Vector2.zero;
        PageButtonContainer.localScale = Vector3.one;
        PageButtonContainer.localRotation = Quaternion.identity;

        GridLayoutGroup grid = PageButtonContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(PageButtonWidth, PageButtonHeight);
            grid.spacing = new Vector2(0f, PageButtonSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
        }
    }

    private static void NormalizePageButtonRect(Button button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }
    }

    private static void RemoveRaidOnlyHud()
    {
        RaidMinimapController[] minimaps = FindObjectsOfType<RaidMinimapController>(true);
        for (int i = 0; i < minimaps.Length; i++)
        {
            RaidMinimapController minimap = minimaps[i];
            if (minimap != null)
            {
                Object target = minimap.name == "RaidMinimapController" && minimap.transform.parent == null
                    ? minimap.gameObject
                    : minimap;
                DestroyUnityObject(target);
            }
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "RaidMinimapCanvas")
            {
                DestroyUnityObject(canvas.gameObject);
            }
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
