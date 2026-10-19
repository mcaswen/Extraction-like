using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Repeatable import and wiring of the Astron artwork into the existing menu and HUD.</summary>
public static class AstronUiAssetBuilder
{
    const string Root = AstronUiTexturePostprocessor.Root;
    const string MenuPath = "Assets/Scenes/Scene_MainMenu.unity";
    const string HudPath = "Assets/Resources/HUD/Pfb_PlayerStatusHud.prefab";
    const string OverlayPath = "Assets/Prefabs/MapGraph/UI/Pfb_MapGraphOverlay.prefab";

    [MenuItem("Tools/UI/Apply Astron Artwork")]
    public static void Rebuild()
    {
        ImportArtwork();
        var scene = SceneManager.GetSceneByPath(MenuPath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>()).First();
            ApplyMainMenu(canvas);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        ApplyHud();
        ApplyMapArtwork();
        AssetDatabase.SaveAssets();
    }

    public static void ImportArtwork()
    {
        foreach (string path in Directory.GetFiles(Root, "*.png")) Import(path.Replace('\\', '/'));
    }

    public static void ApplyMainMenu(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Expand keeps the entire illustrated composition inside narrower or wider windows.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var background = Find(canvas.transform, "Background").GetComponent<Image>();
        background.sprite = Sprite("开始界面背景.png");
        Center(background.rectTransform, Vector2.zero, new Vector2(1920, 1080));
        var aspect = background.GetComponent<AspectRatioFitter>() ?? background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = 16f / 9;
        SetMenuArtwork(Find(canvas.transform, "GameName").GetComponent<Image>(), "UI-标题.png");
        string[] names = { "Start Game Button", "Settings Button", "Exit Game Button" };
        string[] files = { "UI-开始游戏Button.png", "UI-设置Button.png", "UI-退出游戏Button.png" };
        for (int i = 0; i < names.Length; i++)
        {
            var graphic = Find(canvas.transform, names[i] + " Background").GetComponent<Image>();
            SetMenuArtwork(graphic, files[i]);
            var button = Find(canvas.transform, names[i]).GetComponent<Button>();
            Center((RectTransform)button.transform, graphic.rectTransform.anchoredPosition, graphic.rectTransform.sizeDelta);
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .95f, .76f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.73f, .84f, .94f);
            button.colors = colors;
            foreach (var label in button.GetComponentsInChildren<Text>(true)) label.gameObject.SetActive(false);
        }
    }
    static void SetMenuArtwork(Image image, string filename)
    {
        image.sprite = Sprite(filename); image.color = Color.white; image.raycastTarget = false;
        Rect crop = image.sprite.rect;
        Center(image.rectTransform, crop.center - new Vector2(960, 540), crop.size);
    }

    static void ApplyHud()
    {
        var prefab = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var root = (RectTransform)Find(prefab.transform, "PlayerStatusHudRoot");
            TopLeft(root, new Vector2(24, -24), new Vector2(556, 210));
            Find(root, "Background").gameObject.SetActive(false);
            var avatar = Find(root, "AvatarIcon").GetComponent<Image>();
            avatar.sprite = Sprite("UI-主界面头像.png"); avatar.preserveAspect = true;
            TopLeft(avatar.rectTransform, new Vector2(0, -24), new Vector2(146, 146));
            ApplyBar(root, "HealthBar", "HealthBarProgress", "HealthFill", "HealthValue", "HealthMarkerIcon", "UI-主界面血条.png", 0);
            ApplyBar(root, "CarryLoadBar", "CarryLoadBarProgress", "CarryFill", "CarryValue", "CarryMarkerIcon", "UI-主界面背包容量.png", 68);
            ApplyBar(root, "ExperienceBar", "ExperienceBarProgress", "ExperienceFill", "ExperienceValue", "ExperienceMarkerIcon", "UI-主界面经验值.png", 136);
            PrefabUtility.SaveAsPrefabAsset(prefab, HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }
    static void ApplyBar(Transform root, string rowName, string barName, string fillName, string textName, string markerName, string file, float y)
    {
        var row = (RectTransform)Find(root, rowName);
        TopLeft(row, new Vector2(154, -y), new Vector2(390, 60));
        var bar = Find(row, barName).GetComponent<Image>();
        TopLeft(bar.rectTransform, new Vector2(52, -22), new Vector2(315, 20));
        bar.sprite = null; bar.type = Image.Type.Simple; bar.color = new Color(.035f, .09f, .16f, .97f);
        var fill = Find(bar.transform, fillName).GetComponent<Image>();
        fill.sprite = null; fill.type = Image.Type.Simple;
        fill.rectTransform.localScale = Vector3.one;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
        fill.rectTransform.pivot = new Vector2(0, .5f); fill.rectTransform.anchoredPosition = Vector2.zero; fill.rectTransform.sizeDelta = new Vector2(315, 0);
        var marker = (RectTransform)Find(bar.transform, markerName);
        marker.sizeDelta = Vector2.zero; marker.gameObject.SetActive(false);
        var frame = EnsureImage(row, "AstronFrame"); frame.sprite = Sprite(file); frame.color = Color.white;
        TopLeft(frame.rectTransform, Vector2.zero, new Vector2(390, 390 * frame.sprite.rect.height / frame.sprite.rect.width));
        frame.transform.SetAsLastSibling();
        var label = Find(row, textName).GetComponent<Text>();
        TopLeft(label.rectTransform, new Vector2(54, -20), new Vector2(308, 24));
        label.fontSize = 16; label.resizeTextForBestFit = false; label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
        label.transform.SetAsLastSibling();
        var outline = label.GetComponent<Outline>() ?? label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.015f, .045f, .085f, .95f); outline.effectDistance = new Vector2(1, -1);
    }
    public static void ApplyMapArtwork()
    {
        var prefab = PrefabUtility.LoadPrefabContents(OverlayPath);
        try
        {
            Find(prefab.transform, "PanelBorder").gameObject.SetActive(false);
            var frame = EnsureImage(prefab.transform, "AstronFrame");
            frame.sprite = Sprite("UI_小地图边框.png"); frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 5; frame.fillCenter = false;
            Stretch(frame.rectTransform, new Vector2(-12, -12), new Vector2(12, 12)); frame.transform.SetAsLastSibling();
            var content = Find(prefab.transform, "MapContent");
            Stretch((RectTransform)content, Vector2.zero, Vector2.zero);
            foreach (string label in new[] { "Title", "Hint", "Legend", "Detail" })
                Find(prefab.transform, label).gameObject.SetActive(false);
            var backdrop = EnsureImage(content, "IllustratedMap"); backdrop.sprite = Sprite("小地图背景.png");
            backdrop.color = new Color(1,1,1,.8f); backdrop.transform.SetAsFirstSibling();
            var overlay = new SerializedObject(prefab.GetComponent<Gameplay.MapGraph.View.MapGraphOverlayController>());
            overlay.FindProperty("_illustratedMap").objectReferenceValue = backdrop.rectTransform;
            overlay.FindProperty("_illustratedMapDefinition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Gameplay.MapGraph.Config.SO_MapGraphDefinition>(
                "Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset");
            // Reference artwork registration: graph origin at pixel (571,384), measured from bottom left;
            // five artwork pixels represent six graph units. Keep the full 1247 x 915 image, including sea margins.
            overlay.FindProperty("_illustratedMapBounds").rectValue = new Rect(
                new Vector2(-571, -384) * 1.2f, backdrop.sprite.rect.size * 1.2f);
            overlay.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, OverlayPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var theme = AssetDatabase.LoadAssetAtPath<Gameplay.MapGraph.Config.SO_MapGraphTheme>(MapGraphUguiPrefabFactory.ThemePath);
        theme.Background = new Color(.02f, .075f, .12f, .98f);
        theme.ZoneFill = new Color(.02f, .07f, .11f, .24f);
        theme.Border = new Color(.68f, .83f, .91f, .25f);
        theme.MutedText = new Color(.88f, .92f, .91f);
        theme.Edge = new Color(.65f, .84f, .96f, .85f);
        EditorUtility.SetDirty(theme);
    }
    static Sprite Import(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        var pixels = texture.GetPixels32();
        if (AstronUiTexturePostprocessor.HasWhiteMatte(path)) AstronUiTexturePostprocessor.RemoveExteriorWhite(pixels, texture.width, texture.height);
        int left=texture.width,bottom=texture.height,right=0,top=0;
        for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) if(pixels[y*texture.width+x].a>12)
        { left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y); }
        var rect = Rect.MinMaxRect(Math.Max(0,left-2),Math.Max(0,bottom-2),Math.Min(texture.width,right+3),Math.Min(texture.height,top+3));
        Object.DestroyImmediate(texture);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.spritePixelsPerUnit = 100;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        if (AstronUiTexturePostprocessor.HasWhiteMatte(path))
        {
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(platform);
        }
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        string name = Path.GetFileNameWithoutExtension(path);
        var existing = provider.GetSpriteRects().FirstOrDefault(s => s.name == name);
        var sprite = new SpriteRect { name=name, rect=rect, alignment=SpriteAlignment.Center, pivot=Vector2.one*.5f,
            spriteID=existing != null ? existing.spriteID : GUID.Generate(),
            border=path.EndsWith("UI_小地图边框.png") ? new Vector4(320,340,200,320) : Vector4.zero };
        provider.SetSpriteRects(new[] { sprite });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, sprite.spriteID) });
        provider.Apply();
        importer.SaveAndReimport();
        return Sprite(Path.GetFileName(path));
    }
    static Sprite Sprite(string file) => AssetDatabase.LoadAllAssetsAtPath(Root+file).OfType<Sprite>().First();
    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    static Image EnsureImage(Transform parent, string name)
    {
        var child = parent.Find(name);
        var image = child != null ? child.GetComponent<Image>() : new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent,false); image.raycastTarget=false; image.color=Color.white; return image;
    }
    static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    { rect.localScale=Vector3.one; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size; }
    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    { rect.localScale=Vector3.one; rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f; rect.anchoredPosition=position; rect.sizeDelta=size; }
    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.localScale=Vector3.one; rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=min;rect.offsetMax=max; }
}
