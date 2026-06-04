using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime screen-space bars for player health and carry load.
/// </summary>
public sealed class PlayerStatusHudController : MonoBehaviour
{
    private const string RuntimeObjectName = "PlayerStatusHudController";
    private const string CanvasObjectName = "PlayerStatusHudCanvas";

    private static PlayerStatusHudController _instance;
    private static Sprite _whiteSprite;
    private static Font _defaultFont;

    public Vector2 AnchorPosition = new Vector2(16f, -16f);
    public Vector2 BarSize = new Vector2(280f, 18f);
    public float BarSpacing = 8f;

    public Color PanelColor = new Color(0.02f, 0.04f, 0.06f, 0.72f);
    public Color HealthFillColor = new Color(0.86f, 0.18f, 0.18f, 1f);
    public Color CarryFillColor = new Color(0.18f, 0.74f, 0.94f, 1f);
    public Color CarryWarningFillColor = new Color(0.95f, 0.67f, 0.18f, 1f);
    public Color CarryOverloadFillColor = new Color(0.92f, 0.22f, 0.2f, 1f);

    private Canvas _canvas;
    private RectTransform _root;
    private Image _healthFillImage;
    private Image _carryFillImage;
    private Text _healthText;
    private Text _carryText;

    public static PlayerStatusHudController EnsureRuntimeInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        _instance = FindObjectOfType<PlayerStatusHudController>();
        if (_instance != null)
        {
            return _instance;
        }

        GameObject controllerObject = new GameObject(RuntimeObjectName);
        _instance = controllerObject.AddComponent<PlayerStatusHudController>();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureUi();
    }

    private void LateUpdate()
    {
        EnsureUi();
        RefreshBars();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void EnsureUi()
    {
        if (_canvas != null && _root != null)
        {
            return;
        }

        EnsureSharedResources();

        GameObject canvasObject = new GameObject(
            CanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 260;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = CreateRoot(canvasObject.transform);
        CreateStatusBar(_root, "HealthBar", 0f, HealthFillColor, out _healthFillImage, out _healthText);
        CreateStatusBar(_root, "CarryLoadBar", -(BarSize.y + BarSpacing), CarryFillColor, out _carryFillImage, out _carryText);
        RefreshBars();
    }

    private RectTransform CreateRoot(Transform parent)
    {
        float rootHeight = BarSize.y * 2f + BarSpacing;
        GameObject rootObject = new GameObject("PlayerStatusHudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);

        RectTransform rectTransform = rootObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = AnchorPosition;
        rectTransform.sizeDelta = new Vector2(BarSize.x, rootHeight);
        return rectTransform;
    }

    private void CreateStatusBar(
        RectTransform parent,
        string name,
        float yOffset,
        Color fillColor,
        out Image fillImage,
        out Text label)
    {
        GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(0f, yOffset);
        barRect.sizeDelta = BarSize;

        Image backgroundImage = barObject.GetComponent<Image>();
        backgroundImage.sprite = _whiteSprite;
        backgroundImage.color = PanelColor;
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(barRect, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = _whiteSprite;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(barRect, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        label = textObject.GetComponent<Text>();
        label.font = _defaultFont;
        label.fontSize = 13;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private void RefreshBars()
    {
        PlayerHealthController healthController = PlayerHealthController.Instance;
        if (healthController == null)
        {
            healthController = FindObjectOfType<PlayerHealthController>();
        }

        if (_root != null)
        {
            _root.gameObject.SetActive(healthController != null);
        }

        if (healthController == null)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, healthController.MaxHealth);
        float currentHealth = Mathf.Clamp(healthController.CurrentHealth, 0f, maxHealth);
        SetBar(_healthFillImage, _healthText, currentHealth / maxHealth, $"生命 {currentHealth:0}/{maxHealth:0}");

        InventoryScreenController inventory = InventoryScreenController.Instance;
        float currentCarryWeight = inventory != null ? inventory.GetCurrentCarryWeight() : 0f;
        float maxCarryWeight = inventory != null ? inventory.GetMaxCarryWeight() : 1f;
        float carryRatio = currentCarryWeight / Mathf.Max(0.1f, maxCarryWeight);

        if (_carryFillImage != null)
        {
            _carryFillImage.color = GetCarryFillColor(carryRatio);
        }

        SetBar(
            _carryFillImage,
            _carryText,
            carryRatio,
            $"负重 {currentCarryWeight:0.#}/{maxCarryWeight:0.#}");
    }

    private Color GetCarryFillColor(float carryRatio)
    {
        if (carryRatio >= 1f)
        {
            return CarryOverloadFillColor;
        }

        if (carryRatio >= 0.75f)
        {
            return CarryWarningFillColor;
        }

        return CarryFillColor;
    }

    private static void SetBar(Image fillImage, Text label, float ratio, string text)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        if (label != null)
        {
            label.text = text;
        }
    }

    private static void EnsureSharedResources()
    {
        if (_whiteSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        if (_defaultFont == null)
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
