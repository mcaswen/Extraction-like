using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ShopCanvasPrefabBuilder
{
    private const string SourceCanvasPrefabPath = "Assets/Prefabs/Canvas.prefab";
    private const string SourceStoragePrefabPath = "Assets/Prefabs/StorageCanvas.prefab";
    private const string ShopCanvasPrefabPath = "Assets/Prefabs/ShopCanvas.prefab";
    private const string ShopTestScenePath = "Assets/Scenes/ShopCanvasTest.unity";
    private const string ItemDatabasePath = "Assets/Resources/Inventory/InventoryItemDatabase.asset";
    private const string ShopPoolPath = "Assets/Resources/Shop/TotemShopPool.asset";
    private const string CurrencyIconPath = "Assets/Art/Sprites/Png_Item_store/Png_Item_currency.PNG";
    private const string ReturnButtonPath = "Assets/Art/Sprites/UI/Button/IMG_0494.PNG";
    private const string ReturnSceneName = "Scene_PreparationInterface";
    private const int StorageColumns = 6;
    private const int StorageRows = 10;
    private const int StorageMinimumPageCount = 10;
    private const int ShopColumns = 4;
    private const int ShopRows = 4;
    private const int RefreshIntervalMinutes = 30;
    private const float ShopCellSize = 96f;
    private const float ShopCellSpacing = 6f;
    private const float ShopPanelWidth = 520f;
    private const float ShopPanelHeight = 610f;
    private const float StoragePanelLeftInset = 220f;
    private const float ShopPanelRightInset = 220f;
    private const float ShopGridTopInset = 120f;
    private const float HeaderCurrencyIconSize = 40f;
    private const float ReturnButtonWidth = 148f;
    private const float ReturnButtonHeight = 64f;

    [MenuItem("Tools/Backpack/Rebuild Totem Shop Canvas")]
    public static void RebuildShopCanvasAndScene()
    {
        InventoryItemDatabase database = EnsureInventoryItemDatabaseAsset();
        TotemShopPool shopPool = EnsureDefaultShopPoolAsset(database);
        CreateIndependentShopPrefab(database, shopPool);
        CreateShopTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasPrefabPath);
        Debug.Log("Shop canvas prefab and test scene rebuilt.");
    }

    public static void RebuildShopCanvasAndSceneFromCommandLine()
    {
        RebuildShopCanvasAndScene();
    }

    private static void CreateIndependentShopPrefab(InventoryItemDatabase database, TotemShopPool shopPool)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceStoragePrefabPath) == null)
        {
            Debug.LogError("Source StorageCanvas prefab is missing: " + SourceStoragePrefabPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceCanvasPrefabPath) == null)
        {
            Debug.LogError("Source Canvas prefab is missing: " + SourceCanvasPrefabPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(ShopCanvasPrefabPath);
        }

        if (!AssetDatabase.CopyAsset(SourceStoragePrefabPath, ShopCanvasPrefabPath))
        {
            Debug.LogError("Failed to copy StorageCanvas prefab to " + ShopCanvasPrefabPath);
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ShopCanvasPrefabPath);
        try
        {
            prefabRoot.name = "ShopCanvas";
            UnpackNestedPrefabInstances(prefabRoot);
            ConfigureShopPrefab(prefabRoot, database, shopPool);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ShopCanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureShopPrefab(GameObject prefabRoot, InventoryItemDatabase database, TotemShopPool shopPool)
    {
        InventoryScreenController inventoryScreen = prefabRoot.GetComponentInChildren<InventoryScreenController>(true);
        GameObject controllerObject = inventoryScreen != null ? inventoryScreen.gameObject : prefabRoot;

        StorageScreenController oldStorageController = prefabRoot.GetComponentInChildren<StorageScreenController>(true);
        if (oldStorageController != null)
        {
            Object.DestroyImmediate(oldStorageController);
        }

        if (inventoryScreen != null)
        {
            inventoryScreen.enabled = false;
        }

        PlayerStorageService storageService = GetOrAddComponent<PlayerStorageService>(controllerObject);
        PlayerEconomyService economyService = GetOrAddComponent<PlayerEconomyService>(controllerObject);
        TotemShopStockService stockService = GetOrAddComponent<TotemShopStockService>(controllerObject);
        ShopScreenController shopController = GetOrAddComponent<ShopScreenController>(controllerObject);
        Sprite currencyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CurrencyIconPath);
        Sprite returnButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ReturnButtonPath);

        storageService.Columns = StorageColumns;
        storageService.Rows = StorageRows;
        storageService.MinimumPageCount = StorageMinimumPageCount;
        storageService.FallbackAgentId = "default_player";

        economyService.DefaultGold = 1000;
        economyService.FallbackAgentId = "default_player";

        stockService.Columns = ShopColumns;
        stockService.Rows = ShopRows;
        stockService.RefreshIntervalMinutes = RefreshIntervalMinutes;
        stockService.FallbackAgentId = "default_player";

        GameObject storagePanel = FindChildGameObject(prefabRoot.transform, "StoragePanel");
        InventoryUIController storageGrid = inventoryScreen != null ? inventoryScreen.LootChestGrid : null;
        if (storageGrid == null)
        {
            storageGrid = FindChildComponentByName<InventoryUIController>(prefabRoot.transform, "StorageGrid") ??
                FindChildComponentByName<InventoryUIController>(prefabRoot.transform, "LootChestGrid");
        }

        if (storagePanel == null && inventoryScreen != null)
        {
            storagePanel = inventoryScreen.LootChestPanel;
        }

        if (storagePanel == null && storageGrid != null)
        {
            storagePanel = storageGrid.transform.parent != null ? storageGrid.transform.parent.gameObject : storageGrid.gameObject;
        }

        if (storagePanel == null || storageGrid == null)
        {
            Debug.LogError("Shop prefab source is missing the storage panel/grid.");
            return;
        }

        storagePanel.name = "StoragePanel";
        storagePanel.SetActive(true);
        storageGrid.name = "StorageGrid";
        storageGrid.RebuildGridUI(StorageColumns, StorageRows, new List<Vector2Int>());
        ConfigureStoragePanel(storagePanel, currencyIcon);

        DisableOriginalBackpackArea(inventoryScreen, storagePanel);

        GameObject shopPanel = CreateShopPanel(prefabRoot, storagePanel.transform.parent);
        InventoryUIController shopGrid = ConfigureShopPanel(shopPanel);

        shopController.InventoryScreen = inventoryScreen;
        shopController.StorageService = storageService;
        shopController.EconomyService = economyService;
        shopController.StockService = stockService;
        shopController.ItemDatabase = database;
        shopController.ShopPool = shopPool;
        shopController.CurrencyIcon = currencyIcon;
        shopController.ReturnButtonSprite = returnButtonSprite;
        shopController.StoragePanel = storagePanel;
        shopController.StorageGrid = storageGrid;
        shopController.ShopGrid = shopGrid;
        shopController.StorageHeaderText = FindChildComponentByName<Text>(storagePanel.transform, "StorageHeaderText");
        shopController.GoldText = FindChildComponentByName<Text>(storagePanel.transform, "ShopGoldText");
        shopController.ShopHeaderText = FindChildComponentByName<Text>(shopPanel.transform, "ShopHeaderText");
        shopController.RefreshCountdownText = FindChildComponentByName<Text>(shopPanel.transform, "ShopRefreshCountdownText");
        shopController.StorageSortButton = FindChildComponentByName<Button>(storagePanel.transform, "StorageSortButton");
        shopController.ReturnButton = EnsureReturnButton(prefabRoot.transform, returnButtonSprite);
        shopController.PageButtonContainer = FindChildRectTransform(storagePanel.transform, "Content");
        shopController.PageButtonTemplate = FindChildComponentByName<Button>(storagePanel.transform, "StoragePageButtonTemplate");
        shopController.OpenOnStart = true;
        shopController.FallbackAgentId = "default_player";
        shopController.ReturnSceneName = ReturnSceneName;

        RemoveRaidOnlyHud(prefabRoot);
    }

    private static void ConfigureStoragePanel(GameObject storagePanel, Sprite currencyIcon)
    {
        RectTransform panelRect = storagePanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0f, 0.5f);
            panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(StoragePanelLeftInset, 0f);
            panelRect.localScale = Vector3.one;
            panelRect.localRotation = Quaternion.identity;
        }

        Text headerText = FindChildComponentByName<Text>(storagePanel.transform, "StorageHeaderText");
        if (headerText != null)
        {
            headerText.text = "仓库 1/10 (0/60)";
        }

        EnsureGoldText(storagePanel.transform, currencyIcon);
    }

    private static Text EnsureGoldText(Transform storagePanel, Sprite currencyIcon)
    {
        Text goldText = FindChildComponentByName<Text>(storagePanel, "ShopGoldText");
        if (goldText == null)
        {
            GameObject goldObject = CreateUiObject("ShopGoldText", storagePanel);
            goldText = goldObject.AddComponent<Text>();
        }

        RectTransform rect = goldText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(-10f, 12f);
        rect.sizeDelta = new Vector2(190f, 36f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        goldText.font = ResolveFont(storagePanel);
        goldText.fontSize = 20;
        goldText.fontStyle = FontStyle.Bold;
        goldText.alignment = TextAnchor.MiddleLeft;
        goldText.color = Color.white;
        goldText.resizeTextForBestFit = true;
        goldText.resizeTextMinSize = 13;
        goldText.resizeTextMaxSize = 20;
        goldText.raycastTarget = false;
        goldText.text = "1,000";

        Image currencyImage = EnsureCurrencyIcon(storagePanel, currencyIcon);
        if (currencyImage != null)
        {
            RectTransform iconRect = currencyImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-18f, 30f);
            iconRect.sizeDelta = new Vector2(HeaderCurrencyIconSize, HeaderCurrencyIconSize);
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
        }

        Shadow shadow = goldText.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = goldText.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return goldText;
    }

    private static Image EnsureCurrencyIcon(Transform storagePanel, Sprite currencyIcon)
    {
        if (storagePanel == null)
        {
            return null;
        }

        Image image = FindChildComponentByName<Image>(storagePanel, "ShopGoldCurrencyIcon");
        if (image == null)
        {
            GameObject iconObject = CreateUiObject("ShopGoldCurrencyIcon", storagePanel);
            image = iconObject.AddComponent<Image>();
        }

        image.sprite = currencyIcon;
        image.enabled = currencyIcon != null;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateShopPanel(GameObject prefabRoot, Transform targetParent)
    {
        GameObject existing = FindChildGameObject(prefabRoot.transform, "ShopPanel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(SourceCanvasPrefabPath);
        try
        {
            InventoryScreenController sourceInventory = sourceRoot.GetComponentInChildren<InventoryScreenController>(true);
            GameObject sourceLootPanel = sourceInventory != null && sourceInventory.LootChestPanel != null
                ? sourceInventory.LootChestPanel
                : FindChildGameObject(sourceRoot.transform, "LootChestPanel");
            if (sourceLootPanel == null)
            {
                Debug.LogError("Source Canvas prefab is missing LootChestPanel.");
                return null;
            }

            GameObject shopPanel = Object.Instantiate(sourceLootPanel, targetParent, false);
            shopPanel.name = "ShopPanel";
            return shopPanel;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
        }
    }

    private static InventoryUIController ConfigureShopPanel(GameObject shopPanel)
    {
        if (shopPanel == null)
        {
            return null;
        }

        shopPanel.SetActive(true);
        RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-ShopPanelRightInset, 0f);
            panelRect.sizeDelta = new Vector2(ShopPanelWidth, ShopPanelHeight);
            panelRect.localScale = Vector3.one;
            panelRect.localRotation = Quaternion.identity;
        }

        Text headerText = FindChildComponentByName<Text>(shopPanel.transform, "LootHeaderText");
        if (headerText == null)
        {
            headerText = FindChildComponentByName<Text>(shopPanel.transform, "ShopHeaderText");
        }

        if (headerText != null)
        {
            headerText.name = "ShopHeaderText";
            headerText.text = "售卖处";
            RectTransform headerRect = headerText.rectTransform;
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = new Vector2(-160f, 0f);
        }

        EnsureRefreshCountdownText(shopPanel.transform);

        InventoryUIController shopGrid = shopPanel.GetComponentInChildren<InventoryUIController>(true);
        if (shopGrid == null)
        {
            Debug.LogError("Shop panel is missing an InventoryUIController.");
            return null;
        }

        shopGrid.name = "ShopGrid";
        shopGrid.CellSize = ShopCellSize;
        shopGrid.Spacing = ShopCellSpacing;
        ConfigureShopGridContainer(shopGrid);
        shopGrid.RebuildGridUI(ShopColumns, ShopRows, new List<Vector2Int>());

        InventoryGridInteractionPolicy interactionPolicy = shopGrid.GetComponent<InventoryGridInteractionPolicy>();
        if (interactionPolicy == null)
        {
            interactionPolicy = shopGrid.gameObject.AddComponent<InventoryGridInteractionPolicy>();
        }

        interactionPolicy.AllowItemDragStart = false;
        interactionPolicy.AllowItemDrops = false;

        if (shopGrid.GetComponent<ShopGridDropBlocker>() == null)
        {
            shopGrid.gameObject.AddComponent<ShopGridDropBlocker>();
        }

        return shopGrid;
    }

    private static void ConfigureShopGridContainer(InventoryUIController shopGrid)
    {
        if (shopGrid == null)
        {
            return;
        }

        Vector2 gridSize = CalculateShopGridSize();
        RectTransform gridRect = shopGrid.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.sizeDelta = gridSize;
        }

        RectTransform gridContainer = shopGrid.transform.parent as RectTransform;
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
    }

    private static Vector2 CalculateShopGridSize()
    {
        return new Vector2(
            ShopColumns * ShopCellSize + (ShopColumns - 1) * ShopCellSpacing,
            ShopRows * ShopCellSize + (ShopRows - 1) * ShopCellSpacing);
    }

    private static Text EnsureRefreshCountdownText(Transform shopPanel)
    {
        Text countdownText = FindChildComponentByName<Text>(shopPanel, "ShopRefreshCountdownText");
        if (countdownText == null)
        {
            Transform headerRow = shopPanel.Find("LootHeaderRow") ?? shopPanel;
            GameObject countdownObject = CreateUiObject("ShopRefreshCountdownText", headerRow);
            countdownText = countdownObject.AddComponent<Text>();
        }

        RectTransform rect = countdownText.rectTransform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        rect.sizeDelta = new Vector2(150f, 32f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        countdownText.font = ResolveFont(shopPanel);
        countdownText.fontSize = 15;
        countdownText.alignment = TextAnchor.MiddleRight;
        countdownText.color = Color.white;
        countdownText.resizeTextForBestFit = true;
        countdownText.resizeTextMinSize = 10;
        countdownText.resizeTextMaxSize = 15;
        countdownText.raycastTarget = false;
        countdownText.text = "刷新：30:00";
        return countdownText;
    }

    private static void DisableOriginalBackpackArea(InventoryScreenController inventoryScreen, GameObject storagePanel)
    {
        if (inventoryScreen == null)
        {
            return;
        }

        if (inventoryScreen.InventoryPanel != null &&
            storagePanel != null &&
            !storagePanel.transform.IsChildOf(inventoryScreen.InventoryPanel.transform))
        {
            inventoryScreen.InventoryPanel.SetActive(false);
        }

        DisableIfNotNull(inventoryScreen.BackpackGrid != null ? inventoryScreen.BackpackGrid.gameObject : null);
        DisableIfNotNull(inventoryScreen.PocketGrid != null ? inventoryScreen.PocketGrid.gameObject : null);
        DisableIfNotNull(inventoryScreen.TacticalRigGrid != null ? inventoryScreen.TacticalRigGrid.gameObject : null);
        DisableIfNotNull(inventoryScreen.HeadSlot != null ? inventoryScreen.HeadSlot.gameObject : null);
        DisableIfNotNull(inventoryScreen.BodySlot != null ? inventoryScreen.BodySlot.gameObject : null);
        DisableIfNotNull(inventoryScreen.FaceSlot != null ? inventoryScreen.FaceSlot.gameObject : null);
        DisableIfNotNull(inventoryScreen.HeadphoneSlot != null ? inventoryScreen.HeadphoneSlot.gameObject : null);
        DisableIfNotNull(inventoryScreen.TotemSlotA != null ? inventoryScreen.TotemSlotA.gameObject : null);
        DisableIfNotNull(inventoryScreen.TotemSlotB != null ? inventoryScreen.TotemSlotB.gameObject : null);
    }

    private static void DisableIfNotNull(GameObject gameObject)
    {
        if (gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    private static Button EnsureReturnButton(Transform parent, Sprite returnButtonSprite)
    {
        Button button = FindChildComponentByName<Button>(parent, "ShopReturnButton");
        if (button == null)
        {
            GameObject buttonObject = new GameObject(
                "ShopReturnButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(ReturnButtonWidth, ReturnButtonHeight);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = button.GetComponent<Image>();
        image.sprite = returnButtonSprite;
        image.enabled = returnButtonSprite != null;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = true;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.82f, 0.9f, 1f, 0.92f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.onClick.RemoveAllListeners();

        Text label = FindChildComponentByName<Text>(button.transform, "Label");
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.GetComponent<Text>();
        }

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        label.font = ResolveFont(parent);
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
        if (shadow != null)
        {
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
        return button;
    }

    private static void CreateShopTestScene()
    {
        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopCanvasPrefabPath);
        if (shopPrefab == null)
        {
            Debug.LogError("Shop Canvas prefab is missing, test scene was not created.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ShopCanvasTest";

        CreateCamera();
        PrefabUtility.InstantiatePrefab(shopPrefab);
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ShopTestScenePath);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.16f, 0.18f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static InventoryItemDatabase EnsureInventoryItemDatabaseAsset()
    {
        EnsureAssetFolder("Assets/Resources/Inventory");

        InventoryItemDatabase database = AssetDatabase.LoadAssetAtPath<InventoryItemDatabase>(ItemDatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<InventoryItemDatabase>();
            AssetDatabase.CreateAsset(database, ItemDatabasePath);
        }

        database.Items.Clear();
        string[] guids = AssetDatabase.FindAssets("t:InventoryItemData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            InventoryItemData item = AssetDatabase.LoadAssetAtPath<InventoryItemData>(path);
            if (item != null && item.IncludeInRuntimeDatabase && !database.Items.Contains(item))
            {
                database.Items.Add(item);
            }
        }

        EditorUtility.SetDirty(database);
        return database;
    }

    private static TotemShopPool EnsureDefaultShopPoolAsset(InventoryItemDatabase database)
    {
        EnsureAssetFolder("Assets/Resources/Shop");

        TotemShopPool shopPool = AssetDatabase.LoadAssetAtPath<TotemShopPool>(ShopPoolPath);
        if (shopPool == null)
        {
            shopPool = ScriptableObject.CreateInstance<TotemShopPool>();
            AssetDatabase.CreateAsset(shopPool, ShopPoolPath);
        }

        SerializedObject serializedPool = new SerializedObject(shopPool);
        SerializedProperty entries = serializedPool.FindProperty("_entries");
        entries.ClearArray();

        if (database != null && database.Items != null)
        {
            foreach (InventoryItemData item in database.Items)
            {
                if (item == null ||
                    item.EquipmentKind != EquipmentSlotKind.Totem ||
                    !item.IncludeInRuntimeDatabase ||
                    !item.IncludeInTotemShop)
                {
                    continue;
                }

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("ItemData").objectReferenceValue = item;
                entry.FindPropertyRelative("BuyPrice").intValue = Mathf.Max(1, ShopEconomyPriceUtility.ResolveSellPrice(item, shopPool) * 2);
                entry.FindPropertyRelative("Weight").floatValue = 1f;
            }
        }

        serializedPool.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopPool);
        return shopPool;
    }

    private static void RemoveRaidOnlyHud(GameObject prefabRoot)
    {
        foreach (RaidMinimapController minimap in prefabRoot.GetComponentsInChildren<RaidMinimapController>(true))
        {
            if (minimap != null)
            {
                Object.DestroyImmediate(minimap);
            }
        }

        foreach (PlayerStatusHudController statusHud in prefabRoot.GetComponentsInChildren<PlayerStatusHudController>(true))
        {
            if (statusHud != null)
            {
                Object.DestroyImmediate(statusHud.gameObject);
            }
        }

        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            Transform child = transforms[i];
            if (child != null &&
                (child.name == "RaidMinimapCanvas" ||
                 child.name == "Pfb_PlayerStatusHud" ||
                 child.name == "PlayerStatusHudRoot"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void UnpackNestedPrefabInstances(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform == null || transform.gameObject == root)
            {
                continue;
            }

            GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject);
            if (nearestRoot != transform.gameObject)
            {
                continue;
            }

            PrefabUtility.UnpackPrefabInstance(
                transform.gameObject,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component != null && component.name == childName)
            {
                return component;
            }
        }

        return null;
    }

    private static RectTransform FindChildRectTransform(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (RectTransform rectTransform in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rectTransform != null && rectTransform.name == childName)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private static GameObject FindChildGameObject(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static Font ResolveFont(Transform root)
    {
        Text existing = root != null ? root.GetComponentInChildren<Text>(true) : null;
        return existing != null && existing.font != null
            ? existing.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
