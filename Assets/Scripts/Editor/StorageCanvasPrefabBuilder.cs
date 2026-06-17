using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StorageCanvasPrefabBuilder
{
    private const string SourceCanvasPrefabPath = "Assets/Prefabs/Canvas.prefab";
    private const string StorageCanvasPrefabPath = "Assets/Prefabs/StorageCanvas.prefab";
    private const string StorageTestScenePath = "Assets/Scenes/StorageCanvasTest.unity";
    private const string ItemDatabasePath = "Assets/Resources/Inventory/InventoryItemDatabase.asset";
    private const string PreparationBackgroundPath = "Assets/Art/Sprites/Png_Item_preparation interface/Png_Item_background.PNG";
    private const string StoragePanelName = "StoragePanel";
    private const string PageScrollName = "StoragePageScrollView";
    private const string PageViewportName = "Viewport";
    private const string PageContentName = "Content";
    private const string PageTemplateName = "StoragePageButtonTemplate";
    private const string StorageSortButtonName = "StorageSortButton";
    private const string BuildRequestFlagPath = "Temp/StorageCanvasBuildRequested.flag";
    private const int BackpackColumns = 5;
    private const int BackpackRows = 6;
    private const int StorageColumns = 6;
    private const int StorageRows = 10;
    private const int StorageMinimumPageCount = 10;
    private const float StorageCellSize = 70f;
    private const float StorageSpacing = 4f;
    private const float StoragePanelRightInset = 118f;
    private const float PageSelectorGap = 12f;
    private const float PageSelectorWidth = 64f;
    private const float PageSelectorHeight = 560f;
    private const float PageButtonWidth = 52f;
    private const float PageButtonHeight = 38f;
    private const float PageButtonSpacing = 10f;
    private static readonly Vector2 PreparationBackgroundReferenceResolution = new Vector2(1280f, 720f);

    [InitializeOnLoadMethod]
    private static void RunRequestedBuildAfterReload()
    {
        string buildRequestFlagPath = ResolveProjectPath(BuildRequestFlagPath);
        if (!File.Exists(buildRequestFlagPath))
        {
            return;
        }

        File.Delete(buildRequestFlagPath);
        EditorApplication.delayCall += RebuildStorageCanvasAndScene;
    }

    [MenuItem("Tools/Backpack/Rebuild Storage Canvas")]
    public static void RebuildStorageCanvasAndScene()
    {
        EnsureInventoryItemDatabaseAsset();
        CreateIndependentStoragePrefab();
        CreateStorageTestScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(StorageCanvasPrefabPath);
        Debug.Log("Storage canvas prefab and test scene rebuilt.");
    }

    public static void RebuildStorageCanvasAndSceneFromCommandLine()
    {
        RebuildStorageCanvasAndScene();
    }

    private static void CreateIndependentStoragePrefab()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(SourceCanvasPrefabPath))
        {
            Debug.LogError("Source Canvas prefab is missing: " + SourceCanvasPrefabPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(StorageCanvasPrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(StorageCanvasPrefabPath);
        }

        if (!AssetDatabase.CopyAsset(SourceCanvasPrefabPath, StorageCanvasPrefabPath))
        {
            Debug.LogError("Failed to copy Canvas prefab to " + StorageCanvasPrefabPath);
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(StorageCanvasPrefabPath);
        try
        {
            prefabRoot.name = "StorageCanvas";
            UnpackNestedPrefabInstances(prefabRoot);
            ConfigureStoragePrefab(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, StorageCanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureStoragePrefab(GameObject prefabRoot)
    {
        InventoryScreenController inventoryScreen = prefabRoot.GetComponentInChildren<InventoryScreenController>(true);
        if (inventoryScreen == null)
        {
            Debug.LogError("Storage prefab source is missing InventoryScreenController.");
            return;
        }

        GameObject controllerObject = inventoryScreen.gameObject;
        PlayerStorageService storageService = GetOrAddComponent<PlayerStorageService>(controllerObject);
        StorageScreenController storageController = GetOrAddComponent<StorageScreenController>(controllerObject);

        storageService.Columns = StorageColumns;
        storageService.Rows = StorageRows;
        storageService.MinimumPageCount = StorageMinimumPageCount;
        storageService.FallbackAgentId = "default_player";

        storageController.InventoryScreen = inventoryScreen;
        storageController.StorageService = storageService;
        storageController.StorageGrid = inventoryScreen.LootChestGrid;
        storageController.Columns = StorageColumns;
        storageController.Rows = StorageRows;
        storageController.MinimumPageCount = StorageMinimumPageCount;
        storageController.CellSize = StorageCellSize;
        storageController.Spacing = StorageSpacing;
        storageController.OpenOnStart = true;
        storageController.FallbackAgentId = "default_player";
        storageController.SeedBackpackWithTestItems = true;
        storageController.SeedOnlyWhenBackpackIsEmpty = true;
        storageController.TestBackpackItemCount = 6;

        RemoveRaidOnlyHud(prefabRoot);
        DisableRunRevenueWidget(inventoryScreen);
        ConfigureBackpackPanelForStorage(inventoryScreen);
        ConfigureLootPanelAsStorage(inventoryScreen, storageController);
    }

    private static void RemoveRaidOnlyHud(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return;
        }

        foreach (RaidMinimapController minimap in prefabRoot.GetComponentsInChildren<RaidMinimapController>(true))
        {
            if (minimap != null)
            {
                Object.DestroyImmediate(minimap);
            }
        }

        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            Transform child = transforms[i];
            if (child != null && child.name == "RaidMinimapCanvas")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void DisableRunRevenueWidget(InventoryScreenController inventoryScreen)
    {
        SerializedObject serializedObject = new SerializedObject(inventoryScreen);
        SerializedProperty showRunRevenue = serializedObject.FindProperty("_showRunRevenueOnInventory");
        if (showRunRevenue != null)
        {
            showRunRevenue.boolValue = false;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureLootPanelAsStorage(
        InventoryScreenController inventoryScreen,
        StorageScreenController storageController)
    {
        GameObject storagePanel = inventoryScreen.LootChestPanel;
        if (storagePanel == null && inventoryScreen.LootChestGrid != null)
        {
            storagePanel = inventoryScreen.LootChestGrid.gameObject;
        }

        if (storagePanel == null)
        {
            Debug.LogError("Storage prefab source is missing the right-side loot panel.");
            return;
        }

        storagePanel.name = StoragePanelName;
        Text headerText = FindChildComponentByName<Text>(storagePanel.transform, "LootHeaderText");
        if (headerText != null)
        {
            headerText.name = "StorageHeaderText";
            headerText.text = "\u4ed3\u5e93 1/10 (0/60)";
            headerText.resizeTextForBestFit = true;
            headerText.resizeTextMinSize = 13;
            headerText.resizeTextMaxSize = Mathf.Max(16, headerText.fontSize);
            storageController.StorageHeaderText = headerText;
        }

        if (inventoryScreen.LootChestGrid != null)
        {
            inventoryScreen.LootChestGrid.CellSize = StorageCellSize;
            inventoryScreen.LootChestGrid.Spacing = StorageSpacing;
            inventoryScreen.LootChestGrid.RebuildGridUI(StorageColumns, StorageRows, new List<Vector2Int>());
        }

        RectTransform scrollRoot = BuildPageList(storagePanel.transform);
        storageController.PageButtonContainer = FindChildRectTransform(scrollRoot.transform, PageContentName);
        storageController.PageButtonTemplate = FindChildComponentByName<Button>(scrollRoot.transform, PageTemplateName);
        storageController.StorageSortButton = BuildStorageSortButton(storagePanel.transform, headerText);

        RectTransform panelRect = storagePanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-StoragePanelRightInset, 0f);
        }
    }

    private static void ConfigureBackpackPanelForStorage(InventoryScreenController inventoryScreen)
    {
        if (inventoryScreen.BackpackGrid == null)
        {
            return;
        }

        InventoryUIController backpackGrid = inventoryScreen.BackpackGrid;
        backpackGrid.gameObject.SetActive(true);
        if (backpackGrid.GridBackground != null)
        {
            backpackGrid.GridBackground.gameObject.SetActive(true);
        }

        if (backpackGrid.ItemContainer != null)
        {
            backpackGrid.ItemContainer.gameObject.SetActive(true);
        }

        backpackGrid.RebuildGridUI(BackpackColumns, BackpackRows, new List<Vector2Int>());

        RectTransform gridRect = backpackGrid.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(0f, 1f);
            gridRect.pivot = new Vector2(0f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.localScale = Vector3.one;
            gridRect.localRotation = Quaternion.identity;
        }

        RectTransform backpackSection = backpackGrid.transform.parent as RectTransform;
        RectTransform headerRow = inventoryScreen.InventoryPanel != null
            ? FindChildRectTransform(inventoryScreen.InventoryPanel.transform, "BackpackHeaderRow")
            : null;
        if (backpackSection == null || headerRow == null || backpackSection.parent != headerRow.parent)
        {
            return;
        }

        Vector2 gridSize = backpackGrid.GetItemActualSize(BackpackColumns, BackpackRows);
        backpackSection.anchorMin = new Vector2(0f, 1f);
        backpackSection.anchorMax = new Vector2(0f, 1f);
        backpackSection.pivot = new Vector2(0.5f, 1f);
        backpackSection.anchoredPosition = new Vector2(
            headerRow.anchoredPosition.x,
            headerRow.anchoredPosition.y - headerRow.sizeDelta.y * (1f - headerRow.pivot.y) - 10f);
        backpackSection.sizeDelta = new Vector2(Mathf.Max(headerRow.sizeDelta.x, gridSize.x), gridSize.y);
        backpackSection.localScale = Vector3.one;
        backpackSection.localRotation = Quaternion.identity;
        backpackSection.SetAsLastSibling();
    }

    private static RectTransform BuildPageList(Transform storagePanel)
    {
        Transform existing = storagePanel.Find(PageScrollName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject scrollObject = CreateUiObject(PageScrollName, storagePanel);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(1f, 0.5f);
        scrollRectTransform.anchorMax = new Vector2(1f, 0.5f);
        scrollRectTransform.pivot = new Vector2(0f, 0.5f);
        scrollRectTransform.anchoredPosition = new Vector2(PageSelectorGap, 0f);
        scrollRectTransform.sizeDelta = new Vector2(PageSelectorWidth, PageSelectorHeight);

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.04f, 0.1f, 0.14f, 0.88f);

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        GameObject viewportObject = CreateUiObject(PageViewportName, scrollObject.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(4f, 4f);
        viewport.offsetMax = new Vector2(-4f, -4f);

        viewportObject.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport;

        GameObject contentObject = CreateUiObject(PageContentName, viewportObject.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(PageButtonWidth, 0f);

        GridLayoutGroup grid = contentObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(PageButtonWidth, PageButtonHeight);
        grid.spacing = new Vector2(0f, PageButtonSpacing);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        scrollRect.content = content;

        Button template = CreatePageButtonTemplate(contentObject.transform);
        template.gameObject.SetActive(false);
        return scrollRectTransform;
    }

    private static Button BuildStorageSortButton(Transform storagePanel, Text headerText)
    {
        RectTransform existingButton = FindChildRectTransform(storagePanel, StorageSortButtonName);
        if (existingButton != null)
        {
            Object.DestroyImmediate(existingButton.gameObject);
        }

        Transform headerRow = storagePanel.Find("LootHeaderRow") ?? storagePanel;
        GameObject buttonObject = CreateUiObject(StorageSortButtonName, headerRow);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(1f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(-4f, 0f);
        rectTransform.sizeDelta = new Vector2(86f, 32f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.55f, 0.74f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.55f, 0.74f, 1f);
        colors.highlightedColor = new Color(0.25f, 0.68f, 0.9f, 1f);
        colors.pressedColor = new Color(0.1f, 0.38f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.28f, 0.34f, 0.6f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 15;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "\u6574\u7406";

        RectTransform headerTextRect = headerText != null ? headerText.GetComponent<RectTransform>() : null;
        if (headerTextRect != null)
        {
            headerTextRect.anchorMin = Vector2.zero;
            headerTextRect.anchorMax = Vector2.one;
            headerTextRect.offsetMin = Vector2.zero;
            headerTextRect.offsetMax = new Vector2(-100f, 0f);
        }

        return button;
    }

    private static Button CreatePageButtonTemplate(Transform parent)
    {
        GameObject buttonObject = CreateUiObject(PageTemplateName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.56f, 0.84f, 1f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.56f, 0.84f, 1f, 1f);
        colors.highlightedColor = new Color(0.68f, 0.91f, 1f, 1f);
        colors.pressedColor = new Color(0.12f, 0.48f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 13;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.08f, 0.21f, 0.32f, 1f);
        label.raycastTarget = false;
        label.text = "1";

        return button;
    }

    private static void CreateStorageTestScene()
    {
        GameObject storagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StorageCanvasPrefabPath);
        if (storagePrefab == null)
        {
            Debug.LogError("Storage Canvas prefab is missing, test scene was not created.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "StorageCanvasTest";

        CreateCamera();
        CreatePreparationBackgroundCanvas();
        PrefabUtility.InstantiatePrefab(storagePrefab);
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, StorageTestScenePath);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.72f, 0.82f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
    }

    private static void CreatePreparationBackgroundCanvas()
    {
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreparationBackgroundPath);
        if (backgroundSprite == null)
        {
            Debug.LogWarning("Preparation background sprite is missing: " + PreparationBackgroundPath);
            return;
        }

        GameObject canvasObject = new GameObject(
            "PreparationBackgroundCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = PreparationBackgroundReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image image = backgroundObject.GetComponent<Image>();
        image.sprite = backgroundSprite;
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = PreparationBackgroundReferenceResolution;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void EnsureInventoryItemDatabaseAsset()
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

    private static string ResolveProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
