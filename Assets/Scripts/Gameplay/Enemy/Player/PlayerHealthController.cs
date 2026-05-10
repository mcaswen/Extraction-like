using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player health, death and status effect controller.
/// </summary>
public class PlayerHealthController : MonoBehaviour
{
    public static PlayerHealthController Instance { get; private set; }

    [Header("Core Stats")]
    public float MaxHealth = 220f;

    [Min(1f)]
    public float MinimumPlaytestHealth = 220f;

    [Header("Damage Tuning")]
    [Range(0.05f, 1f)]
    public float MinimumDamageTakenMultiplier = 0.2f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    [Header("Status Effect")]
    public Color CorrosionTintColor = new Color(0.45f, 1f, 0.55f, 1f);
    public float CorrosionTintStrength = 0.45f;

    [Header("Health UI")]
    public Image HealthFillImage;
    public Vector3 RuntimeHealthBarOffset = new Vector3(0f, 2.15f, 0f);
    public Vector2 RuntimeHealthBarSize = new Vector2(120f, 16f);
    public Color RuntimeHealthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color RuntimeHealthBarFillColor = new Color(0.16f, 0.86f, 0.24f, 1f);

    private float _corrosionDurationRemaining;
    private float _corrosionTickTimer;
    private float _corrosionTickInterval = 0.25f;
    private float _corrosionDamagePerTick;
    private Renderer[] _cachedRenderers;
    private Color[] _originalRendererColors;
    private float _baseMaxHealth;
    private float _damageTakenMultiplier = 1f;
    private Canvas _runtimeHealthBarCanvas;
    private bool _ownsRuntimeHealthBar;
    private Texture2D _runtimeUiTexture;
    private Sprite _defaultUiSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        WhiteboxCharacterVisualUtility.ApplyCharacterWhite(gameObject);

        if (MinimumPlaytestHealth <= 0f)
        {
            MinimumPlaytestHealth = 220f;
        }

        if (MinimumDamageTakenMultiplier <= 0f)
        {
            MinimumDamageTakenMultiplier = 0.2f;
        }

        MaxHealth = Mathf.Max(MaxHealth, MinimumPlaytestHealth);
        _baseMaxHealth = Mathf.Max(1f, MaxHealth);
        MaxHealth = _baseMaxHealth;
        CurrentHealth = MaxHealth;

        CacheRendererColors();
        EnsureHealthBar();
        UpdateHealthBar();
    }

    private void Update()
    {
        TickCorrosionEffect();
    }

    private void LateUpdate()
    {
        if (Instance != this || IsDead)
        {
            return;
        }

        EnsureHealthBar();
        UpdateHealthBar();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Apply direct damage to the player and return the actual health loss
    /// </summary>
    /// <param name="damage"></param>
    /// <returns></returns>
    public float TakeDamage(float damage)
    {
        if (Instance != null && Instance != this)
        {
            return Instance.TakeDamage(damage);
        }

        if (IsDead || damage <= 0f)
        {
            return 0f;
        }

        float previousHealth = CurrentHealth;
        float effectiveMultiplier = Mathf.Max(MinimumDamageTakenMultiplier, _damageTakenMultiplier);
        CurrentHealth -= damage * effectiveMultiplier;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
        UpdateHealthBar();

        float actualDamage = previousHealth - CurrentHealth;
        if (CurrentHealth <= 0f)
        {
            Die();
        }

        return actualDamage;
    }

    /// <summary>
    /// Apply or refresh corrosion damage-over-time.
    /// </summary>
    public void ApplyCorrosion(float damagePerSecond, float duration, float tickInterval = 0.25f)
    {
        if (Instance != null && Instance != this)
        {
            Instance.ApplyCorrosion(damagePerSecond, duration, tickInterval);
            return;
        }

        if (IsDead || damagePerSecond <= 0f || duration <= 0f)
        {
            return;
        }

        _corrosionDurationRemaining = Mathf.Max(_corrosionDurationRemaining, duration);
        _corrosionTickInterval = Mathf.Max(0.05f, tickInterval);
        _corrosionDamagePerTick = Mathf.Max(_corrosionDamagePerTick, damagePerSecond * _corrosionTickInterval);
        _corrosionTickTimer = Mathf.Min(_corrosionTickTimer, _corrosionTickInterval);
        UpdateCorrosionVisual();
    }

    public bool IsCorroded()
    {
        return _corrosionDurationRemaining > 0f;
    }

    public void ApplyCombatEnhancement(float defenseMultiplier, float maxHealthMultiplier)
    {
        if (Instance != null && Instance != this)
        {
            Instance.ApplyCombatEnhancement(defenseMultiplier, maxHealthMultiplier);
            return;
        }

        float safeDefenseMultiplier = Mathf.Max(0.1f, defenseMultiplier);
        float safeHealthMultiplier = Mathf.Max(0.1f, maxHealthMultiplier);
        _damageTakenMultiplier = Mathf.Max(MinimumDamageTakenMultiplier, 1f / safeDefenseMultiplier);

        float previousMaxHealth = Mathf.Max(1f, MaxHealth);
        float healthRatio = CurrentHealth / previousMaxHealth;

        MaxHealth = _baseMaxHealth * safeHealthMultiplier;
        CurrentHealth = Mathf.Clamp(MaxHealth * healthRatio, 0f, MaxHealth);
        UpdateHealthBar();
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        UpdateHealthBar();

        if (_ownsRuntimeHealthBar && _runtimeHealthBarCanvas != null)
        {
            _runtimeHealthBarCanvas.gameObject.SetActive(false);
        }

        Debug.Log("Player died, mission failed.");
        RaidFlowController.Instance?.NotifyPlayerDied();
        RestoreOriginalRendererColors();
    }

    private void TickCorrosionEffect()
    {
        if (IsDead || _corrosionDurationRemaining <= 0f)
        {
            if (!IsDead)
            {
                RestoreOriginalRendererColors();
            }
            return;
        }

        _corrosionDurationRemaining -= Time.deltaTime;
        _corrosionTickTimer += Time.deltaTime;

        while (_corrosionTickTimer >= _corrosionTickInterval && !IsDead)
        {
            _corrosionTickTimer -= _corrosionTickInterval;
            TakeDamage(_corrosionDamagePerTick);
        }

        if (_corrosionDurationRemaining <= 0f)
        {
            _corrosionDurationRemaining = 0f;
            _corrosionDamagePerTick = 0f;
            RestoreOriginalRendererColors();
        }
        else
        {
            UpdateCorrosionVisual();
        }
    }

    private void UpdateHealthBar()
    {
        if (Instance != this)
        {
            return;
        }

        if (HealthFillImage == null)
        {
            return;
        }

        ConfigureHealthFillImage(HealthFillImage);
        float maxHealth = Mathf.Max(1f, MaxHealth);
        HealthFillImage.fillAmount = Mathf.Clamp01(CurrentHealth / maxHealth);
    }

    private void EnsureHealthBar()
    {
        if (HealthFillImage == null)
        {
            HealthFillImage = FindRuntimeHealthFillImage();
        }

        if (HealthFillImage == null)
        {
            HealthFillImage = CreateRuntimeHealthBar();
        }

        ConfigureHealthFillImage(HealthFillImage);
    }

    private Image CreateRuntimeHealthBar()
    {
        GameObject rootObject = new GameObject(
            "PlayerHealthBar",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(HealthBarBillboardController));
        rootObject.layer = gameObject.layer;

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        rootRect.localPosition = RuntimeHealthBarOffset;
        rootRect.localRotation = Quaternion.identity;
        rootRect.localScale = Vector3.one * 0.01f;
        rootRect.sizeDelta = RuntimeHealthBarSize;

        Canvas canvas = rootObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 250;

        _runtimeHealthBarCanvas = canvas;
        _ownsRuntimeHealthBar = true;

        Sprite defaultSprite = GetDefaultUiSprite();

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.SetParent(rootRect, false);
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = RuntimeHealthBarBackgroundColor;
        backgroundImage.raycastTarget = false;
        if (defaultSprite != null)
        {
            backgroundImage.sprite = defaultSprite;
        }

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(backgroundRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1.5f, 1.5f);
        fillRect.offsetMax = new Vector2(-1.5f, -1.5f);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = RuntimeHealthBarFillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        if (defaultSprite != null)
        {
            fillImage.sprite = defaultSprite;
        }

        return fillImage;
    }

    private Image FindRuntimeHealthFillImage()
    {
        Transform fillTransform = transform.Find("PlayerHealthBar/Background/Fill");
        if (fillTransform == null)
        {
            return null;
        }

        return fillTransform.GetComponent<Image>();
    }

    private void ConfigureHealthFillImage(Image fillImage)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.raycastTarget = false;
        if (fillImage.sprite == null)
        {
            fillImage.sprite = GetDefaultUiSprite();
        }
    }

    private Sprite GetDefaultUiSprite()
    {
        if (_defaultUiSprite != null)
        {
            return _defaultUiSprite;
        }

        if (_runtimeUiTexture == null)
        {
            _runtimeUiTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _runtimeUiTexture.name = "RuntimeUIWhitePixel";
            _runtimeUiTexture.SetPixel(0, 0, Color.white);
            _runtimeUiTexture.wrapMode = TextureWrapMode.Clamp;
            _runtimeUiTexture.filterMode = FilterMode.Bilinear;
            _runtimeUiTexture.Apply(false, false);
            _runtimeUiTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        _defaultUiSprite = Sprite.Create(
            _runtimeUiTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        _defaultUiSprite.name = "RuntimeUIWhiteSprite";
        _defaultUiSprite.hideFlags = HideFlags.HideAndDontSave;

        return _defaultUiSprite;
    }

    private void OnDestroy()
    {
        if (_defaultUiSprite != null)
        {
            Destroy(_defaultUiSprite);
            _defaultUiSprite = null;
        }

        if (_runtimeUiTexture != null)
        {
            Destroy(_runtimeUiTexture);
            _runtimeUiTexture = null;
        }
    }

    private void CacheRendererColors()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _originalRendererColors = new Color[_cachedRenderers.Length];

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent != null && rendererComponent.material.HasProperty("_Color"))
            {
                _originalRendererColors[i] = rendererComponent.material.color;
            }
            else
            {
                _originalRendererColors[i] = Color.white;
            }
        }
    }

    private void UpdateCorrosionVisual()
    {
        if (_cachedRenderers == null || _originalRendererColors == null)
        {
            return;
        }

        float pulse = 0.5f + Mathf.Sin(Time.time * 7f) * 0.5f;
        float tintStrength = CorrosionTintStrength * (0.55f + pulse * 0.45f);

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalRendererColors[i], CorrosionTintColor, tintStrength);
        }
    }

    private void RestoreOriginalRendererColors()
    {
        if (_cachedRenderers == null || _originalRendererColors == null)
        {
            return;
        }

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = _originalRendererColors[i];
        }
    }
}
