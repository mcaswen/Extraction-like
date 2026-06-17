using System.Collections.Generic;
using UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PreparationInterfaceSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Scene_PreparationInterface.unity";
    private const string StoreScenePath = "Assets/Scenes/ShopCanvasTest.unity";
    private const string AttributeSelectionScenePath = "Assets/Scenes/Scene_ElementSelectionMenu.unity";
    private const string StoreSceneName = "ShopCanvasTest";
    private const string AttributeSelectionSceneName = "Scene_ElementSelectionMenu";
    private const string SpriteRoot = "Assets/Art/Sprites/Png_Item_preparation interface/";
    private const string BackgroundPath = SpriteRoot + "Png_Item_background.PNG";
    private const string PanelPath = SpriteRoot + "Png_Item_panel.PNG";
    private const string EnhancePath = SpriteRoot + "Png_Item_ skill enhancement.PNG";
    private const string StorePath = SpriteRoot + "Png_Item_ store.PNG";
    private const string SettingPath = SpriteRoot + "Png_Item_ setting.PNG";
    private const string StartPath = SpriteRoot + "Png_Item_start.PNG";
    private const string CharacterPath = "Assets/Art/Sprites/UI design/Battle/Jpg_protagonist.PNG";
    private const string TitleFontPath = "Assets/Font/Title.ttf";

    private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
    private static readonly Vector2 FullCanvasSize = new Vector2(1280f, 720f);
    private static readonly Vector2 CharacterCenter = new Vector2(-355f, -5f);
    private static readonly Vector2 CharacterSize = new Vector2(365f, 602f);
    private static readonly Color LabelColor = new Color(0.72f, 0.88f, 1f, 1f);
    private static readonly Color LabelShadowColor = new Color(0.08f, 0.17f, 0.24f, 0.34f);

    private static readonly ButtonSpec[] Buttons =
    {
        new ButtonSpec(
            "Enhance",
            "Enhance",
            EnhancePath,
            new Vector2(27.3f, 79.3f),
            new Vector2(280f, 128f),
            new Vector2(282f, 96f),
            46,
            nameof(PreparationInterfaceController.OpenEnhance)),
        new ButtonSpec(
            "Store",
            "Store",
            StorePath,
            new Vector2(244.3f, 80.3f),
            new Vector2(286f, 126f),
            new Vector2(250f, 96f),
            54,
            nameof(PreparationInterfaceController.OpenStore)),
        new ButtonSpec(
            "Setting",
            "Setting",
            SettingPath,
            new Vector2(468.3f, 80.7f),
            new Vector2(282f, 130f),
            new Vector2(270f, 96f),
            51,
            nameof(PreparationInterfaceController.OpenSettings)),
        new ButtonSpec(
            "Start",
            "Start",
            StartPath,
            new Vector2(279.3f, -141.3f),
            new Vector2(450f, 156f),
            new Vector2(360f, 118f),
            72,
            nameof(PreparationInterfaceController.StartAttributeSelection))
    };

    [MenuItem("Tools/UI/Rebuild Preparation Interface Scene")]
    public static void RebuildPreparationInterfaceScene()
    {
        Sprite backgroundSprite = ImportSprite(BackgroundPath);
        Sprite panelSprite = ImportSprite(PanelPath);
        Sprite characterSprite = ImportSprite(CharacterPath);
        Font titleFont = LoadFont(TitleFontPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Scene_PreparationInterface";

        CreateCamera();
        Canvas canvas = CreateCanvas();
        PreparationInterfaceController controller = canvas.GetComponent<PreparationInterfaceController>();
        ConfigureController(controller);

        CreateFullCanvasImage(canvas.transform, "Background", backgroundSprite);
        CreateCharacterImage(canvas.transform, characterSprite);
        CreateFullCanvasImage(canvas.transform, "CharacterPanel", panelSprite);

        foreach (ButtonSpec button in Buttons)
        {
            CreateFullCanvasImage(canvas.transform, button.Name + "Artwork", ImportSprite(button.SpritePath));
            CreateButtonHotspot(canvas.transform, button, controller);
            CreateButtonLabel(canvas.transform, button, titleFont);
        }

        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("Preparation interface scene rebuilt: " + ScenePath);
    }

    public static void RebuildPreparationInterfaceSceneFromCommandLine()
    {
        RebuildPreparationInterfaceScene();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(PreparationInterfaceController));

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
        camera.backgroundColor = new Color(0.54f, 0.72f, 0.82f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.cullingMask = 0;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateFullCanvasImage(Transform parent, string name, Sprite sprite)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = FullCanvasSize;
    }

    private static void CreateCharacterImage(Transform parent, Sprite sprite)
    {
        GameObject imageObject = new GameObject("Protagonist", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = CharacterCenter;
        rect.sizeDelta = CharacterSize;
    }

    private static Button CreateButtonHotspot(
        Transform parent,
        ButtonSpec spec,
        PreparationInterfaceController controller)
    {
        GameObject buttonObject = new GameObject(
            spec.Name + "Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = spec.Center;
        rect.sizeDelta = spec.HotspotSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        button.colors = colors;

        BindButton(button, controller, spec.MethodName);
        return button;
    }

    private static Text CreateButtonLabel(Transform parent, ButtonSpec spec, Font titleFont)
    {
        GameObject labelObject = new GameObject(
            spec.Name + "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Shadow));
        labelObject.transform.SetParent(parent, false);

        Text text = labelObject.GetComponent<Text>();
        text.text = spec.Label;
        text.font = titleFont;
        text.fontSize = spec.FontSize;
        text.color = LabelColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Shadow shadow = labelObject.GetComponent<Shadow>();
        shadow.effectColor = LabelShadowColor;
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.useGraphicAlpha = true;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = spec.Center;
        rect.sizeDelta = spec.LabelSize;

        return text;
    }

    private static void BindButton(
        Button button,
        PreparationInterfaceController controller,
        string methodName)
    {
        if (button == null || controller == null)
            return;

        switch (methodName)
        {
            case nameof(PreparationInterfaceController.OpenEnhance):
                UnityEventTools.AddPersistentListener(button.onClick, controller.OpenEnhance);
                break;
            case nameof(PreparationInterfaceController.OpenStore):
                UnityEventTools.AddPersistentListener(button.onClick, controller.OpenStore);
                break;
            case nameof(PreparationInterfaceController.OpenSettings):
                UnityEventTools.AddPersistentListener(button.onClick, controller.OpenSettings);
                break;
            case nameof(PreparationInterfaceController.StartAttributeSelection):
                UnityEventTools.AddPersistentListener(button.onClick, controller.StartAttributeSelection);
                break;
        }
    }

    private static void ConfigureController(PreparationInterfaceController controller)
    {
        if (controller == null)
            return;

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("storeSceneName").stringValue = StoreSceneName;
        serializedController.FindProperty("attributeSelectionSceneName").stringValue = AttributeSelectionSceneName;
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
            Debug.LogError("Preparation interface sprite is missing: " + path);

        return sprite;
    }

    private static Font LoadFont(string path)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (font == null)
            Debug.LogError("Preparation interface font is missing: " + path);

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

        AddBuildSceneIfMissing(scenes, includedPaths, ScenePath);
        AddBuildSceneIfMissing(scenes, includedPaths, StoreScenePath);
        AddBuildSceneIfMissing(scenes, includedPaths, AttributeSelectionScenePath);

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

    private readonly struct ButtonSpec
    {
        public readonly string Name;
        public readonly string Label;
        public readonly string SpritePath;
        public readonly Vector2 Center;
        public readonly Vector2 HotspotSize;
        public readonly Vector2 LabelSize;
        public readonly int FontSize;
        public readonly string MethodName;

        public ButtonSpec(
            string name,
            string label,
            string spritePath,
            Vector2 center,
            Vector2 hotspotSize,
            Vector2 labelSize,
            int fontSize,
            string methodName)
        {
            Name = name;
            Label = label;
            SpritePath = spritePath;
            Center = center;
            HotspotSize = hotspotSize;
            LabelSize = labelSize;
            FontSize = fontSize;
            MethodName = methodName;
        }
    }
}
