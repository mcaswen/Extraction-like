using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Agent.Core
{
    /// <summary>
    /// Agent 的权威生命值组件。
    /// AgentPawnRoot 负责战斗结算、黑板同步和死亡流程，本组件只保存生命状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentHealthController : MonoBehaviour, global::ICombatDamageReceiver
    {
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Min(0)] private int _currentHealth;

        [Header("Status Effect")]
        [SerializeField] private Color _corrosionTintColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] private float _corrosionTintStrength = 0.45f;

        [Header("Health UI")]
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private Vector3 _runtimeHealthBarOffset = new Vector3(0f, 2.15f, 0f);
        [SerializeField] private Vector2 _runtimeHealthBarSize = new Vector2(120f, 16f);
        [SerializeField] private Color _runtimeHealthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] private Color _runtimeHealthBarFillColor = new Color(0.16f, 0.86f, 0.24f, 1f);

        private AgentPawnRoot _pawnRoot;
        private bool _initialized;
        private float _corrosionDurationRemaining;
        private float _corrosionTickTimer;
        private float _corrosionTickInterval = 0.25f;
        private float _corrosionDamagePerTick;
        private Renderer[] _cachedRenderers;
        private Color[] _originalRendererColors;
        private Canvas _runtimeHealthBarCanvas;
        private bool _ownsRuntimeHealthBar;
        private Texture2D _runtimeUiTexture;
        private Sprite _defaultUiSprite;

        public int CurrentHealth => Mathf.Max(0, _currentHealth);
        public int MaxHealth => Mathf.Max(1, _maxHealth);
        public float HealthRatio => MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)CurrentHealth / MaxHealth);
        public bool IsDead => _initialized && CurrentHealth <= 0;
        public Transform DamageRootTransform => transform;
        public bool IsCombatDamageReceiverAlive => _initialized && !IsDead;

        private void Awake()
        {
            CacheComponents();
            CacheRendererColors();
        }

        private void Start()
        {
            EnsureHealthBar();
            UpdateHealthBar();
        }

        private void Update()
        {
            TickCorrosionEffect();
        }

        private void LateUpdate()
        {
            if (IsDead)
            {
                UpdateHealthBar();
                return;
            }

            EnsureHealthBar();
            UpdateHealthBar();
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1, _maxHealth);
            _currentHealth = Mathf.Max(0, _currentHealth);
            _corrosionTintStrength = Mathf.Max(0f, _corrosionTintStrength);
        }

        public void Initialize(int maxHealth)
        {
            _maxHealth = Mathf.Max(1, maxHealth);
            _currentHealth = _maxHealth;
            _initialized = true;
            UpdateHealthBar();
        }

        public void SyncMaxHealth(int maxHealth)
        {
            int safeMaxHealth = Mathf.Max(1, maxHealth);
            if (!_initialized)
            {
                Initialize(safeMaxHealth);
                return;
            }

            int previousMaxHealth = _maxHealth;
            _maxHealth = safeMaxHealth;

            if (!IsDead && safeMaxHealth > previousMaxHealth)
                _currentHealth += safeMaxHealth - previousMaxHealth;

            UpdateHealthBar();
        }

        public float ApplyResolvedDamage(int damageAmount)
        {
            if (IsDead || damageAmount <= 0)
                return 0f;

            _initialized = true;
            int previousHealth = CurrentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - damageAmount);
            UpdateHealthBar();
            return Mathf.Max(0, previousHealth - CurrentHealth);
        }

        public void ApplyCorrosion(float damagePerSecond, float duration, float tickInterval = 0.25f)
        {
            if (IsDead || damagePerSecond <= 0f || duration <= 0f)
                return;

            _corrosionDurationRemaining = Mathf.Max(_corrosionDurationRemaining, duration);
            _corrosionTickInterval = Mathf.Max(0.05f, tickInterval);
            _corrosionDamagePerTick = Mathf.Max(_corrosionDamagePerTick, damagePerSecond * _corrosionTickInterval);
            _corrosionTickTimer = Mathf.Min(_corrosionTickTimer, _corrosionTickInterval);
            UpdateCorrosionVisual();
        }

        public float TakeCombatDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, GameObject source)
        {
            CacheComponents();
            if (_pawnRoot != null && _pawnRoot.enabled)
                return _pawnRoot.TakeCombatDamage(damage, hitPoint, hitDirection, source);

            return ApplyResolvedDamage(Mathf.RoundToInt(Mathf.Max(0f, damage)));
        }

        private void CacheComponents()
        {
            if (_pawnRoot == null)
                _pawnRoot = GetComponent<AgentPawnRoot>();
        }

        private void TickCorrosionEffect()
        {
            if (IsDead || _corrosionDurationRemaining <= 0f)
            {
                if (!IsDead)
                    RestoreOriginalRendererColors();
                return;
            }

            _corrosionDurationRemaining -= Time.deltaTime;
            _corrosionTickTimer += Time.deltaTime;

            while (_corrosionTickTimer >= _corrosionTickInterval && !IsDead)
            {
                _corrosionTickTimer -= _corrosionTickInterval;
                TakeCombatDamage(_corrosionDamagePerTick, transform.position, Vector3.zero, null);
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
            if (_healthFillImage == null)
                return;

            ConfigureHealthFillImage(_healthFillImage);
            _healthFillImage.fillAmount = Mathf.Clamp01((float)CurrentHealth / MaxHealth);

            if (IsDead && _ownsRuntimeHealthBar && _runtimeHealthBarCanvas != null)
                _runtimeHealthBarCanvas.gameObject.SetActive(false);
        }

        private void EnsureHealthBar()
        {
            if (_healthFillImage == null)
                _healthFillImage = FindRuntimeHealthFillImage();

            if (_healthFillImage == null)
                _healthFillImage = CreateRuntimeHealthBar();

            ConfigureHealthFillImage(_healthFillImage);
        }

        private Image CreateRuntimeHealthBar()
        {
            GameObject rootObject = new GameObject(
                "AgentHealthBar",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(HealthBarBillboardController));
            rootObject.layer = gameObject.layer;

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.SetParent(transform, false);
            rootRect.localPosition = _runtimeHealthBarOffset;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one * 0.01f;
            rootRect.sizeDelta = _runtimeHealthBarSize;

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
            backgroundImage.color = _runtimeHealthBarBackgroundColor;
            backgroundImage.raycastTarget = false;
            if (defaultSprite != null)
                backgroundImage.sprite = defaultSprite;

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
            fillImage.color = _runtimeHealthBarFillColor;
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            if (defaultSprite != null)
                fillImage.sprite = defaultSprite;

            return fillImage;
        }

        private Image FindRuntimeHealthFillImage()
        {
            Transform fillTransform = transform.Find("AgentHealthBar/Background/Fill");
            return fillTransform != null ? fillTransform.GetComponent<Image>() : null;
        }

        private void ConfigureHealthFillImage(Image fillImage)
        {
            if (fillImage == null)
                return;

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.raycastTarget = false;
            if (fillImage.sprite == null)
                fillImage.sprite = GetDefaultUiSprite();
        }

        private Sprite GetDefaultUiSprite()
        {
            if (_defaultUiSprite != null)
                return _defaultUiSprite;

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

        private void CacheRendererColors()
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _originalRendererColors = new Color[_cachedRenderers.Length];

            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = _cachedRenderers[i];
                if (rendererComponent != null && rendererComponent.material.HasProperty("_Color"))
                    _originalRendererColors[i] = rendererComponent.material.color;
                else
                    _originalRendererColors[i] = Color.white;
            }
        }

        private void UpdateCorrosionVisual()
        {
            if (_cachedRenderers == null || _originalRendererColors == null)
                return;

            float pulse = 0.5f + Mathf.Sin(Time.time * 7f) * 0.5f;
            float tintStrength = _corrosionTintStrength * (0.55f + pulse * 0.45f);

            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = _cachedRenderers[i];
                if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
                    continue;

                rendererComponent.material.color = Color.Lerp(
                    _originalRendererColors[i],
                    _corrosionTintColor,
                    tintStrength);
            }
        }

        private void RestoreOriginalRendererColors()
        {
            if (_cachedRenderers == null || _originalRendererColors == null)
                return;

            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = _cachedRenderers[i];
                if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
                    continue;

                rendererComponent.material.color = _originalRendererColors[i];
            }
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
    }
}
