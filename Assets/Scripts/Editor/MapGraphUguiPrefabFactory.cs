using System;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建地图图界面相关预制体的编辑器工厂
/// </summary>
public static class MapGraphUguiPrefabFactory
{
    private const string OutputFolder = "Assets/Prefabs/MapGraph/UI";
    private const string NodePrefabPath = OutputFolder + "/Pfb_MapGraphNodeView.prefab";
    private const string EdgePrefabPath = OutputFolder + "/Pfb_MapGraphEdgeView.prefab";
    private const string AgentPrefabPath = OutputFolder + "/Pfb_MapGraphAgentView.prefab";
    private const string OverlayPrefabPath = OutputFolder + "/Pfb_MapGraphOverlay.prefab";

    private const string BoardNodePrefabPath = "Assets/Prefabs/BoardGame/View/Pfb_NodeView.prefab";
    private const string BoardAgentPrefabPath = "Assets/Prefabs/BoardGame/View/Pfb_AgentView.prefab";

    [InitializeOnLoadMethod]
    private static void EnsurePrefabsExistAfterEditorLoad()
    {
        EditorApplication.delayCall += CreateMissingPrefabsAfterEditorLoad;
    }

    /// <summary>
    /// 生成地图图画布、面板、节点图标和连线预制体
    /// </summary>
    [MenuItem("Tools/Map Graph/Create UGUI Prefabs")]
    public static void CreateUguiPrefabs()
    {
        CreateUguiPrefabs(selectOutput: true);
    }

    private static void CreateMissingPrefabsAfterEditorLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath) != null)
            return;

        CreateUguiPrefabs(selectOutput: false);
    }

    private static void CreateUguiPrefabs(bool selectOutput)
    {
        EnsureAssetFolder(OutputFolder);

        Sprite builtInSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite nodeSprite = LoadSpriteFromPrefab(BoardNodePrefabPath, "IconRenderer") ?? builtInSprite;
        Sprite progressSprite = LoadSpriteFromPrefab(BoardNodePrefabPath, "ProgressFill") ?? builtInSprite;
        Sprite agentSprite = LoadSpriteFromPrefab(BoardAgentPrefabPath, null) ?? nodeSprite;

        MapGraphNodeView nodePrefab = CreateNodePrefab(nodeSprite, progressSprite);
        MapGraphEdgeView edgePrefab = CreateEdgePrefab(builtInSprite);
        MapGraphAgentView agentPrefab = CreateAgentPrefab(agentSprite, nodeSprite);
        GameObject overlayPrefab = CreateOverlayPrefab(nodePrefab, edgePrefab, agentPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectOutput && overlayPrefab != null)
        {
            Selection.activeObject = overlayPrefab;
            EditorGUIUtility.PingObject(overlayPrefab);
        }
    }

    private static MapGraphNodeView CreateNodePrefab(Sprite nodeSprite, Sprite progressSprite)
    {
        GameObject root = CreateUiObject(
            "Pfb_MapGraphNodeView",
            null,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(MapGraphNodeView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        ConfigureCenteredRect(rootRect, Vector2.zero, new Vector2(42f, 42f));

        Image bodyImage = root.GetComponent<Image>();
        ConfigureImage(bodyImage, nodeSprite, Color.white, false);
        bodyImage.preserveAspect = true;

        Image targetRingImage = CreateImageChild(
            root.transform,
            "TargetRing",
            nodeSprite,
            new Color(1f, 0.78f, 0.18f, 0.36f),
            new Vector2(56f, 56f));
        Image occupiedRingImage = CreateImageChild(
            root.transform,
            "OccupiedRing",
            nodeSprite,
            new Color(0.24f, 0.78f, 1f, 0.32f),
            new Vector2(50f, 50f));
        Image statusOverlayImage = CreateImageChild(
            root.transform,
            "StatusOverlay",
            nodeSprite,
            new Color(0.08f, 0.08f, 0.08f, 0.46f),
            new Vector2(42f, 42f));
        Image iconImage = CreateImageChild(
            root.transform,
            "Icon",
            nodeSprite,
            Color.white,
            new Vector2(30f, 30f));
        Image progressBackgroundImage = CreateImageChild(
            root.transform,
            "ProgressBackground",
            progressSprite,
            new Color(0.03f, 0.04f, 0.06f, 0.72f),
            new Vector2(52f, 6f));
        Image progressFillImage = CreateImageChild(
            root.transform,
            "ProgressFill",
            progressSprite,
            new Color(0.24f, 0.82f, 0.35f, 0.95f),
            new Vector2(52f, 6f));
        TMP_Text labelText = CreateLabelChild(
            root.transform,
            "Label",
            "Node",
            new Vector2(0f, -33f),
            new Vector2(112f, 28f),
            11f,
            new Color(0.93f, 0.97f, 1f, 0.96f));

        progressBackgroundImage.rectTransform.anchoredPosition = new Vector2(0f, 28f);
        progressFillImage.rectTransform.anchoredPosition = new Vector2(0f, 28f);
        progressFillImage.type = Image.Type.Filled;
        progressFillImage.fillMethod = Image.FillMethod.Horizontal;
        progressFillImage.fillOrigin = 0;
        progressFillImage.fillAmount = 0f;

        targetRingImage.gameObject.SetActive(false);
        occupiedRingImage.gameObject.SetActive(false);
        statusOverlayImage.gameObject.SetActive(false);
        progressBackgroundImage.gameObject.SetActive(false);
        progressFillImage.gameObject.SetActive(false);

        MapGraphNodeView view = root.GetComponent<MapGraphNodeView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_bodyImage").objectReferenceValue = bodyImage;
        serializedView.FindProperty("_iconImage").objectReferenceValue = iconImage;
        serializedView.FindProperty("_statusOverlayImage").objectReferenceValue = statusOverlayImage;
        serializedView.FindProperty("_targetRingImage").objectReferenceValue = targetRingImage;
        serializedView.FindProperty("_occupiedRingImage").objectReferenceValue = occupiedRingImage;
        serializedView.FindProperty("_progressBackgroundImage").objectReferenceValue = progressBackgroundImage;
        serializedView.FindProperty("_progressFillImage").objectReferenceValue = progressFillImage;
        serializedView.FindProperty("_labelText").objectReferenceValue = labelText;
        serializedView.FindProperty("_fallbackSprite").objectReferenceValue = nodeSprite;
        serializedView.FindProperty("_defaultSize").vector2Value = new Vector2(42f, 42f);
        serializedView.FindProperty("_labelSize").vector2Value = new Vector2(112f, 28f);
        serializedView.FindProperty("_progressSize").vector2Value = new Vector2(52f, 6f);
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefabAndLoadComponent<MapGraphNodeView>(root, NodePrefabPath);
    }

    private static MapGraphEdgeView CreateEdgePrefab(Sprite builtInSprite)
    {
        GameObject root = CreateUiObject(
            "Pfb_MapGraphEdgeView",
            null,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(MapGraphEdgeView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        ConfigureCenteredRect(rootRect, Vector2.zero, new Vector2(160f, 4f));

        Image lineImage = root.GetComponent<Image>();
        ConfigureImage(lineImage, builtInSprite, new Color(0.38f, 0.48f, 0.58f, 0.58f), false);

        MapGraphEdgeView view = root.GetComponent<MapGraphEdgeView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_lineImage").objectReferenceValue = lineImage;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefabAndLoadComponent<MapGraphEdgeView>(root, EdgePrefabPath);
    }

    private static MapGraphAgentView CreateAgentPrefab(Sprite agentSprite, Sprite ringSprite)
    {
        GameObject root = CreateUiObject(
            "Pfb_MapGraphAgentView",
            null,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(MapGraphAgentView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        ConfigureCenteredRect(rootRect, Vector2.zero, new Vector2(28f, 28f));

        Image bodyImage = root.GetComponent<Image>();
        ConfigureImage(bodyImage, agentSprite, Color.white, false);
        bodyImage.preserveAspect = true;

        Image selectionRingImage = CreateImageChild(
            root.transform,
            "SelectionRing",
            ringSprite,
            new Color(0.22f, 0.62f, 1f, 0.36f),
            new Vector2(38f, 38f));
        TMP_Text labelText = CreateLabelChild(
            root.transform,
            "Label",
            "Agent",
            new Vector2(0f, 20f),
            new Vector2(112f, 24f),
            10f,
            new Color(0.95f, 0.98f, 1f, 0.96f));

        selectionRingImage.gameObject.SetActive(false);

        MapGraphAgentView view = root.GetComponent<MapGraphAgentView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_bodyImage").objectReferenceValue = bodyImage;
        serializedView.FindProperty("_selectionRingImage").objectReferenceValue = selectionRingImage;
        serializedView.FindProperty("_labelText").objectReferenceValue = labelText;
        serializedView.FindProperty("_defaultSize").vector2Value = new Vector2(28f, 28f);
        serializedView.FindProperty("_labelSize").vector2Value = new Vector2(112f, 24f);
        serializedView.FindProperty("_selectionRingColor").colorValue = new Color(0.22f, 0.62f, 1f, 0.36f);
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefabAndLoadComponent<MapGraphAgentView>(root, AgentPrefabPath);
    }

    private static GameObject CreateOverlayPrefab(
        MapGraphNodeView nodePrefab,
        MapGraphEdgeView edgePrefab,
        MapGraphAgentView agentPrefab)
    {
        GameObject root = CreateUiObject(
            "Pfb_MapGraphOverlay",
            null,
            typeof(CanvasGroup),
            typeof(AgentGraphProjectionController),
            typeof(MapGraphOverlayController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        ConfigureCenteredRect(rootRect, Vector2.zero, new Vector2(860f, 560f));

        Image backgroundImage = CreateImageChild(
            root.transform,
            "Background",
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            new Color(0.025f, 0.035f, 0.05f, 0.88f),
            Vector2.zero);
        StretchToParent(backgroundImage.rectTransform);
        backgroundImage.raycastTarget = true;

        RectTransform edgesRoot = CreateRootChild(root.transform, "EdgesRoot");
        RectTransform nodesRoot = CreateRootChild(root.transform, "NodesRoot");
        RectTransform agentsRoot = CreateRootChild(root.transform, "AgentsRoot");

        MapGraphOverlayController overlay = root.GetComponent<MapGraphOverlayController>();
        AgentGraphProjectionController projectionController = root.GetComponent<AgentGraphProjectionController>();
        SerializedObject serializedOverlay = new SerializedObject(overlay);
        serializedOverlay.FindProperty("_overlayRoot").objectReferenceValue = rootRect;
        serializedOverlay.FindProperty("_backgroundImage").objectReferenceValue = backgroundImage;
        serializedOverlay.FindProperty("_edgesRoot").objectReferenceValue = edgesRoot;
        serializedOverlay.FindProperty("_nodesRoot").objectReferenceValue = nodesRoot;
        serializedOverlay.FindProperty("_agentsRoot").objectReferenceValue = agentsRoot;
        serializedOverlay.FindProperty("_projectionController").objectReferenceValue = projectionController;
        serializedOverlay.FindProperty("_nodeViewPrefab").objectReferenceValue = nodePrefab;
        serializedOverlay.FindProperty("_edgeViewPrefab").objectReferenceValue = edgePrefab;
        serializedOverlay.FindProperty("_agentViewPrefab").objectReferenceValue = agentPrefab;
        serializedOverlay.FindProperty("_createDefaultBackground").boolValue = true;
        serializedOverlay.FindProperty("_defaultPanelSize").vector2Value = new Vector2(860f, 560f);
        serializedOverlay.FindProperty("_mapScale").vector2Value = new Vector2(48f, 48f);
        serializedOverlay.FindProperty("_edgeWidth").floatValue = 4f;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefabAndLoad(root, OverlayPrefabPath);
    }

    private static Image CreateImageChild(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        Vector2 size)
    {
        GameObject child = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        ConfigureCenteredRect(rectTransform, Vector2.zero, size);

        Image image = child.GetComponent<Image>();
        ConfigureImage(image, sprite, color, false);
        return image;
    }

    private static TMP_Text CreateLabelChild(
        Transform parent,
        string name,
        string text,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color)
    {
        GameObject child = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        ConfigureCenteredRect(rectTransform, position, size);

        TMP_Text label = child.GetComponent<TMP_Text>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return label;
    }

    private static RectTransform CreateRootChild(Transform parent, string name)
    {
        GameObject child = CreateUiObject(name, parent);
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        return rectTransform;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
    {
        Type[] components = new Type[extraComponents.Length + 1];
        components[0] = typeof(RectTransform);
        for (int i = 0; i < extraComponents.Length; i++)
            components[i + 1] = extraComponents[i];

        GameObject gameObject = new GameObject(name, components);
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            gameObject.layer = uiLayer;

        if (parent != null)
            gameObject.transform.SetParent(parent, false);

        return gameObject;
    }

    private static void ConfigureImage(Image image, Sprite sprite, Color color, bool raycastTarget)
    {
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        image.type = Image.Type.Simple;
    }

    private static void ConfigureCenteredRect(RectTransform rectTransform, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static T SavePrefabAndLoadComponent<T>(GameObject root, string path)
        where T : Component
    {
        GameObject prefab = SavePrefabAndLoad(root, path);
        return prefab != null ? prefab.GetComponent<T>() : null;
    }

    private static GameObject SavePrefabAndLoad(GameObject root, string path)
    {
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return savedPrefab != null
            ? savedPrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Sprite LoadSpriteFromPrefab(string prefabPath, string childName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return null;

        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        Sprite firstSprite = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            if (firstSprite == null)
                firstSprite = renderer.sprite;

            if (string.IsNullOrWhiteSpace(childName) || renderer.name == childName)
                return renderer.sprite;
        }

        return firstSprite;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string normalizedFolder = assetFolder.Replace("\\", "/");
        string[] parts = normalizedFolder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            return;

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
