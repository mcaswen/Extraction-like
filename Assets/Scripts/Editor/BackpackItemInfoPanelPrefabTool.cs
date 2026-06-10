using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 构建背包物品信息面板 Prefab 节点。
/// </summary>
public static class BackpackItemInfoPanelPrefabTool
{
    private const string CanvasPrefabPath = "Assets/Prefabs/Canvas.prefab";
    private const string BackgroundSpritePath = "Assets/Art/Sprites/UI/Information_Panel/IMG_0612.PNG";
    private const string MarkerSpritePath = "Assets/Art/Sprites/UI/Information_Panel/IMG_0613.PNG";
    private const string PanelName = "ItemInfoPanel";

    [MenuItem("Tools/Backpack/Rebuild Item Info Panel")]
    public static void RebuildCanvasPrefabItemInfoPanel()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            Sprite markerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerSpritePath);
            if (backgroundSprite == null || markerSprite == null)
            {
                Debug.LogError("Item info panel sprites are missing or not imported as Sprite assets.");
                return;
            }

            Transform panelTransform = prefabRoot.transform.Find(PanelName);
            if (panelTransform != null)
            {
                Object.DestroyImmediate(panelTransform.gameObject);
            }

            GameObject panel = CreateUiObject(PanelName, prefabRoot.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(292f, 350f);
            panelRect.anchoredPosition = new Vector2(760f, -130f);
            panel.SetActive(true);

            Image background = panel.AddComponent<Image>();
            background.sprite = backgroundSprite;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            background.raycastTarget = true;

            InventoryItemInfoPanelController controller = panel.AddComponent<InventoryItemInfoPanelController>();
            controller.BackgroundImage = background;
            controller.PreferredOffset = new Vector2(12f, 0f);
            controller.ScreenPadding = new Vector2(16f, 16f);

            Image marker = CreateImage("InfoMarker", panel.transform, markerSprite);
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0.5f, 1f);
            markerRect.anchorMax = new Vector2(0.5f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.75f);
            markerRect.sizeDelta = new Vector2(54f, 47f);
            markerRect.anchoredPosition = new Vector2(0f, 2f);
            marker.raycastTarget = false;
            controller.MarkerImage = marker;

            Image iconFrame = CreateSolidImage("ItemIconFrame", panel.transform, new Color(0.08f, 0.1f, 0.1f, 0.62f));
            RectTransform iconFrameRect = iconFrame.rectTransform;
            iconFrameRect.anchorMin = new Vector2(0f, 1f);
            iconFrameRect.anchorMax = new Vector2(0f, 1f);
            iconFrameRect.pivot = new Vector2(0f, 1f);
            iconFrameRect.sizeDelta = new Vector2(74f, 74f);
            iconFrameRect.anchoredPosition = new Vector2(28f, -44f);
            iconFrame.raycastTarget = false;

            Image icon = CreateSolidImage("ItemIcon", iconFrame.transform, Color.clear);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.08f, 0.08f);
            iconRect.anchorMax = new Vector2(0.92f, 0.92f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            controller.ItemIconImage = icon;

            Text nameText = CreateText("NameText", panel.transform, 24, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(118f, -92f);
            nameRect.offsetMax = new Vector2(-28f, -44f);
            nameText.color = new Color(0.94f, 1f, 0.97f, 1f);
            controller.NameText = nameText;

            Text rarityText = CreateText("RarityText", panel.transform, 16, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform rarityRect = rarityText.rectTransform;
            rarityRect.anchorMin = new Vector2(0f, 1f);
            rarityRect.anchorMax = new Vector2(1f, 1f);
            rarityRect.pivot = new Vector2(0f, 1f);
            rarityRect.offsetMin = new Vector2(118f, -122f);
            rarityRect.offsetMax = new Vector2(-28f, -96f);
            controller.RarityText = rarityText;

            Image divider = CreateSolidImage("Divider", panel.transform, new Color(0.67f, 0.95f, 0.86f, 0.42f));
            RectTransform dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.offsetMin = new Vector2(28f, -138f);
            dividerRect.offsetMax = new Vector2(-28f, -136f);
            divider.raycastTarget = false;

            Text detailText = CreateText("DetailText", panel.transform, 15, FontStyle.Normal, TextAnchor.UpperLeft);
            RectTransform detailRect = detailText.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.pivot = new Vector2(0f, 1f);
            detailRect.offsetMin = new Vector2(28f, 76f);
            detailRect.offsetMax = new Vector2(-28f, -152f);
            detailText.color = new Color(0.9f, 0.96f, 0.92f, 1f);
            detailText.lineSpacing = 1.05f;
            controller.DetailText = detailText;

            Button splitButton = CreateButton("SplitButton", panel.transform, "拆分", new Vector2(28f, 28f), new Vector2(116f, 38f));
            controller.SplitButton = splitButton;

            Button closeButton = CreateButton("CloseButton", panel.transform, "关闭", new Vector2(148f, 28f), new Vector2(116f, 38f));
            controller.CloseButton = closeButton;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CanvasPrefabPath);
            Debug.Log("Rebuilt backpack item info panel on Canvas.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        return image;
    }

    private static Image CreateSolidImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject gameObject = CreateUiObject(name, parent);
        Text text = gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        Image background = CreateSolidImage(name, parent, new Color(0.11f, 0.16f, 0.15f, 0.86f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Button button = background.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.11f, 0.16f, 0.15f, 0.86f);
        colors.highlightedColor = new Color(0.2f, 0.34f, 0.3f, 0.95f);
        colors.pressedColor = new Color(0.08f, 0.12f, 0.11f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Label", background.transform, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;
        text.color = new Color(0.9f, 1f, 0.96f, 1f);

        return button;
    }
}
