using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShopScreenController : MonoBehaviour
{
    private const string StorageTitle = "仓库";
    private const string ShopTitle = "售卖处";
    private const string DefaultReturnSceneName = "Scene_PreparationInterface";
    private const int StorageColumns = 6;
    private const int StorageRows = 10;
    private const int StorageMinimumPageCount = 10;
    private const int ShopColumns = 4;
    private const int ShopRows = 4;
    private const int RefreshMinutes = 30;
    private const float UiRefreshInterval = 0.2f;
    private const float RaidHudCleanupDurationSeconds = 3f;
    private const string CurrencyIconAssetPath = "Assets/Art/Sprites/Png_Item_store/Png_Item_currency.PNG";
    private const float ShopCellSize = 96f;
    private const float ShopCellSpacing = 6f;
    private const float ShopPanelWidth = 520f;
    private const float ShopPanelHeight = 610f;
    private const float StoragePanelLeftInset = 220f;
    private const float ShopPanelRightInset = 220f;
    private const float ShopGridTopInset = 120f;
    private const string ReturnButtonAssetPath = "Assets/Art/Sprites/UI/Button/IMG_0494.PNG";
    private const float ReturnButtonWidth = 148f;
    private const float ReturnButtonHeight = 64f;
    private const float ItemCurrencyIconSize = 30f;
    private const float ItemCurrencyLabelHeight = 38f;
    private const int ItemCurrencyAmountMaxFontSize = 17;
    private const int ItemCurrencyAmountMinFontSize = 11;
    private const float ShopScreenItemNameHeight = 25f;
    private const int ShopScreenItemNameMaxFontSize = 16;
    private const int ShopScreenItemNameMinFontSize = 10;
    private const float PopupCurrencyIconSize = 40f;
    private const float HeaderCurrencyIconSize = 40f;

    [Header("References")]
    public InventoryScreenController InventoryScreen;
    public PlayerStorageService StorageService;
    public PlayerEconomyService EconomyService;
    public TotemShopStockService StockService;
    public InventoryItemDatabase ItemDatabase;
    public TotemShopPool ShopPool;
    public Sprite CurrencyIcon;
    public Sprite ReturnButtonSprite;
    public GameObject StoragePanel;
    public InventoryUIController StorageGrid;
    public InventoryUIController ShopGrid;
    public Text StorageHeaderText;
    public Text GoldText;
    public Text ShopHeaderText;
    public Text RefreshCountdownText;
    public Button StorageSortButton;
    public Button ReturnButton;
    public RectTransform PageButtonContainer;
    public Button PageButtonTemplate;

    [Header("Runtime")]
    public bool OpenOnStart = true;
    public string FallbackAgentId = "default_player";
    public string ReturnSceneName = DefaultReturnSceneName;
    public float HeaderRefreshInterval = UiRefreshInterval;

    private readonly List<Button> _pageButtons = new List<Button>();
    private readonly Dictionary<string, TotemShopStockItemRuntime> _shopItemsById =
        new Dictionary<string, TotemShopStockItemRuntime>(StringComparer.Ordinal);

    private RectTransform _sellPopupRoot;
    private Text _sellValueText;
    private Button _sellButton;
    private RectTransform _buyPopupRoot;
    private Text _buyValueText;
    private Button _buyButton;
    private Text _toastText;
    private float _toastHideTime;
    private DraggableItemUI _selectedStorageItem;
    private string _selectedStockItemId;
    private int _currentPageIndex;
    private float _nextHeaderRefreshTime;
    private float _raidHudCleanupUntilTime;
    private int _popupOpenedFrame = -1;
    private bool _isSwitchingPage;
    private bool _hasOpened;

    private void Awake()
    {
        ScheduleRaidHudCleanup();
        RemoveRaidOnlyHud();
        ResolveReferences();
        ForceStoragePanelActive();
        ConfigureServices();
        ConfigureButtons();
        HidePopups();
    }

    private void Start()
    {
        if (OpenOnStart)
        {
            OpenShop();
        }

        ScheduleRaidHudCleanup();
        RemoveRaidOnlyHud();
    }

    private void Update()
    {
        if (!_hasOpened)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (_raidHudCleanupUntilTime > 0f)
        {
            if (now <= _raidHudCleanupUntilTime)
            {
                RemoveRaidOnlyHud();
            }
            else
            {
                _raidHudCleanupUntilTime = 0f;
            }
        }

        if (now >= _nextHeaderRefreshTime)
        {
            _nextHeaderRefreshTime = now + Mathf.Max(0.05f, HeaderRefreshInterval);
            SaveCurrentPageFromGrid();
            if (StorageService != null && StorageService.EnsureTrailingBlankPage())
            {
                RebuildPageButtons();
            }

            RefreshHeaders(false);
            RefreshShopIfExpired();
            AttachStorageClickTargets();
            AttachShopClickTargetsAndPrices();
        }

        if (_toastText != null && _toastText.gameObject.activeSelf && now >= _toastHideTime)
        {
            _toastText.gameObject.SetActive(false);
        }

        HandlePopupOutsideClick();
    }

    private void OnDisable()
    {
        SaveAll();
    }

    public void OpenShop()
    {
        ScheduleRaidHudCleanup();
        RemoveRaidOnlyHud();
        ResolveReferences();
        ForceStoragePanelActive();
        ConfigureServices();
        ConfigureButtons();

        if (StorageGrid == null || ShopGrid == null || StorageService == null || EconomyService == null || StockService == null)
        {
            Debug.LogWarning("[ShopScreenController] Cannot open shop because required references are missing.", this);
            return;
        }

        string agentId = ResolveAgentId();
        StorageService.LoadForAgent(agentId);
        StorageService.EnsurePageCount(StorageMinimumPageCount);
        StorageService.EnsureTrailingBlankPage();

        EconomyService.LoadForAgent(agentId);
        StockService.LoadForAgent(agentId);
        StockService.EnsureStockCurrent(BuildRuntimeShopPool());

        _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, StorageService.PageCount - 1));
        LoadCurrentPageIntoGrid();
        RebuildShopGrid();
        RebuildPageButtons();
        RefreshHeaders(true);
        HidePopups();
        _hasOpened = true;
    }

    public void ReturnToPreparationScene()
    {
        string sceneName = string.IsNullOrWhiteSpace(ReturnSceneName)
            ? DefaultReturnSceneName
            : ReturnSceneName;
        SceneManager.LoadScene(sceneName);
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
        RefreshHeaders(true);
        HidePopups();
        _isSwitchingPage = false;
    }

    public void SortCurrentStoragePage()
    {
        if (StorageGrid == null)
        {
            return;
        }

        StorageGrid.AutoSort();
        SaveCurrentPageFromGrid();
        StorageService?.EnsureTrailingBlankPage();
        StorageService?.Save();
        RebuildPageButtons();
        RefreshHeaders(true);
        AttachStorageClickTargets();
    }

    public void HandleStorageItemClicked(DraggableItemUI itemView, PointerEventData eventData)
    {
        if (itemView == null || itemView.ItemData == null || itemView.CurrentGrid != StorageGrid)
        {
            return;
        }

        _selectedStorageItem = itemView;
        _selectedStockItemId = null;
        EnsurePopups();
        int value = ShopEconomyPriceUtility.ResolveSellPrice(itemView.ItemData, this);
        _sellValueText.text = value.ToString("N0");
        _sellButton.interactable = value > 0;
        ShowPopup(_sellPopupRoot, eventData);
        HidePopup(_buyPopupRoot);
    }

    public void HandleShopItemClicked(string stockItemId, DraggableItemUI itemView, PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(stockItemId) || !_shopItemsById.TryGetValue(stockItemId, out TotemShopStockItemRuntime stockItem))
        {
            return;
        }

        if (stockItem == null || stockItem.IsSold || stockItem.ItemData == null)
        {
            return;
        }

        _selectedStorageItem = null;
        _selectedStockItemId = stockItemId;
        EnsurePopups();
        _buyValueText.text = stockItem.BuyPrice.ToString("N0");
        RefreshBuyButtonState(stockItem);
        ShowPopup(_buyPopupRoot, eventData);
        HidePopup(_sellPopupRoot);
    }

    private void ExecuteSell()
    {
        DraggableItemUI itemView = _selectedStorageItem;
        if (itemView == null || itemView.ItemData == null || itemView.CurrentGrid != StorageGrid)
        {
            HidePopups();
            return;
        }

        int value = ShopEconomyPriceUtility.ResolveSellPrice(itemView.ItemData, this);
        if (value <= 0)
        {
            HidePopups();
            return;
        }

        StorageGrid.GetGridController().RemoveItem(
            itemView,
            itemView._originalGridIndex.x,
            itemView._originalGridIndex.y,
            itemView._originalIsRotated);
        Destroy(itemView.gameObject);

        SaveCurrentPageFromGrid();
        StorageService?.Save();
        EconomyService.AddGold(value);
        EconomyService.Save();
        HidePopups();
        RefreshHeaders(true);
    }

    private void ExecuteBuy()
    {
        if (string.IsNullOrWhiteSpace(_selectedStockItemId) ||
            !_shopItemsById.TryGetValue(_selectedStockItemId, out TotemShopStockItemRuntime stockItem) ||
            stockItem == null ||
            stockItem.IsSold ||
            stockItem.ItemData == null)
        {
            HidePopups();
            return;
        }

        if (!EconomyService.CanSpend(stockItem.BuyPrice))
        {
            RefreshBuyButtonState(stockItem);
            return;
        }

        if (!EconomyService.TrySpend(stockItem.BuyPrice))
        {
            ShowToast("金币不足");
            RefreshBuyButtonState(stockItem);
            return;
        }

        SaveCurrentPageFromGrid();
        if (!TryAddPurchasedItemToStorage(stockItem.ItemData))
        {
            EconomyService.AddGold(stockItem.BuyPrice);
            ShowToast("仓库已满");
            return;
        }

        StockService.MarkSold(stockItem.StockItemId);
        EconomyService.Save();
        StockService.Save();
        StorageService.Save();
        RebuildShopGrid();
        RebuildPageButtons();
        HidePopups();
        RefreshHeaders(true);
    }

    private void LoadCurrentPageIntoGrid()
    {
        if (StorageGrid == null || StorageService == null)
        {
            return;
        }

        ForceStoragePanelActive();
        StorageGrid.RebuildGridUI(StorageColumns, StorageRows, new List<Vector2Int>());
        StorageGrid.LoadFromRuntimeState(
            StorageService.GetPageItems(_currentPageIndex),
            StorageService.GetPageCellStates(_currentPageIndex));
        AttachStorageClickTargets();
    }

    private void RebuildShopGrid()
    {
        if (ShopGrid == null || StockService == null)
        {
            return;
        }

        ConfigureShopPanelPresentation();
        _shopItemsById.Clear();
        List<ContainerItemSaveData> shopItems = new List<ContainerItemSaveData>();
        List<TotemShopStockItemRuntime> runtimeStock = StockService.GetRuntimeStockItems();
        foreach (TotemShopStockItemRuntime stockItem in runtimeStock)
        {
            if (stockItem == null || string.IsNullOrWhiteSpace(stockItem.StockItemId))
            {
                continue;
            }

            _shopItemsById[stockItem.StockItemId] = stockItem;
            if (stockItem.IsSold || stockItem.ItemData == null)
            {
                continue;
            }

            shopItems.Add(new ContainerItemSaveData
            {
                RuntimeItemId = stockItem.StockItemId,
                ItemData = stockItem.ItemData,
                Amount = 1,
                X = stockItem.X,
                Y = stockItem.Y,
                IsRotated = stockItem.IsRotated,
                RequiresSearch = false,
                IsSearched = true,
                SearchProgressSeconds = 0f,
                SearchDurationSeconds = 0f,
                InternalItems = new List<ContainerItemSaveData>(),
                InternalCellStates = new List<ContainerCellStateSaveData>()
            });
        }

        ShopGrid.RebuildGridUI(ShopColumns, ShopRows, new List<Vector2Int>());
        ShopGrid.LoadFromRuntimeState(shopItems, new List<ContainerCellStateSaveData>());
        AttachShopClickTargetsAndPrices();
    }

    private void SaveCurrentPageFromGrid()
    {
        if (!_hasOpened || StorageService == null || StorageGrid == null)
        {
            return;
        }

        StorageService.SetPageRuntimeState(
            _currentPageIndex,
            StorageGrid.ExtractSaveData(),
            StorageGrid.ExtractCellStateData());
    }

    private void SaveAll()
    {
        if (!_hasOpened)
        {
            return;
        }

        SaveCurrentPageFromGrid();
        StorageService?.Save();
        EconomyService?.Save();
        StockService?.Save();
    }

    private bool TryAddPurchasedItemToStorage(InventoryItemData itemData)
    {
        if (itemData == null || StorageService == null)
        {
            return false;
        }

        if (!CanFitInStoragePage(itemData))
        {
            return false;
        }

        StorageService.EnsureTrailingBlankPage();
        int startingPage = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, StorageService.PageCount - 1));
        for (int pageIndex = startingPage; pageIndex < StorageService.PageCount; pageIndex++)
        {
            if (TryAddItemToStoragePage(pageIndex, itemData))
            {
                if (pageIndex == _currentPageIndex)
                {
                    LoadCurrentPageIntoGrid();
                }

                return true;
            }
        }

        int newPageIndex = Mathf.Max(0, StorageService.PageCount);
        StorageService.EnsurePageCount(newPageIndex + 1);
        if (TryAddItemToStoragePage(newPageIndex, itemData))
        {
            return true;
        }

        return false;
    }

    private bool TryAddItemToStoragePage(int pageIndex, InventoryItemData itemData)
    {
        List<ContainerItemSaveData> items = StorageService.GetPageItems(pageIndex);
        List<ContainerCellStateSaveData> cellStates = StorageService.GetPageCellStates(pageIndex);

        InventoryGridModel model = new InventoryGridModel();
        model.Configure(StorageColumns, StorageRows, new List<Vector2Int>(), true);

        foreach (ContainerItemSaveData existingItem in items)
        {
            if (existingItem?.ItemData == null)
            {
                continue;
            }

            int width = existingItem.IsRotated ? existingItem.ItemData.Height : existingItem.ItemData.Width;
            int height = existingItem.IsRotated ? existingItem.ItemData.Width : existingItem.ItemData.Height;
            if (!IsFootprintInBounds(existingItem.X, existingItem.Y, width, height, StorageColumns, StorageRows))
            {
                continue;
            }

            model.PlaceItem(
                InventoryItemRuntimeState.Create(existingItem.ItemData, existingItem.Amount),
                existingItem.X,
                existingItem.Y,
                width,
                height,
                existingItem.IsRotated);
        }

        if (!model.FindFirstAvailableSpace(
                Mathf.Max(1, itemData.Width),
                Mathf.Max(1, itemData.Height),
                out Vector2Int position,
                out bool needsRotation))
        {
            return false;
        }

        items.Add(new ContainerItemSaveData
        {
            RuntimeItemId = Guid.NewGuid().ToString("N"),
            ItemData = itemData,
            Amount = 1,
            X = position.x,
            Y = position.y,
            IsRotated = needsRotation,
            RequiresSearch = false,
            IsSearched = true,
            SearchProgressSeconds = 0f,
            SearchDurationSeconds = 0f,
            InternalItems = new List<ContainerItemSaveData>(),
            InternalCellStates = new List<ContainerCellStateSaveData>()
        });

        StorageService.SetPageRuntimeState(pageIndex, items, cellStates);
        StorageService.EnsureTrailingBlankPage();
        return true;
    }

    private static bool CanFitInStoragePage(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        int width = Mathf.Max(1, itemData.Width);
        int height = Mathf.Max(1, itemData.Height);
        bool normalFits = width <= StorageColumns && height <= StorageRows;
        bool rotatedFits = height <= StorageColumns && width <= StorageRows;
        return normalFits || rotatedFits;
    }

    private static bool IsFootprintInBounds(int x, int y, int width, int height, int columns, int rows)
    {
        return x >= 0 && y >= 0 && x + width <= columns && y + height <= rows;
    }

    private void AttachStorageClickTargets()
    {
        foreach (DraggableItemUI itemView in EnumerateItemViews(StorageGrid))
        {
            ShopItemClickTarget clickTarget = itemView.GetComponent<ShopItemClickTarget>();
            if (clickTarget == null)
            {
                clickTarget = itemView.gameObject.AddComponent<ShopItemClickTarget>();
            }

            clickTarget.Configure(this, itemView, ShopItemClickMode.SellFromStorage);
            EnsureCurrencyPriceLabel(
                itemView,
                "StorageSellPriceLabel",
                ShopEconomyPriceUtility.ResolveSellPrice(itemView.ItemData, this),
                true);
            ApplyShopScreenItemNameLabel(itemView);
        }
    }

    private void AttachShopClickTargetsAndPrices()
    {
        foreach (DraggableItemUI itemView in EnumerateItemViews(ShopGrid))
        {
            if (itemView == null || !_shopItemsById.TryGetValue(itemView.RuntimeItemId, out TotemShopStockItemRuntime stockItem))
            {
                continue;
            }

            EnsureCurrencyPriceLabel(itemView, "ShopPriceLabel", stockItem.BuyPrice, false);
            ApplyShopScreenItemNameLabel(itemView);
            EnsureShopClickOverlay(itemView, stockItem.StockItemId);
        }
    }

    private void EnsureCurrencyPriceLabel(DraggableItemUI itemView, string labelName, int price, bool pinToTopLeft)
    {
        if (itemView == null || string.IsNullOrWhiteSpace(labelName))
        {
            return;
        }

        Transform existing = itemView.transform.Find(labelName);
        RectTransform labelRect;
        if (existing == null)
        {
            GameObject labelObject = new GameObject(labelName, typeof(RectTransform));
            labelObject.transform.SetParent(itemView.transform, false);
            labelRect = labelObject.GetComponent<RectTransform>();
        }
        else
        {
            labelRect = existing.GetComponent<RectTransform>();
        }

        if (labelRect == null)
        {
            return;
        }

        if (pinToTopLeft)
        {
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(4f, -4f - ItemCurrencyLabelHeight);
            labelRect.offsetMax = new Vector2(-4f, -4f);
        }
        else
        {
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.offsetMin = new Vector2(4f, ShopScreenItemNameHeight + 5f);
            labelRect.offsetMax = new Vector2(-4f, ShopScreenItemNameHeight + 5f + ItemCurrencyLabelHeight);
        }

        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        Image icon = EnsureChildImage(labelRect, "CurrencyIcon");
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 0f);
        iconRect.sizeDelta = new Vector2(ItemCurrencyIconSize, ItemCurrencyIconSize);
        iconRect.localScale = Vector3.one;
        iconRect.localRotation = Quaternion.identity;
        ApplyCurrencyIcon(icon);

        Text text = EnsureChildText(labelRect, "CurrencyAmountText");
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(ItemCurrencyIconSize + 6f, 0f);
        textRect.offsetMax = Vector2.zero;
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;

        text.font = ResolveFont();
        text.fontSize = ItemCurrencyAmountMaxFontSize;
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = ItemCurrencyAmountMinFontSize;
        text.resizeTextMaxSize = ItemCurrencyAmountMaxFontSize;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = price.ToString("N0");

        Shadow shadow = text.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = text.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        labelRect.SetAsLastSibling();
    }

    private void ApplyShopScreenItemNameLabel(DraggableItemUI itemView)
    {
        if (itemView == null)
        {
            return;
        }

        itemView.UpdateItemNameText();
        Text text = itemView.ItemNameText;
        if (text == null)
        {
            return;
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.offsetMin = new Vector2(5f, 3f);
        rect.offsetMax = new Vector2(-5f, 3f + ShopScreenItemNameHeight);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        text.font = ResolveFont();
        text.fontSize = ShopScreenItemNameMaxFontSize;
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = ShopScreenItemNameMinFontSize;
        text.resizeTextMaxSize = ShopScreenItemNameMaxFontSize;
        text.alignment = TextAnchor.LowerRight;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = Color.white;
        text.raycastTarget = false;

        Shadow shadow = text.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = text.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;
        text.transform.SetAsLastSibling();
    }

    private void EnsureShopClickOverlay(DraggableItemUI itemView, string stockItemId)
    {
        const string overlayName = "ShopClickOverlay";
        Transform existing = itemView.transform.Find(overlayName);
        GameObject overlayObject;
        if (existing == null)
        {
            overlayObject = new GameObject(overlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ShopItemClickTarget));
            overlayObject.transform.SetParent(itemView.transform, false);
        }
        else
        {
            overlayObject = existing.gameObject;
        }

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = overlayObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        ShopItemClickTarget clickTarget = overlayObject.GetComponent<ShopItemClickTarget>();
        clickTarget.Configure(this, itemView, ShopItemClickMode.BuyFromShop, stockItemId);
        overlayObject.transform.SetAsLastSibling();
    }

    private void RebuildPageButtons()
    {
        if (PageButtonContainer == null || PageButtonTemplate == null || StorageService == null)
        {
            return;
        }

        for (int i = _pageButtons.Count - 1; i >= 0; i--)
        {
            if (_pageButtons[i] != null)
            {
                Destroy(_pageButtons[i].gameObject);
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
    }

    private void RefreshHeaders(bool force)
    {
        if (StorageService != null && StorageHeaderText != null)
        {
            int occupied = StorageGrid != null
                ? CountOccupiedCells(StorageGrid.ExtractSaveData())
                : StorageService.CountOccupiedCells(_currentPageIndex);
            StorageHeaderText.text = $"{StorageTitle} {_currentPageIndex + 1}/{Mathf.Max(1, StorageService.PageCount)} ({occupied}/{StorageService.PageCapacity})";
        }

        if (GoldText != null && EconomyService != null)
        {
            EnsureGoldCurrencyIcon();
            GoldText.text = EconomyService.Gold.ToString("N0");
        }

        if (ShopHeaderText != null)
        {
            ShopHeaderText.text = ShopTitle;
        }

        if (RefreshCountdownText != null && StockService != null)
        {
            RefreshCountdownText.text = $"刷新：{FormatCountdown(StockService.GetSecondsUntilRefresh())}";
        }

        if (!force && !string.IsNullOrWhiteSpace(_selectedStockItemId) &&
            _shopItemsById.TryGetValue(_selectedStockItemId, out TotemShopStockItemRuntime stockItem))
        {
            RefreshBuyButtonState(stockItem);
        }
    }

    private void RefreshShopIfExpired()
    {
        if (StockService == null || StockService.GetSecondsUntilRefresh() > 0f)
        {
            return;
        }

        if (StockService.EnsureStockCurrent(BuildRuntimeShopPool()))
        {
            RebuildShopGrid();
            RefreshHeaders(true);
        }
    }

    private List<TotemShopPoolEntry> BuildRuntimeShopPool()
    {
        if (ShopPool != null)
        {
            return ShopPool.BuildRuntimeEntries(ItemDatabase);
        }

        InventoryItemDatabase database = ItemDatabase != null
            ? ItemDatabase
            : Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase");
        List<TotemShopPoolEntry> entries = new List<TotemShopPoolEntry>();
        if (database == null || database.Items == null)
        {
            return entries;
        }

        foreach (InventoryItemData item in database.Items)
        {
            if (item != null && item.EquipmentKind == EquipmentSlotKind.Totem)
            {
                entries.Add(new TotemShopPoolEntry
                {
                    ItemData = item,
                    BuyPrice = 0,
                    Weight = 1f
                });
            }
        }

        return entries;
    }

    private void RefreshBuyButtonState(TotemShopStockItemRuntime stockItem)
    {
        if (_buyButton == null || stockItem == null || EconomyService == null)
        {
            return;
        }

        _buyButton.interactable = !stockItem.IsSold && EconomyService.CanSpend(stockItem.BuyPrice);
    }

    private void ConfigureServices()
    {
        if (StorageService != null)
        {
            StorageService.FallbackAgentId = FallbackAgentId;
            StorageService.Configure(StorageColumns, StorageRows, StorageMinimumPageCount);
        }

        if (EconomyService != null)
        {
            EconomyService.FallbackAgentId = FallbackAgentId;
            EconomyService.DefaultGold = 1000;
        }

        if (StockService != null)
        {
            StockService.FallbackAgentId = FallbackAgentId;
            StockService.Configure(ShopColumns, ShopRows, RefreshMinutes);
        }

        if (ShopGrid != null)
        {
            ConfigureStoragePanelPresentation();
            ConfigureShopPanelPresentation();
            InventoryGridInteractionPolicy interactionPolicy = ShopGrid.GetComponent<InventoryGridInteractionPolicy>();
            if (interactionPolicy == null)
            {
                interactionPolicy = ShopGrid.gameObject.AddComponent<InventoryGridInteractionPolicy>();
            }

            interactionPolicy.AllowItemDragStart = false;
            interactionPolicy.AllowItemDrops = false;

            if (ShopGrid.GetComponent<ShopGridDropBlocker>() == null)
            {
                ShopGrid.gameObject.AddComponent<ShopGridDropBlocker>();
            }
        }
    }

    private void ConfigureStoragePanelPresentation()
    {
        RectTransform panelRect = StoragePanel != null
            ? StoragePanel.GetComponent<RectTransform>()
            : null;
        if (panelRect == null)
        {
            return;
        }

        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(StoragePanelLeftInset, 0f);
        panelRect.localScale = Vector3.one;
        panelRect.localRotation = Quaternion.identity;
    }

    private void ConfigureShopPanelPresentation()
    {
        if (ShopGrid == null)
        {
            return;
        }

        ShopGrid.CellSize = ShopCellSize;
        ShopGrid.Spacing = ShopCellSpacing;

        Vector2 gridSize = CalculateShopGridSize();
        RectTransform gridRect = ShopGrid.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.sizeDelta = gridSize;
        }

        RectTransform gridContainer = ShopGrid.transform.parent as RectTransform;
        if (gridContainer != null)
        {
            gridContainer.anchorMin = new Vector2(0.5f, 1f);
            gridContainer.anchorMax = new Vector2(0.5f, 1f);
            gridContainer.pivot = new Vector2(0.5f, 1f);
            gridContainer.anchoredPosition = new Vector2(0f, -ShopGridTopInset);
            gridContainer.sizeDelta = gridSize;
            gridContainer.localScale = Vector3.one;
            gridContainer.localRotation = Quaternion.identity;
        }

        RectTransform panelRect = FindAncestorRect(ShopGrid.transform, "ShopPanel");
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-ShopPanelRightInset, 0f);
            panelRect.sizeDelta = new Vector2(ShopPanelWidth, ShopPanelHeight);
            panelRect.localScale = Vector3.one;
            panelRect.localRotation = Quaternion.identity;

            RectTransform headerRow = panelRect.Find("LootHeaderRow") as RectTransform;
            if (headerRow != null)
            {
                headerRow.anchorMin = new Vector2(0f, 1f);
                headerRow.anchorMax = new Vector2(1f, 1f);
                headerRow.pivot = new Vector2(0.5f, 1f);
                headerRow.anchoredPosition = new Vector2(0f, -20f);
                headerRow.sizeDelta = new Vector2(-48f, 56f);
                headerRow.localScale = Vector3.one;
                headerRow.localRotation = Quaternion.identity;
            }
        }
    }

    private static Vector2 CalculateShopGridSize()
    {
        return new Vector2(
            ShopColumns * ShopCellSize + (ShopColumns - 1) * ShopCellSpacing,
            ShopRows * ShopCellSize + (ShopRows - 1) * ShopCellSpacing);
    }

    private void ConfigureButtons()
    {
        EnsureReturnButton();
        if (StorageSortButton != null)
        {
            StorageSortButton.onClick.RemoveListener(SortCurrentStoragePage);
            StorageSortButton.onClick.AddListener(SortCurrentStoragePage);
        }

        if (ReturnButton != null)
        {
            ReturnButton.onClick.RemoveListener(ReturnToPreparationScene);
            ReturnButton.onClick.AddListener(ReturnToPreparationScene);
        }
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

        if (EconomyService == null)
        {
            EconomyService = GetComponent<PlayerEconomyService>() ?? GetComponentInChildren<PlayerEconomyService>(true);
        }

        if (StockService == null)
        {
            StockService = GetComponent<TotemShopStockService>() ?? GetComponentInChildren<TotemShopStockService>(true);
        }

        if (StorageGrid == null)
        {
            StorageGrid = FindGridByName("StorageGrid") ?? FindGridByName("LootChestGrid");
        }

        if (StoragePanel == null)
        {
            StoragePanel = FindGameObjectByName("StoragePanel");
        }

        if (StoragePanel == null && StorageGrid != null)
        {
            StoragePanel = StorageGrid.transform.parent != null
                ? StorageGrid.transform.parent.gameObject
                : StorageGrid.gameObject;
        }

        if (ShopGrid == null)
        {
            ShopGrid = FindGridByName("ShopGrid");
        }

        if (StorageHeaderText == null)
        {
            StorageHeaderText = FindTextByName("StorageHeaderText");
        }

        if (GoldText == null)
        {
            GoldText = FindTextByName("ShopGoldText");
        }

        if (ShopHeaderText == null)
        {
            ShopHeaderText = FindTextByName("ShopHeaderText");
        }

        if (RefreshCountdownText == null)
        {
            RefreshCountdownText = FindTextByName("ShopRefreshCountdownText");
        }

        if (StorageSortButton == null)
        {
            StorageSortButton = FindButtonByName("StorageSortButton");
        }

        if (ReturnButton == null)
        {
            ReturnButton = FindButtonByName("ShopReturnButton");
        }
    }

    private void ForceStoragePanelActive()
    {
        if (StoragePanel == null)
        {
            StoragePanel = FindGameObjectByName("StoragePanel");
        }

        Transform root = StoragePanel != null
            ? StoragePanel.transform
            : StorageGrid != null
                ? StorageGrid.transform
                : null;
        if (root == null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        Transform stopAt = canvas != null ? canvas.transform : transform;
        Transform current = root;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            if (current == stopAt)
            {
                break;
            }

            current = current.parent;
        }

        if (StorageGrid != null)
        {
            if (!StorageGrid.gameObject.activeSelf)
            {
                StorageGrid.gameObject.SetActive(true);
            }

            StorageGrid.enabled = true;
            SetActiveIfNotNull(StorageGrid.GridBackground);
            SetActiveIfNotNull(StorageGrid.ItemContainer);
            StorageGrid.HideHighlight();
        }
    }

    private void ScheduleRaidHudCleanup()
    {
        _raidHudCleanupUntilTime = Mathf.Max(_raidHudCleanupUntilTime, Time.unscaledTime + RaidHudCleanupDurationSeconds);
    }

    private static void RemoveRaidOnlyHud()
    {
        RaidMinimapController[] minimaps = FindObjectsOfType<RaidMinimapController>(true);
        for (int i = 0; i < minimaps.Length; i++)
        {
            RaidMinimapController minimap = minimaps[i];
            if (minimap != null)
            {
                UnityEngine.Object target = minimap.name == "RaidMinimapController" && minimap.transform.parent == null
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

        PlayerStatusHudController[] statusHuds = FindObjectsOfType<PlayerStatusHudController>(true);
        for (int i = 0; i < statusHuds.Length; i++)
        {
            PlayerStatusHudController statusHud = statusHuds[i];
            if (statusHud != null)
            {
                DestroyUnityObject(statusHud.gameObject);
            }
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
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

    private string ResolveAgentId()
    {
        if (InventoryScreen != null && !string.IsNullOrWhiteSpace(InventoryScreen.ActiveInventoryAgentId))
        {
            return InventoryScreen.ActiveInventoryAgentId;
        }

        return string.IsNullOrWhiteSpace(FallbackAgentId) ? "default_player" : FallbackAgentId.Trim();
    }

    private void EnsurePopups()
    {
        if (_sellPopupRoot == null)
        {
            _sellPopupRoot = CreateActionPopup("SellPopup", "出售", out _sellValueText, out _sellButton);
            _sellButton.onClick.AddListener(ExecuteSell);
        }

        if (_buyPopupRoot == null)
        {
            _buyPopupRoot = CreateActionPopup("BuyPopup", "购买", out _buyValueText, out _buyButton);
            _buyButton.onClick.AddListener(ExecuteBuy);
        }
    }

    private RectTransform CreateActionPopup(string popupName, string buttonLabel, out Text valueText, out Button actionButton)
    {
        Transform parent = ResolveCanvasParent();
        GameObject popupObject = new GameObject(popupName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupObject.transform.SetParent(parent, false);
        RectTransform popupRect = popupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0f, 1f);
        popupRect.sizeDelta = new Vector2(180f, 92f);
        popupRect.localScale = Vector3.one;
        popupRect.localRotation = Quaternion.identity;

        Image background = popupObject.GetComponent<Image>();
        background.color = new Color(0.03f, 0.06f, 0.08f, 0.95f);

        Image valueIcon = CreateImage("CurrencyIcon", popupRect);
        RectTransform iconRect = valueIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(40f, -28f);
        iconRect.sizeDelta = new Vector2(PopupCurrencyIconSize, PopupCurrencyIconSize);
        iconRect.localScale = Vector3.one;
        iconRect.localRotation = Quaternion.identity;
        ApplyCurrencyIcon(valueIcon);

        valueText = CreateText("ValueText", popupRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(80f, -46f), new Vector2(-10f, -10f), 16, TextAnchor.MiddleLeft);
        valueText.fontStyle = FontStyle.Bold;
        valueText.resizeTextForBestFit = true;
        valueText.resizeTextMinSize = 12;
        valueText.resizeTextMaxSize = 16;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        Shadow valueShadow = valueText.gameObject.AddComponent<Shadow>();
        valueShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        valueShadow.effectDistance = new Vector2(1f, -1f);
        actionButton = CreateButton("ActionButton", popupRect, buttonLabel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(112f, 32f));
        popupObject.SetActive(false);
        return popupRect;
    }

    private void ShowPopup(RectTransform popup, PointerEventData eventData)
    {
        if (popup == null)
        {
            return;
        }

        PositionPopup(popup, eventData);
        popup.gameObject.SetActive(true);
        _popupOpenedFrame = Time.frameCount;
        popup.SetAsLastSibling();
    }

    private void HidePopup(RectTransform popup)
    {
        if (popup != null)
        {
            popup.gameObject.SetActive(false);
        }
    }

    private void HidePopups()
    {
        HidePopup(_sellPopupRoot);
        HidePopup(_buyPopupRoot);
        _selectedStorageItem = null;
        _selectedStockItemId = null;
    }

    private void HandlePopupOutsideClick()
    {
        if (!Input.GetMouseButtonDown(0) ||
            Time.frameCount == _popupOpenedFrame ||
            !HasVisiblePopup())
        {
            return;
        }

        if (IsPointerInsidePopup(_sellPopupRoot) ||
            IsPointerInsidePopup(_buyPopupRoot))
        {
            return;
        }

        HidePopups();
    }

    private bool HasVisiblePopup()
    {
        return (_sellPopupRoot != null && _sellPopupRoot.gameObject.activeSelf) ||
               (_buyPopupRoot != null && _buyPopupRoot.gameObject.activeSelf);
    }

    private bool IsPointerInsidePopup(RectTransform popup)
    {
        if (popup == null || !popup.gameObject.activeSelf)
        {
            return false;
        }

        Camera eventCamera = ResolveEventCamera();
        return RectTransformUtility.RectangleContainsScreenPoint(
            popup,
            Input.mousePosition,
            eventCamera);
    }

    private Camera ResolveEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void PositionPopup(RectTransform popup, PointerEventData eventData)
    {
        RectTransform canvasRect = ResolveCanvasParent() as RectTransform;
        if (canvasRect == null || eventData == null)
        {
            popup.anchoredPosition = Vector2.zero;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        Vector2 offset = new Vector2(18f, -18f);
        Vector2 position = localPoint + offset;
        Rect rect = canvasRect.rect;
        position.x = Mathf.Clamp(position.x, rect.xMin + 8f, rect.xMax - popup.sizeDelta.x - 8f);
        position.y = Mathf.Clamp(position.y, rect.yMin + popup.sizeDelta.y + 8f, rect.yMax - 8f);
        popup.anchoredPosition = position;
    }

    private void ShowToast(string message)
    {
        if (_toastText == null)
        {
            Transform parent = ResolveCanvasParent();
            GameObject toastObject = new GameObject("ShopToastText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            toastObject.transform.SetParent(parent, false);
            _toastText = toastObject.GetComponent<Text>();
            RectTransform rect = _toastText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 46f);
            rect.sizeDelta = new Vector2(360f, 34f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            _toastText.font = ResolveFont();
            _toastText.fontSize = 18;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastText.raycastTarget = false;
            Shadow shadow = toastObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        _toastText.text = message;
        _toastText.gameObject.SetActive(true);
        _toastText.transform.SetAsLastSibling();
        _toastHideTime = Time.unscaledTime + 2f;
    }

    private Transform ResolveCanvasParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private void EnsureGoldCurrencyIcon()
    {
        if (GoldText == null || GoldText.transform.parent == null)
        {
            return;
        }

        RectTransform textRect = GoldText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0f, 0f);
        textRect.anchoredPosition = new Vector2(-10f, 12f);
        textRect.sizeDelta = new Vector2(190f, 36f);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;

        GoldText.font = ResolveFont();
        GoldText.fontSize = 20;
        GoldText.fontStyle = FontStyle.Bold;
        GoldText.alignment = TextAnchor.MiddleLeft;
        GoldText.resizeTextForBestFit = true;
        GoldText.resizeTextMinSize = 13;
        GoldText.resizeTextMaxSize = 20;
        GoldText.color = Color.white;
        GoldText.raycastTarget = false;

        Image icon = EnsureChildImage(GoldText.transform.parent, "ShopGoldCurrencyIcon");
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-18f, 30f);
        iconRect.sizeDelta = new Vector2(HeaderCurrencyIconSize, HeaderCurrencyIconSize);
        iconRect.localScale = Vector3.one;
        iconRect.localRotation = Quaternion.identity;
        ApplyCurrencyIcon(icon);
    }

    private void EnsureReturnButton()
    {
        if (ReturnButton == null)
        {
            ReturnButton = FindButtonByName("ShopReturnButton");
        }

        if (ReturnButton == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;
            GameObject buttonObject = new GameObject(
                "ShopReturnButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            ReturnButton = buttonObject.GetComponent<Button>();
        }

        RectTransform rect = ReturnButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(ReturnButtonWidth, ReturnButtonHeight);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        Image image = ReturnButton.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = ResolveReturnButtonSprite();
            image.enabled = image.sprite != null;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
            ReturnButton.targetGraphic = image;
        }

        ColorBlock colors = ReturnButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.82f, 0.9f, 1f, 0.92f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        colors.colorMultiplier = 1f;
        ReturnButton.colors = colors;

        Text label = EnsureChildText(ReturnButton.transform, "Label");
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        label.font = ResolveFont();
        label.fontSize = 26;
        label.fontStyle = FontStyle.Bold;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 16;
        label.resizeTextMaxSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "\u8FD4\u56DE";

        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = label.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;

        ReturnButton.gameObject.SetActive(true);
        ReturnButton.transform.SetAsLastSibling();
    }

    private Sprite ResolveReturnButtonSprite()
    {
        if (ReturnButtonSprite != null)
        {
            return ReturnButtonSprite;
        }

#if UNITY_EDITOR
        ReturnButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ReturnButtonAssetPath);
#endif
        return ReturnButtonSprite;
    }

    private Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Image EnsureChildImage(Transform parent, string name)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            image = imageObject.GetComponent<Image>();
        }

        image.raycastTarget = false;
        return image;
    }

    private Text EnsureChildText(Transform parent, string name)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<Text>();
        }

        text.raycastTarget = false;
        return text;
    }

    private void ApplyCurrencyIcon(Image image)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = ResolveCurrencyIcon();
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private Sprite ResolveCurrencyIcon()
    {
        if (CurrencyIcon != null)
        {
            return CurrencyIcon;
        }

#if UNITY_EDITOR
        CurrencyIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(CurrencyIconAssetPath);
#endif
        return CurrencyIcon;
    }

    private Text CreateText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.font = ResolveFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.55f, 0.74f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.55f, 0.74f, 1f);
        colors.highlightedColor = new Color(0.25f, 0.68f, 0.9f, 1f);
        colors.pressedColor = new Color(0.1f, 0.38f, 0.58f, 1f);
        colors.disabledColor = new Color(0.18f, 0.22f, 0.26f, 0.7f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 15, TextAnchor.MiddleCenter);
        text.text = label;
        return button;
    }

    private Font ResolveFont()
    {
        Text existingText = GetComponentInChildren<Text>(true);
        return existingText != null && existingText.font != null
            ? existingText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private InventoryUIController FindGridByName(string objectName)
    {
        InventoryUIController[] grids = GetComponentsInChildren<InventoryUIController>(true);
        foreach (InventoryUIController grid in grids)
        {
            if (grid != null && string.Equals(grid.name, objectName, StringComparison.Ordinal))
            {
                return grid;
            }
        }

        return null;
    }

    private Text FindTextByName(string objectName)
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text != null && string.Equals(text.name, objectName, StringComparison.Ordinal))
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
            if (button != null && string.Equals(button.name, objectName, StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private static RectTransform FindAncestorRect(Transform start, string objectName)
    {
        Transform current = start;
        while (current != null)
        {
            if (string.Equals(current.name, objectName, StringComparison.Ordinal))
            {
                return current as RectTransform;
            }

            current = current.parent;
        }

        return null;
    }

    private GameObject FindGameObjectByName(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static void SetActiveIfNotNull(Component component)
    {
        if (component != null && !component.gameObject.activeSelf)
        {
            component.gameObject.SetActive(true);
        }
    }

    private static IEnumerable<DraggableItemUI> EnumerateItemViews(InventoryUIController grid)
    {
        if (grid == null || grid.ItemContainer == null)
        {
            yield break;
        }

        foreach (Transform child in grid.ItemContainer)
        {
            if (grid.Highlighter != null && child == grid.Highlighter)
            {
                continue;
            }

            DraggableItemUI itemView = child.GetComponent<DraggableItemUI>();
            if (itemView != null && itemView.ItemData != null)
            {
                yield return itemView;
            }
        }
    }

    private static int CountOccupiedCells(List<ContainerItemSaveData> items)
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

    private static string FormatCountdown(float seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        if (span.TotalHours >= 1d)
        {
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        return $"{span.Minutes:00}:{span.Seconds:00}";
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
            image.color = normal;
            image.raycastTarget = true;
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
}
