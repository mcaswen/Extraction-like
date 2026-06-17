using System.Collections.Generic;
using UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Scene_MainMenu.unity";
    private const string NextScenePath = "Assets/Scenes/Scene_PreparationInterface.unity";
    private const string NextSceneName = "Scene_PreparationInterface";
    private const string SpriteRoot = "Assets/Art/Sprites/Png_Item_Main menu/";
    private const string BackgroundPath = SpriteRoot + "Png_Item_ Background.PNG";
    private const string GameNamePath = SpriteRoot + "Png_Item_ Game name.PNG";
    private const string ButtonPath = SpriteRoot + "Png_Item_ Button.PNG";
    private const string TitleFontPath = "Assets/Font/Title.ttf";

    private static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
    private static readonly Vector2 ButtonSpriteSourceCenter = new Vector2(437f, 82f);
    private static readonly Color ButtonTextColor = new Color(0.06f, 0.17f, 0.25f, 1f);

    private static readonly MenuButtonSpec[] Buttons =
    {
        new MenuButtonSpec("Start Game Button", "Start Game", new Vector2(437f, 82f), new Vector2(360f, 108f), 42),
        new MenuButtonSpec("Settings Button", "Settings", new Vector2(437f, -63f), new Vector2(360f, 108f), 46),
        new MenuButtonSpec("Exit Game Button", "Exit Game", new Vector2(437f, -208f), new Vector2(360f, 108f), 42)
    };

    [MenuItem("Tools/UI/Rebuild Main Menu Scene")]
    public static void RebuildMainMenuScene()
    {
        Sprite backgroundSprite = ImportSprite(BackgroundPath);
        Sprite gameNameSprite = ImportSprite(GameNamePath);
        Sprite buttonSprite = ImportSprite(ButtonPath);
        Font titleFont = LoadFont(TitleFontPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Scene_MainMenu";

        CreateCamera();
        Canvas canvas = CreateCanvas();
        MainMenuController controller = canvas.GetComponent<MainMenuController>();

        CreateFullCanvasImage(canvas.transform, "Background", backgroundSprite, Vector2.zero);
        CreateFullCanvasImage(canvas.transform, "GameName", gameNameSprite, Vector2.zero);

        Text[] labels = CreateMenuButtons(canvas.transform, buttonSprite, titleFont, controller);
        AssignControllerReferences(controller, titleFont, labels);

        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        Debug.Log("Main menu scene rebuilt: " + MenuScenePath);
    }

    public static void RebuildMainMenuSceneFromCommandLine()
    {
        RebuildMainMenuScene();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(MainMenuController));

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
        camera.backgroundColor = new Color(0.08f, 0.13f, 0.16f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.cullingMask = 0;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Text[] CreateMenuButtons(
        Transform parent,
        Sprite buttonSprite,
        Font titleFont,
        MainMenuController controller)
    {
        Text[] labels = new Text[Buttons.Length];
        for (int i = 0; i < Buttons.Length; i++)
        {
            MenuButtonSpec spec = Buttons[i];
            Image buttonGraphic = CreateFullCanvasImage(parent, spec.Name + " Background", buttonSprite, spec.Center - ButtonSpriteSourceCenter);

            Button button = CreateButtonHotspot(parent, spec, titleFont, buttonGraphic, out Text label);
            BindButton(button, controller, i);
            labels[i] = label;
        }

        return labels;
    }

    private static Button CreateButtonHotspot(
        Transform parent,
        MenuButtonSpec spec,
        Font titleFont,
        Graphic hoverTarget,
        out Text label)
    {
        GameObject buttonObject = new GameObject(
            spec.Name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = spec.Center;
        buttonRect.sizeDelta = spec.HotspotSize;

        Image hitImage = buttonObject.GetComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0f);
        hitImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = hoverTarget;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.58f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.36f, 0.82f, 1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 0.45f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 4f);
        labelRect.offsetMax = new Vector2(-18f, -4f);

        label = labelObject.GetComponent<Text>();
        label.text = spec.Label;
        label.font = titleFont != null ? titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = spec.FontSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 24;
        label.resizeTextMaxSize = spec.FontSize;
        label.color = ButtonTextColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.52f);
        outline.effectDistance = new Vector2(1.25f, -1.25f);

        return button;
    }

    private static void BindButton(Button button, MainMenuController controller, int index)
    {
        if (button == null || controller == null)
            return;

        switch (index)
        {
            case 0:
                UnityEventTools.AddPersistentListener(button.onClick, controller.StartGame);
                break;
            case 1:
                UnityEventTools.AddPersistentListener(button.onClick, controller.OpenSettings);
                break;
            case 2:
                UnityEventTools.AddPersistentListener(button.onClick, controller.ExitGame);
                break;
        }
    }

    private static Image CreateFullCanvasImage(Transform parent, string name, Sprite sprite, Vector2 anchoredPosition)
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
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = ReferenceResolution;

        return image;
    }

    private static void AssignControllerReferences(MainMenuController controller, Font titleFont, Text[] labels)
    {
        if (controller == null)
            return;

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("nextSceneName").stringValue = NextSceneName;
        serializedController.FindProperty("menuFont").objectReferenceValue = titleFont;

        SerializedProperty labelsProperty = serializedController.FindProperty("menuLabels");
        labelsProperty.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
            labelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];

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
            Debug.LogError("Main menu sprite is missing: " + path);

        return sprite;
    }

    private static Font LoadFont(string path)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (font == null)
            Debug.LogError("Main menu font is missing: " + path);

        return font;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        HashSet<string> includedPaths = new HashSet<string>();

        AddBuildSceneIfMissing(scenes, includedPaths, MenuScenePath);
        AddBuildSceneIfMissing(scenes, includedPaths, NextScenePath);

        foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
        {
            if (existingScene == null || string.IsNullOrEmpty(existingScene.path))
                continue;

            AddBuildSceneIfMissing(scenes, includedPaths, existingScene.path, existingScene.enabled);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildSceneIfMissing(
        List<EditorBuildSettingsScene> scenes,
        HashSet<string> includedPaths,
        string scenePath,
        bool enabled = true)
    {
        if (includedPaths.Contains(scenePath))
            return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, enabled));
        includedPaths.Add(scenePath);
    }

    private readonly struct MenuButtonSpec
    {
        public readonly string Name;
        public readonly string Label;
        public readonly Vector2 Center;
        public readonly Vector2 HotspotSize;
        public readonly int FontSize;

        public MenuButtonSpec(string name, string label, Vector2 center, Vector2 hotspotSize, int fontSize)
        {
            Name = name;
            Label = label;
            Center = center;
            HotspotSize = hotspotSize;
            FontSize = fontSize;
        }
    }
}
