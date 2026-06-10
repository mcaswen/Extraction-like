using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a world-space icon above an enemy for chase prompts or persistent enemy markers.
/// </summary>
public sealed class EnemyChaseIndicatorController : MonoBehaviour
{
    /// <summary>
    /// Controls when the indicator is visible.
    /// </summary>
    public enum IndicatorVisibilityMode
    {
        ChaseOnly,
        Always
    }

    [Header("Indicator")]
    [SerializeField] private Sprite _indicatorSprite;
    [SerializeField] private IndicatorVisibilityMode _visibilityMode = IndicatorVisibilityMode.ChaseOnly;
    [SerializeField] private string _canvasObjectName = "ChaseIndicatorCanvas";
    [SerializeField] private string _imageObjectName = "ChaseIndicatorImage";
    [SerializeField] private Vector2 _size = new Vector2(56f, 56f);
    [SerializeField] private float _worldScale = 0.01f;
    [SerializeField] private Color _tint = Color.white;
    [SerializeField] private int _sortingOrder = 10;

    [Header("Position")]
    [SerializeField] private Vector3 _fallbackLocalOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private float _boundsVerticalPadding = 1.05f;

    private RectTransform _canvasRect;
    private Canvas _canvas;
    private Image _indicatorImage;
    private Camera _mainCamera;
    private Collider[] _anchorColliders;
    private Renderer[] _anchorRenderers;

    private EnemyBehaviorController _basicMelee;
    private RangedEnemyBehaviorController _ranged;
    private ModernStranderBehaviorController _modernStrander;
    private TidalAberrationBehaviorController _tidalAberration;
    private AncientStranderBehaviorController _ancientStrander;
    private HunterBossBehaviorController _hunterBoss;

    /// <summary>
    /// Current serialized visibility mode, used by editor setup tools to avoid duplicate bindings.
    /// </summary>
    public IndicatorVisibilityMode VisibilityMode => _visibilityMode;

    /// <summary>
    /// Canvas object name used by this indicator instance.
    /// </summary>
    public string CanvasObjectName => _canvasObjectName;

    private void Awake()
    {
        CacheStateSources();
        EnsureIndicatorHierarchy();
        CacheAnchorSources();
        ApplyVisualSettings();
        SetIndicatorVisible(false);
    }

    private void OnDisable()
    {
        SetIndicatorVisible(false);
    }

    private void LateUpdate()
    {
        if (_indicatorSprite == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        bool shouldShow = ShouldShowIndicator();
        SetIndicatorVisible(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        UpdateIndicatorPosition();
        FaceMainCamera();
    }

    /// <summary>
    /// Configures the icon asset and placement values from editor setup tools.
    /// </summary>
    public void ConfigureIndicator(
        Sprite indicatorSprite,
        Vector3 fallbackLocalOffset,
        Vector2 size,
        float worldScale,
        float boundsVerticalPadding)
    {
        ConfigureIndicator(
            indicatorSprite,
            _visibilityMode,
            _canvasObjectName,
            _imageObjectName,
            fallbackLocalOffset,
            size,
            worldScale,
            boundsVerticalPadding,
            _sortingOrder);
    }

    /// <summary>
    /// Configures the icon asset, visibility mode, hierarchy names, and placement values from editor setup tools.
    /// </summary>
    public void ConfigureIndicator(
        Sprite indicatorSprite,
        IndicatorVisibilityMode visibilityMode,
        string canvasObjectName,
        string imageObjectName,
        Vector3 fallbackLocalOffset,
        Vector2 size,
        float worldScale,
        float boundsVerticalPadding,
        int sortingOrder)
    {
        _indicatorSprite = indicatorSprite;
        _visibilityMode = visibilityMode;
        _canvasObjectName = string.IsNullOrWhiteSpace(canvasObjectName)
            ? "ChaseIndicatorCanvas"
            : canvasObjectName;
        _imageObjectName = string.IsNullOrWhiteSpace(imageObjectName)
            ? "ChaseIndicatorImage"
            : imageObjectName;
        _fallbackLocalOffset = fallbackLocalOffset;
        _size = size;
        _worldScale = Mathf.Max(0.0001f, worldScale);
        _boundsVerticalPadding = Mathf.Max(0f, boundsVerticalPadding);
        _sortingOrder = sortingOrder;
        ApplyVisualSettings();
    }

    /// <summary>
    /// Returns true when this component already represents the requested indicator binding.
    /// </summary>
    public bool MatchesBinding(IndicatorVisibilityMode visibilityMode, string canvasObjectName)
    {
        return _visibilityMode == visibilityMode &&
               string.Equals(_canvasObjectName, canvasObjectName, System.StringComparison.Ordinal);
    }

    private void CacheStateSources()
    {
        _basicMelee = GetComponentInChildren<EnemyBehaviorController>(true);
        _ranged = GetComponentInChildren<RangedEnemyBehaviorController>(true);
        _modernStrander = GetComponentInChildren<ModernStranderBehaviorController>(true);
        _tidalAberration = GetComponentInChildren<TidalAberrationBehaviorController>(true);
        _ancientStrander = GetComponentInChildren<AncientStranderBehaviorController>(true);
        _hunterBoss = GetComponentInChildren<HunterBossBehaviorController>(true);
    }

    private void CacheAnchorSources()
    {
        _anchorColliders = GetComponentsInChildren<Collider>(true);
        _anchorRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void EnsureIndicatorHierarchy()
    {
        if (string.IsNullOrWhiteSpace(_canvasObjectName))
        {
            _canvasObjectName = "ChaseIndicatorCanvas";
        }

        if (string.IsNullOrWhiteSpace(_imageObjectName))
        {
            _imageObjectName = "ChaseIndicatorImage";
        }

        Transform existingCanvas = transform.Find(_canvasObjectName);
        GameObject canvasObject;
        if (existingCanvas != null)
        {
            canvasObject = existingCanvas.gameObject;
            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvas = canvasObject.GetComponent<Canvas>();
        }
        else
        {
            canvasObject = new GameObject(CanvasObjectName, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = gameObject.layer;
            _canvasRect = canvasObject.GetComponent<RectTransform>();
            _canvas = canvasObject.GetComponent<Canvas>();
        }

        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = _sortingOrder;
        _canvasRect.localRotation = Quaternion.identity;
        _canvasRect.localPosition = _fallbackLocalOffset;

        Transform existingImage = canvasObject.transform.Find(_imageObjectName);
        GameObject imageObject;
        if (existingImage != null)
        {
            imageObject = existingImage.gameObject;
            _indicatorImage = imageObject.GetComponent<Image>();
        }
        else
        {
            imageObject = new GameObject(_imageObjectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            imageObject.layer = canvasObject.layer;
            _indicatorImage = imageObject.GetComponent<Image>();
        }

        if (_indicatorImage == null)
        {
            _indicatorImage = imageObject.AddComponent<Image>();
        }

        RectTransform imageRect = _indicatorImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyVisualSettings()
    {
        if (_canvasRect != null)
        {
            _canvasRect.sizeDelta = _size;
            _canvasRect.localScale = Vector3.one * Mathf.Max(0.0001f, _worldScale);
        }

        if (_canvas != null)
        {
            _canvas.sortingOrder = _sortingOrder;
        }

        if (_indicatorImage == null)
        {
            return;
        }

        _indicatorImage.sprite = _indicatorSprite;
        _indicatorImage.color = _tint;
        _indicatorImage.preserveAspect = true;
        _indicatorImage.raycastTarget = false;
        _indicatorImage.rectTransform.sizeDelta = _size;
    }

    private bool ShouldShowIndicator()
    {
        return _visibilityMode == IndicatorVisibilityMode.Always || IsInChaseState();
    }

    private bool IsInChaseState()
    {
        if (_basicMelee != null)
        {
            return _basicMelee.CurrentState == EnemyBehaviorController.EnemyState.Chase;
        }

        if (_ranged != null)
        {
            return _ranged.CurrentState == RangedEnemyBehaviorController.EnemyState.Chase;
        }

        if (_modernStrander != null)
        {
            return _modernStrander.CurrentState == ModernStranderBehaviorController.EnemyState.Chase;
        }

        if (_tidalAberration != null)
        {
            return _tidalAberration.CurrentState == TidalAberrationBehaviorController.EnemyState.Chase;
        }

        if (_ancientStrander != null)
        {
            return _ancientStrander.CurrentState == AncientStranderBehaviorController.EnemyState.Chase;
        }

        return _hunterBoss != null &&
               _hunterBoss.CurrentState == HunterBossBehaviorController.BossState.Chase;
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (_canvasRect != null && _canvasRect.gameObject.activeSelf != visible)
        {
            _canvasRect.gameObject.SetActive(visible);
        }
    }

    private void UpdateIndicatorPosition()
    {
        if (_canvasRect == null)
        {
            return;
        }

        _canvasRect.position = TryGetBoundsAnchor(out Vector3 anchorPosition)
            ? anchorPosition
            : transform.TransformPoint(_fallbackLocalOffset);
    }

    private bool TryGetBoundsAnchor(out Vector3 anchorPosition)
    {
        if (TryGetColliderBounds(out Bounds bounds) || TryGetRendererBounds(out bounds))
        {
            Vector3 center = bounds.center;
            anchorPosition = new Vector3(center.x, bounds.max.y + _boundsVerticalPadding, center.z);
            return true;
        }

        anchorPosition = default;
        return false;
    }

    private bool TryGetColliderBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (_anchorColliders == null)
        {
            return false;
        }

        for (int index = 0; index < _anchorColliders.Length; index++)
        {
            Collider anchorCollider = _anchorColliders[index];
            if (anchorCollider == null ||
                !anchorCollider.enabled ||
                anchorCollider.isTrigger ||
                IsPartOfIndicator(anchorCollider.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = anchorCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(anchorCollider.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (_anchorRenderers == null)
        {
            return false;
        }

        for (int index = 0; index < _anchorRenderers.Length; index++)
        {
            Renderer anchorRenderer = _anchorRenderers[index];
            if (anchorRenderer == null ||
                !anchorRenderer.enabled ||
                anchorRenderer is LineRenderer ||
                anchorRenderer is TrailRenderer ||
                anchorRenderer is ParticleSystemRenderer ||
                IsPartOfIndicator(anchorRenderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = anchorRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(anchorRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool IsPartOfIndicator(Transform candidate)
    {
        return _canvasRect != null && candidate != null && candidate.IsChildOf(_canvasRect);
    }

    private void FaceMainCamera()
    {
        if (_canvasRect == null)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return;
        }

        _canvas.worldCamera = _mainCamera;
        _canvasRect.LookAt(_canvasRect.position + _mainCamera.transform.forward);
    }
}
