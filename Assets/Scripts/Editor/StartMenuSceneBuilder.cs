using System.Collections.Generic;
using System.IO;
using UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 重建开始菜单场景和相关界面图片资产的编辑器工具
/// </summary>
public static class StartMenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Scene_StartMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Scene_sdw_test2.unity";
    private const string OutputFolder = "Assets/Art/Sprites/UI/StartMenu";
    private const string BackgroundPath = OutputFolder + "/StartMenu_BeachBackground.png";
    private const string ButtonFramePath = OutputFolder + "/StartMenu_ButtonFrame.png";
    private const string CharacterPath = "Assets/Art/Sprites/UI design/Battle/Jpg_protagonist.PNG";

    /// <summary>
    /// 重建开始菜单场景并更新构建设置
    /// </summary>
    [MenuItem("Tools/UI/Rebuild Start Menu Scene")]
    public static void RebuildStartMenuScene()
    {
        EnsureAssetFolder(OutputFolder);
        Sprite backgroundSprite = CreateBackgroundSprite();
        Sprite buttonFrameSprite = CreateButtonFrameSprite();
        Sprite characterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterPath);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Scene_StartMenu";

        CreateCamera();
        Canvas canvas = CreateCanvas();
        StartMenuController controller = canvas.GetComponent<StartMenuController>();

        CreateFullScreenImage(canvas.transform, "Beach Background", backgroundSprite);
        CreateCharacter(canvas.transform, characterSprite);

        Text[] labels = CreateMenuButtons(canvas.transform, buttonFrameSprite, controller);
        AssignControllerReferences(controller, labels);

        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        Debug.Log("Start menu scene rebuilt: " + MenuScenePath);
    }

    /// <summary>
    /// 命令行入口，复用开始菜单重建流程
    /// </summary>
    public static void RebuildStartMenuSceneFromCommandLine()
    {
        RebuildStartMenuScene();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(StartMenuController));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
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
        camera.backgroundColor = new Color(0.49f, 0.82f, 0.91f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.cullingMask = 0;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateFullScreenImage(Transform parent, string name, Sprite sprite)
    {
        Image image = CreateImage(parent, name, sprite, Color.white);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CreateCharacter(Transform parent, Sprite characterSprite)
    {
        if (characterSprite == null)
        {
            Debug.LogWarning("Start menu character sprite is missing: " + CharacterPath);
            return;
        }

        Image image = CreateImage(parent, "Menu Character", characterSprite, Color.white);
        image.preserveAspect = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(255f, -2f);
        rect.sizeDelta = new Vector2(430f, 665f);
    }

    private static Text[] CreateMenuButtons(Transform parent, Sprite buttonFrameSprite, StartMenuController controller)
    {
        Text skillLabel = CreateMenuButton(
            parent,
            "Skill Upgrade Button",
            buttonFrameSprite,
            "\u6280\u80fd\u5f3a\u5316",
            new Vector2(95f, 135f),
            new Vector2(350f, 126f),
            50);

        Text shopLabel = CreateMenuButton(
            parent,
            "Shop Button",
            buttonFrameSprite,
            "\u5546\u5e97",
            new Vector2(430f, 135f),
            new Vector2(320f, 126f),
            52);

        Text settingsLabel = CreateMenuButton(
            parent,
            "Settings Button",
            buttonFrameSprite,
            "\u8bbe\u7f6e",
            new Vector2(80f, -110f),
            new Vector2(280f, 126f),
            52);

        Text startLabel = CreateMenuButton(
            parent,
            "Start Game Button",
            buttonFrameSprite,
            "\u5f00\u59cb\u6e38\u620f",
            new Vector2(400f, -110f),
            new Vector2(390f, 140f),
            58);

        Button startButton = startLabel.GetComponentInParent<Button>();
        UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);

        return new[] { skillLabel, shopLabel, settingsLabel, startLabel };
    }

    private static Text CreateMenuButton(
        Transform parent,
        string name,
        Sprite buttonFrameSprite,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = buttonFrameSprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.96f, 0.86f, 1f);
        colors.pressedColor = new Color(0.86f, 0.76f, 0.61f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 10f);
        labelRect.offsetMax = new Vector2(-18f, -10f);

        Text text = labelObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.02f, 0.19f, 0.48f, 1f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void AssignControllerReferences(StartMenuController controller, Text[] labels)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("gameplaySceneName").stringValue = "Scene_sdw_test2";

        SerializedProperty labelsProperty = serializedController.FindProperty("menuLabels");
        labelsProperty.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
            labelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];

        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite CreateBackgroundSprite()
    {
        if (!File.Exists(BackgroundPath))
        {
            Texture2D texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            DrawBackground(texture);
            WriteTexture(texture, BackgroundPath);
            Object.DestroyImmediate(texture);
        }

        return ImportSprite(BackgroundPath);
    }

    private static Sprite CreateButtonFrameSprite()
    {
        Texture2D texture = new Texture2D(640, 220, TextureFormat.RGBA32, false);
        DrawButtonFrame(texture);
        WriteTexture(texture, ButtonFramePath);
        Object.DestroyImmediate(texture);

        return ImportSprite(ButtonFramePath);
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

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void WriteTexture(Texture2D texture, string path)
    {
        string absolutePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
    }

    private static void DrawBackground(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color;
                if (y > 440)
                {
                    float t = Mathf.InverseLerp(440f, height - 1f, y);
                    color = Color.Lerp(new Color(0.55f, 0.86f, 0.96f), new Color(0.78f, 0.93f, 0.99f), t);
                }
                else if (y > 300)
                {
                    float t = Mathf.InverseLerp(300f, 440f, y);
                    color = Color.Lerp(new Color(0.16f, 0.76f, 0.86f), new Color(0.36f, 0.88f, 0.94f), t);
                }
                else
                {
                    float t = Mathf.InverseLerp(0f, 300f, y);
                    color = Color.Lerp(new Color(0.06f, 0.67f, 0.76f), new Color(0.25f, 0.86f, 0.90f), t);
                }

                float wave = Mathf.Sin((x * 0.036f) + (y * 0.055f)) * 0.5f + 0.5f;
                if (y < 280 && wave > 0.86f)
                    color = Color.Lerp(color, Color.white, 0.16f);

                texture.SetPixel(x, y, color);
            }
        }

        FillEllipse(texture, 870, 600, 80, 28, new Color(1f, 1f, 1f, 0.72f));
        FillEllipse(texture, 940, 612, 110, 38, new Color(1f, 1f, 1f, 0.64f));
        FillEllipse(texture, 1020, 595, 86, 31, new Color(1f, 1f, 1f, 0.58f));

        FillEllipse(texture, 95, 558, 140, 48, new Color(0.24f, 0.58f, 0.23f, 0.86f));
        FillEllipse(texture, 210, 538, 150, 48, new Color(0.18f, 0.50f, 0.20f, 0.82f));
        FillEllipse(texture, 330, 520, 120, 42, new Color(0.22f, 0.57f, 0.23f, 0.76f));

        for (int y = 0; y < 260; y++)
        {
            for (int x = 0; x < 560; x++)
            {
                if (y > 260f - (x * 0.34f))
                    continue;

                float plank = Mathf.Floor(x / 72f) % 2f;
                float shade = plank > 0f ? 0.04f : -0.02f;
                Color wood = new Color(0.70f + shade, 0.47f + shade, 0.27f + shade);
                if (Mathf.Abs((x % 72) - 2) < 2)
                    wood = new Color(0.36f, 0.23f, 0.15f);
                if (Mathf.Abs((y + x * 0.19f) % 46f) < 2f)
                    wood = Color.Lerp(wood, new Color(0.31f, 0.20f, 0.12f), 0.55f);

                texture.SetPixel(x, y, wood);
            }
        }

        for (int x = 0; x < width; x++)
        {
            int y = Mathf.RoundToInt(438f + Mathf.Sin(x * 0.02f) * 2f);
            DrawLine(texture, x, y, x, y + 1, new Color(1f, 1f, 1f, 0.22f));
        }

        texture.Apply();
    }

    private static void DrawButtonFrame(Texture2D texture)
    {
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        FillRoundedRect(texture, 10, 8, 620, 204, 34, new Color(0.55f, 0.30f, 0.12f, 1f));
        FillRoundedRect(texture, 22, 20, 596, 180, 27, new Color(0.83f, 0.55f, 0.27f, 1f));
        FillRoundedRect(texture, 38, 36, 564, 148, 21, new Color(0.91f, 0.88f, 0.80f, 0.95f));
        FillRoundedRect(texture, 52, 48, 536, 124, 16, new Color(0.78f, 0.89f, 0.91f, 0.55f));

        for (int y = 44; y < 176; y++)
        {
            for (int x = 48; x < 592; x++)
            {
                if (!IsInsideRoundedRect(x, y, 52, 48, 536, 124, 16))
                    continue;

                float shine = Mathf.Sin((x * 0.035f) + (y * 0.014f)) * 0.5f + 0.5f;
                Color current = texture.GetPixel(x, y);
                texture.SetPixel(x, y, Color.Lerp(current, Color.white, 0.04f + shine * 0.08f));
            }
        }

        texture.Apply();
    }

    private static void FillRoundedRect(Texture2D texture, int x, int y, int width, int height, int radius, Color color)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
            {
                if (IsInsideRoundedRect(px, py, x, y, width, height, radius))
                    texture.SetPixel(px, py, color);
            }
        }
    }

    private static bool IsInsideRoundedRect(int px, int py, int x, int y, int width, int height, int radius)
    {
        int nearestX = Mathf.Clamp(px, x + radius, x + width - radius);
        int nearestY = Mathf.Clamp(py, y + radius, y + height - radius);
        int dx = px - nearestX;
        int dy = py - nearestY;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static void FillEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        int minX = Mathf.Max(0, centerX - radiusX);
        int maxX = Mathf.Min(texture.width - 1, centerX + radiusX);
        int minY = Mathf.Max(0, centerY - radiusY);
        int maxY = Mathf.Min(texture.height - 1, centerY + radiusY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - centerX) / (float)radiusX;
                float dy = (y - centerY) / (float)radiusY;
                if ((dx * dx) + (dy * dy) > 1f)
                    continue;

                Color baseColor = texture.GetPixel(x, y);
                texture.SetPixel(x, y, Color.Lerp(baseColor, color, color.a));
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            if (x0 >= 0 && x0 < texture.width && y0 >= 0 && y0 < texture.height)
            {
                Color baseColor = texture.GetPixel(x0, y0);
                texture.SetPixel(x0, y0, Color.Lerp(baseColor, color, color.a));
            }

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameplayScenePath, true)
        };

        HashSet<string> includedPaths = new HashSet<string> { MenuScenePath, GameplayScenePath };
        foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
        {
            if (existingScene == null || string.IsNullOrEmpty(existingScene.path))
                continue;

            if (includedPaths.Contains(existingScene.path))
                continue;

            scenes.Add(existingScene);
            includedPaths.Add(existingScene.path);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
