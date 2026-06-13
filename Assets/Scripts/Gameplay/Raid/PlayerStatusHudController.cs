using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime screen-space status HUD for player health and carry load.
/// </summary>
public sealed class PlayerStatusHudController : MonoBehaviour
{
    private const string RuntimeObjectName = "PlayerStatusHudController";
    private const string CanvasObjectName = "PlayerStatusHudCanvas";
    private const string SpriteSetResourcePath = "HUD/PlayerStatusHudSpriteSet";

    private static PlayerStatusHudController _instance;
    private static PlayerStatusHudSpriteSet _spriteSet;
    private static Sprite _whiteSprite;
    private static Font _defaultFont;

    [SerializeField] private Sprite _avatarIconSprite;
    [SerializeField] private Sprite _healthIconSprite;
    [SerializeField] private Sprite _carryIconSprite;
    [SerializeField] private Sprite _progressBarSprite;
    [SerializeField] private Rect _avatarCropRect;

    public Vector2 AnchorPosition = new Vector2(32f, -32f);
    public Vector2 AvatarSize = new Vector2(108f, 108f);
    public Vector2 StatusIconSize = new Vector2(24f, 24f);
    public Vector2 BarSize = new Vector2(380f, 24f);
    public Vector2 BarFillPadding = Vector2.zero;
    public float BarSpacing = 8f;
    public float AvatarBarGap = 16f;
    public float IconBarGap = 0f;
    public float BarsTopInset = 8f;
    public float ValueBarGap = 2f;
    public float ValueRowHeight = 14f;

    public Color PanelColor = new Color(0.18f, 0.18f, 0.18f, 0.92f);
    public Color HealthFillColor = new Color(0.36f, 0.78f, 0.48f, 1f);
    public Color CarryFillColor = new Color(0.95f, 0.7f, 0.22f, 1f);
    public Color CarryWarningFillColor = new Color(0.96f, 0.48f, 0.18f, 1f);
    public Color CarryOverloadFillColor = new Color(0.92f, 0.22f, 0.2f, 1f);
    public Color ValueTextColor = new Color(0.94f, 0.94f, 0.94f, 1f);
    public int ValueTextFontSize = 11;

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _healthMarkerRect;
    private RectTransform _carryMarkerRect;
    private Image _healthFillImage;
    private Image _carryFillImage;
    private Text _healthText;
    private Text _carryText;
    private Sprite _avatarDisplaySprite;

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

        if (_avatarDisplaySprite != null)
        {
            Destroy(_avatarDisplaySprite);
            _avatarDisplaySprite = null;
        }
    }

    private void EnsureUi()
    {
        if (_canvas != null && _root != null)
        {
            return;
        }

        EnsureSharedResources();
        ApplySpriteSet();

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
        AdaptiveCanvasScaler.Configure(scaler, new Vector2(1920f, 1080f));

        _root = CreateRoot(canvasObject.transform);
        CreateAvatarIcon(_root);

        float rowHeight = GetStatusRowHeight();
        float healthY = -BarsTopInset;
        float carryY = -(BarsTopInset + rowHeight + BarSpacing);
        CreateStatusBar(_root, "HealthBar", healthY, _healthIconSprite, HealthFillColor, out _healthFillImage, out _healthText, out _healthMarkerRect);
        CreateStatusBar(_root, "CarryLoadBar", carryY, _carryIconSprite, CarryFillColor, out _carryFillImage, out _carryText, out _carryMarkerRect);
        RefreshBars();
    }

    private RectTransform CreateRoot(Transform parent)
    {
        float rowHeight = GetStatusRowHeight();
        float barsHeight = BarsTopInset + rowHeight * 2f + BarSpacing;
        float rootHeight = Mathf.Max(AvatarSize.y, barsHeight);
        float rootWidth = AvatarSize.x + AvatarBarGap + BarSize.x;

        GameObject rootObject = new GameObject("PlayerStatusHudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);

        RectTransform rectTransform = rootObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = AnchorPosition;
        rectTransform.sizeDelta = new Vector2(rootWidth, rootHeight);
        return rectTransform;
    }

    private void CreateAvatarIcon(RectTransform parent)
    {
        GameObject iconObject = new GameObject("AvatarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(parent, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = AvatarSize;

        Sprite avatarSprite = GetAvatarDisplaySprite();
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = avatarSprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = avatarSprite != null;
    }

    private void CreateStatusBar(
        RectTransform parent,
        string name,
        float yOffset,
        Sprite statusIcon,
        Color fillColor,
        out Image fillImage,
        out Text label,
        out RectTransform markerRect)
    {
        float rowHeight = GetStatusRowHeight();
        float rowWidth = BarSize.x;

        GameObject rowObject = new GameObject(name, typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(AvatarSize.x + AvatarBarGap, yOffset);
        rowRect.sizeDelta = new Vector2(rowWidth, rowHeight);

        GameObject textObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(rowRect, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, ValueRowHeight);

        label = textObject.GetComponent<Text>();
        label.font = _defaultFont;
        label.fontSize = ValueTextFontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = ValueTextColor;
        label.raycastTarget = false;
        label.supportRichText = false;

        RectTransform barRect = CreateProgressBar(rowRect, name);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(barRect, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(
            Mathf.Clamp(BarFillPadding.x, 0f, BarSize.x * 0.45f),
            Mathf.Clamp(BarFillPadding.y, 0f, BarSize.y * 0.45f));
        fillRect.offsetMax = new Vector2(
            Mathf.Clamp(BarFillPadding.x, 0f, BarSize.x * 0.45f) + BarSize.x,
            -Mathf.Clamp(BarFillPadding.y, 0f, BarSize.y * 0.45f));

        fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = _whiteSprite;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Simple;

        GameObject iconObject = new GameObject("MarkerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(barRect, false);

        markerRect = iconObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(StatusIconSize.x * 0.5f, 0f);
        markerRect.sizeDelta = StatusIconSize;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = statusIcon;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = statusIcon != null;
    }

    private RectTransform CreateProgressBar(RectTransform parent, string name)
    {
        GameObject barObject = new GameObject($"{name}Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        barObject.transform.SetParent(parent, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(0f, -(ValueRowHeight + ValueBarGap));
        barRect.sizeDelta = BarSize;

        Image backgroundImage = barObject.GetComponent<Image>();
        backgroundImage.sprite = _whiteSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = PanelColor;
        backgroundImage.raycastTarget = false;
        return barRect;
    }

    private float GetStatusRowHeight()
    {
        return ValueRowHeight + ValueBarGap + Mathf.Max(StatusIconSize.y, BarSize.y);
    }

    private void RefreshBars()
    {
        bool hasFocusedAgent = TryGetFocusedAgent(out IAgentReadOnly focusedAgent);
        PlayerHealthController healthController = null;
        if (!hasFocusedAgent)
        {
            healthController = PlayerHealthController.Instance;
            if (healthController == null)
            {
                healthController = FindObjectOfType<PlayerHealthController>();
            }
        }

        if (_root != null)
        {
            _root.gameObject.SetActive(hasFocusedAgent || healthController != null);
        }

        if (!hasFocusedAgent && healthController == null)
        {
            return;
        }

        float maxHealth = hasFocusedAgent
            ? Mathf.Max(1f, focusedAgent.MaxHealth)
            : Mathf.Max(1f, healthController.MaxHealth);
        float currentHealth = hasFocusedAgent
            ? Mathf.Clamp(focusedAgent.CurrentHealth, 0f, maxHealth)
            : Mathf.Clamp(healthController.CurrentHealth, 0f, maxHealth);
        string healthLabel = hasFocusedAgent && !string.IsNullOrWhiteSpace(focusedAgent.AgentIdValue)
            ? $"{focusedAgent.AgentIdValue}  {currentHealth:0} / {maxHealth:0}  HP"
            : $"{currentHealth:0} / {maxHealth:0}  HP";
        SetBar(_healthFillImage, _healthText, _healthMarkerRect, BarSize, currentHealth / maxHealth, healthLabel);

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
            _carryMarkerRect,
            BarSize,
            carryRatio,
            $"{currentCarryWeight:0.#} / {maxCarryWeight:0.#}  KG");
    }

    private static bool TryGetFocusedAgent(out IAgentReadOnly focusedAgent)
    {
        focusedAgent = null;
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || !registry.TryGetFocusedHandle(out AgentRuntimeHandle focusedHandle))
        {
            return false;
        }

        focusedAgent = focusedHandle.ReadOnly;
        return focusedAgent != null;
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

    private void ApplySpriteSet()
    {
        if (_spriteSet == null)
        {
            _spriteSet = Resources.Load<PlayerStatusHudSpriteSet>(SpriteSetResourcePath);
        }

        if (_spriteSet == null)
        {
            return;
        }

        if (_avatarIconSprite == null)
        {
            _avatarIconSprite = _spriteSet.AvatarIcon;
        }

        if (_healthIconSprite == null)
        {
            _healthIconSprite = _spriteSet.HealthIcon;
        }

        if (_carryIconSprite == null)
        {
            _carryIconSprite = _spriteSet.CarryIcon;
        }

        if (_progressBarSprite == null)
        {
            _progressBarSprite = _spriteSet.ProgressBar;
        }

        if (_avatarCropRect.width <= 0f || _avatarCropRect.height <= 0f)
        {
            _avatarCropRect = _spriteSet.AvatarCropRect;
        }
    }

    private Sprite GetAvatarDisplaySprite()
    {
        if (_avatarIconSprite == null)
        {
            return null;
        }

        if (_avatarCropRect.width <= 0f || _avatarCropRect.height <= 0f)
        {
            return _avatarIconSprite;
        }

        if (_avatarDisplaySprite != null)
        {
            return _avatarDisplaySprite;
        }

        Rect textureRect = _avatarIconSprite.textureRect;
        Rect cropRect = ClampRectToTexture(_avatarCropRect, textureRect);
        if (cropRect.width <= 0f || cropRect.height <= 0f)
        {
            return _avatarIconSprite;
        }

        _avatarDisplaySprite = Sprite.Create(
            _avatarIconSprite.texture,
            cropRect,
            new Vector2(0.5f, 0.5f),
            _avatarIconSprite.pixelsPerUnit);
        return _avatarDisplaySprite;
    }

    private static Rect ClampRectToTexture(Rect cropRect, Rect textureRect)
    {
        float minX = Mathf.Clamp(cropRect.xMin, textureRect.xMin, textureRect.xMax);
        float minY = Mathf.Clamp(cropRect.yMin, textureRect.yMin, textureRect.yMax);
        float maxX = Mathf.Clamp(cropRect.xMax, textureRect.xMin, textureRect.xMax);
        float maxY = Mathf.Clamp(cropRect.yMax, textureRect.yMin, textureRect.yMax);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void SetBar(Image fillImage, Text label, RectTransform markerRect, Vector2 barSize, float ratio, string text)
    {
        float clampedRatio = Mathf.Clamp01(ratio);

        if (fillImage != null)
        {
            RectTransform fillRect = fillImage.rectTransform;
            float minimumFillWidth = markerRect != null ? markerRect.rect.width * 0.5f : 0f;
            float fillWidth = Mathf.Max(minimumFillWidth, barSize.x * clampedRatio);
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
        }

        if (label != null)
        {
            label.text = text;
        }

        if (markerRect != null)
        {
            float halfWidth = Mathf.Max(0f, markerRect.rect.width * 0.5f);
            float targetX = Mathf.Clamp(barSize.x * clampedRatio + halfWidth, halfWidth, barSize.x - halfWidth);
            markerRect.anchoredPosition = new Vector2(targetX, 0f);
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
