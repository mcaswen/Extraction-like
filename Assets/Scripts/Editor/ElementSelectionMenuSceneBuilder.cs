using System.Collections.Generic;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ElementSelectionMenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Scene_ElementSelectionMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Scene_lyl_test2 1.unity";
    private const string AttributeSpriteRoot = "Assets/Art/Sprites/Png_Item_attribute selection/";
    private const string BackgroundPath = AttributeSpriteRoot + "Png_Item_ background.PNG";
    private const string ButtonPath = AttributeSpriteRoot + "Png_Item_ button.PNG";
    private const string PanelPath = AttributeSpriteRoot + "Png_Item_ panel.PNG";
    private const string StartButtonPath = "Assets/Art/Sprites/UI/supplement/IMG_0826.PNG";
    private const string TitleFontPath = "Assets/Font/Title.ttf";
    private const string TextFontPath = "Assets/Font/Text.ttf";
    private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
    private static readonly Vector2 ButtonSourceCenter = new Vector2(460.3333f, 281.3333f);
    private static readonly Vector2 ButtonLayerSize = new Vector2(1280f, 720f);
    private static readonly Vector2 ElementIconSize = new Vector2(78f, 78f);
    private static readonly Vector2 ElementTextSize = new Vector2(245f, 92f);
    private static readonly Vector2 StartButtonSize = new Vector2(320f, 188f);
    private static readonly Vector2 StartTextSize = new Vector2(240f, 96f);
    private static readonly Vector2 StartCenter = new Vector2(124f, -5f);
    private static readonly Vector2 StartHotspotSize = new Vector2(320f, 180f);
    private static readonly ElementVisualSpec[] Elements =
    {
        new ElementVisualSpec("Fire", AttributeSpriteRoot + "Png_Item_ fire.PNG", new Vector2(475f, 276f), new Color(0.96f, 0.62f, 0.36f, 1f)),
        new ElementVisualSpec("Ice", AttributeSpriteRoot + "Png_Item_ ice.PNG", new Vector2(475f, 131f), new Color(0.76f, 0.94f, 1f, 1f)),
        new ElementVisualSpec("Earth", AttributeSpriteRoot + "Png_Item_ earth.PNG", new Vector2(475f, -18f), new Color(0.82f, 0.68f, 0.50f, 1f)),
        new ElementVisualSpec("Water", AttributeSpriteRoot + "Png_Item_ water.PNG", new Vector2(475f, -154f), new Color(0.52f, 0.96f, 0.89f, 1f)),
        new ElementVisualSpec("Metal", AttributeSpriteRoot + "Png_Item_ metal.PNG", new Vector2(475f, -288f), new Color(0.98f, 0.90f, 0.43f, 1f))
    };

    [MenuItem("Tools/UI/Rebuild Element Selection Menu Scene")]
    public static void RebuildElementSelectionMenuScene()
    {
        Sprite backgroundSprite = ImportSprite(BackgroundPath);
        Sprite buttonSprite = ImportSprite(ButtonPath);
        Sprite panelSprite = ImportSprite(PanelPath);
        Sprite startButtonSprite = ImportSprite(StartButtonPath);
        Font titleFont = LoadFont(TitleFontPath);
        Font textFont = LoadFont(TextFontPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Scene_ElementSelectionMenu";

        CreateCamera();
        Canvas canvas = CreateCanvas();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        ConfigureController(canvas.GetComponent<ElementSelectionMenuController>(), canvasRect, buttonSprite, startButtonSprite);

        CreateFullCanvasImage(canvas.transform, "Background", backgroundSprite, Vector2.zero);
        CreateFullCanvasImage(canvas.transform, "RightPanel", panelSprite, Vector2.zero);
        CreateElementButtons(canvas.transform, buttonSprite);
        CreateElementIconsAndLabels(canvas.transform, titleFont);
        CreateCenteredImage(canvas.transform, "StartButtonBackground", startButtonSprite, StartCenter, StartButtonSize, false);
        CreateText(canvas.transform, "StartButtonText", "Start", textFont, 58, new Color(0.16f, 0.22f, 0.28f, 1f), StartCenter, StartTextSize, TextAnchor.MiddleCenter);
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        Debug.Log("Element selection menu scene rebuilt: " + MenuScenePath);
    }

    public static void RebuildElementSelectionMenuSceneFromCommandLine()
    {
        RebuildElementSelectionMenuScene();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(ElementSelectionMenuController));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.13f, 0.18f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.cullingMask = 0;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateElementButtons(Transform parent, Sprite buttonSprite)
    {
        for (int i = 0; i < Elements.Length; i++)
        {
            Vector2 offset = Elements[i].Center - ButtonSourceCenter;
            CreateFullCanvasImage(parent, Elements[i].Key + "Button", buttonSprite, offset);
        }
    }

    private static void CreateElementIconsAndLabels(Transform parent, Font titleFont)
    {
        foreach (ElementVisualSpec element in Elements)
        {
            Sprite iconSprite = ImportSprite(element.IconPath);
            Vector2 iconCenter = new Vector2(386f, element.Center.y);
            Vector2 textCenter = new Vector2(590f, element.Center.y + 1f);
            CreateCenteredImage(parent, element.Key + "Icon", iconSprite, iconCenter, ElementIconSize, true);
            CreateText(parent, element.Key + "Label", element.Key, titleFont, 55, element.TextColor, textCenter, ElementTextSize, TextAnchor.MiddleLeft);
        }
    }

    private static void CreateFullCanvasImage(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition)
    {
        CreateCenteredImage(parent, name, sprite, anchoredPosition, ButtonLayerSize, false);
    }

    private static void CreateCenteredImage(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 anchoredPosition,
        Vector2 size,
        bool preserveAspect)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = preserveAspect;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void CreateText(
        Transform parent,
        string name,
        string value,
        Font font,
        int fontSize,
        Color color,
        Vector2 anchoredPosition,
        Vector2 size,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void ConfigureController(
        ElementSelectionMenuController controller,
        RectTransform highlightRoot,
        Sprite selectedButtonSprite,
        Sprite startReadySprite)
    {
        if (controller == null)
            return;

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("gameplaySceneName").stringValue = "Scene_lyl_test2 1";
        serializedController.FindProperty("requiredSelectionCount").intValue = 2;
        serializedController.FindProperty("referenceResolution").vector2Value = ReferenceResolution;
        serializedController.FindProperty("highlightRoot").objectReferenceValue = highlightRoot;
        serializedController.FindProperty("selectedButtonSprite").objectReferenceValue = selectedButtonSprite;
        serializedController.FindProperty("startReadySprite").objectReferenceValue = startReadySprite;
        serializedController.FindProperty("buttonSpriteReferenceSize").vector2Value = ButtonLayerSize;
        serializedController.FindProperty("buttonSpriteSourceCenter").vector2Value = ButtonSourceCenter;
        serializedController.FindProperty("selectedFillColor").colorValue = new Color(0.38f, 0.94f, 1f, 0.3f);
        serializedController.FindProperty("selectedOutlineColor").colorValue = new Color(0.7f, 1f, 1f, 0.28f);
        serializedController.FindProperty("startReadyFillColor").colorValue = new Color(1f, 1f, 1f, 0.26f);
        serializedController.FindProperty("startReadyOutlineColor").colorValue = new Color(0.62f, 0.95f, 1f, 0.28f);
        serializedController.FindProperty("hoverFillColor").colorValue = new Color(0.86f, 0.98f, 1f, 0.28f);
        serializedController.FindProperty("hoverShiftColor").colorValue = new Color(0.22f, 0.9f, 1f, 0.38f);
        serializedController.FindProperty("hoverGlowColor").colorValue = new Color(0.54f, 0.95f, 1f, 0.26f);
        serializedController.FindProperty("hoverPulseSpeed").floatValue = 4.6f;
        serializedController.FindProperty("hoverPulseScale").floatValue = 0.018f;
        serializedController.FindProperty("hoverPulseAlpha").floatValue = 0.24f;
        serializedController.FindProperty("selectionPulseSpeed").floatValue = 3.6f;
        serializedController.FindProperty("selectionPulseScale").floatValue = 0.028f;
        serializedController.FindProperty("selectionPulseAlpha").floatValue = 0.24f;

        SerializedProperty hotspots = serializedController.FindProperty("elementHotspots");
        hotspots.arraySize = Elements.Length;
        for (int i = 0; i < Elements.Length; i++)
        {
            SerializedProperty element = hotspots.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("ElementKey").stringValue = Elements[i].Key;
            element.FindPropertyRelative("Center").vector2Value = Elements[i].Center;
            element.FindPropertyRelative("Size").vector2Value = new Vector2(330f, 100f);
        }

        SerializedProperty startHotspot = serializedController.FindProperty("startHotspot");
        startHotspot.FindPropertyRelative("ElementKey").stringValue = "Start";
        startHotspot.FindPropertyRelative("Center").vector2Value = StartCenter;
        startHotspot.FindPropertyRelative("Size").vector2Value = StartHotspotSize;

        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError("Element selection background sprite is missing: " + path);

        return sprite;
    }

    private static Font LoadFont(string path)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (font == null)
            Debug.LogError("Element selection font is missing: " + path);

        return font;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        HashSet<string> includedPaths = new HashSet<string>();
        foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
        {
            if (existingScene == null || string.IsNullOrEmpty(existingScene.path))
                continue;

            if (includedPaths.Contains(existingScene.path))
                continue;

            scenes.Add(existingScene);
            includedPaths.Add(existingScene.path);
        }

        AddBuildSceneIfMissing(scenes, includedPaths, MenuScenePath);
        AddBuildSceneIfMissing(scenes, includedPaths, GameplayScenePath);

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildSceneIfMissing(
        List<EditorBuildSettingsScene> scenes,
        HashSet<string> includedPaths,
        string scenePath)
    {
        if (includedPaths.Contains(scenePath))
            return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        includedPaths.Add(scenePath);
    }

    private readonly struct ElementVisualSpec
    {
        public readonly string Key;
        public readonly string IconPath;
        public readonly Vector2 Center;
        public readonly Color TextColor;

        public ElementVisualSpec(string key, string iconPath, Vector2 center, Color textColor)
        {
            Key = key;
            IconPath = iconPath;
            Center = center;
            TextColor = textColor;
        }
    }
}
