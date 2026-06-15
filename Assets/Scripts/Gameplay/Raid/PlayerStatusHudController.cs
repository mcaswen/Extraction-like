using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Progression;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space status HUD bound to a prefab layout.
/// </summary>
public sealed class PlayerStatusHudController : MonoBehaviour
{
    private const string HudPrefabAssetPath = "Assets/Resources/HUD/Pfb_PlayerStatusHud.prefab";

    private static PlayerStatusHudController _instance;

    [Header("Bars")]
    public Vector2 BarSize = new Vector2(380f, 24f);
    public Color HealthFillColor = new Color(0.36f, 0.78f, 0.48f, 1f);
    public Color CarryFillColor = new Color(0.95f, 0.7f, 0.22f, 1f);
    public Color CarryWarningFillColor = new Color(0.96f, 0.48f, 0.18f, 1f);
    public Color CarryOverloadFillColor = new Color(0.92f, 0.22f, 0.2f, 1f);
    public Color ExperienceFillColor = new Color(0.42f, 0.65f, 1f, 1f);

    [Header("Prefab References")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private Image _healthFillImage;
    [SerializeField] private Image _carryFillImage;
    [SerializeField] private Image _experienceFillImage;
    [SerializeField] private RectTransform _healthBarRect;
    [SerializeField] private RectTransform _carryBarRect;
    [SerializeField] private RectTransform _experienceBarRect;
    [SerializeField] private RectTransform _healthMarkerRect;
    [SerializeField] private RectTransform _carryMarkerRect;
    [SerializeField] private RectTransform _experienceMarkerRect;
    [SerializeField] private Text _healthText;
    [SerializeField] private Text _carryText;
    [SerializeField] private Text _experienceText;

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

        Debug.LogError($"Missing player status HUD prefab instance. Place {HudPrefabAssetPath} in the scene.");
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
        CachePrefabReferences();
        ApplyRuntimeColors();
    }

    private void LateUpdate()
    {
        RefreshBars();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

    }

    private void CachePrefabReferences()
    {
        if (_root == null)
            _root = FindComponent<RectTransform>("PlayerStatusHudRoot");

        if (_healthFillImage == null)
            _healthFillImage = FindComponent<Image>("HealthFill");

        if (_carryFillImage == null)
            _carryFillImage = FindComponent<Image>("CarryFill");

        if (_experienceFillImage == null)
            _experienceFillImage = FindComponent<Image>("ExperienceFill");

        if (_healthBarRect == null)
            _healthBarRect = FindComponent<RectTransform>("HealthBarProgress");

        if (_carryBarRect == null)
            _carryBarRect = FindComponent<RectTransform>("CarryLoadBarProgress");

        if (_experienceBarRect == null)
            _experienceBarRect = FindComponent<RectTransform>("ExperienceBarProgress");

        if (_healthMarkerRect == null)
            _healthMarkerRect = FindComponent<RectTransform>("HealthMarkerIcon");

        if (_carryMarkerRect == null)
            _carryMarkerRect = FindComponent<RectTransform>("CarryMarkerIcon");

        if (_experienceMarkerRect == null)
            _experienceMarkerRect = FindComponent<RectTransform>("ExperienceMarkerIcon");

        if (_healthText == null)
            _healthText = FindComponent<Text>("HealthValue");

        if (_carryText == null)
            _carryText = FindComponent<Text>("CarryValue");

        if (_experienceText == null)
            _experienceText = FindComponent<Text>("ExperienceValue");
    }

    private void RefreshBars()
    {
        bool hasFocusedAgent = TryGetFocusedAgent(
            out IAgentReadOnly focusedAgent,
            out AgentRuntimeHandle focusedHandle);
        if (_root != null)
        {
            _root.gameObject.SetActive(hasFocusedAgent);
        }

        if (!hasFocusedAgent)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, focusedAgent.MaxHealth);
        float currentHealth = Mathf.Max(0f, focusedAgent.CurrentHealth);
        string healthLabel = !string.IsNullOrWhiteSpace(focusedAgent.AgentIdValue)
            ? $"{focusedAgent.AgentIdValue}  {currentHealth:0} / {maxHealth:0}  HP"
            : $"{currentHealth:0} / {maxHealth:0}  HP";
        SetBar(
            _healthFillImage,
            _healthText,
            _healthMarkerRect,
            _healthBarRect,
            BarSize,
            currentHealth / maxHealth,
            healthLabel);

        InventoryScreenController inventory = InventoryScreenController.Instance;
        float occupiedCells = inventory != null ? inventory.GetCurrentBackpackOccupiedCells() : 0f;
        float usableCells = inventory != null ? inventory.GetMaxBackpackUsableCells() : 1f;
        float carryRatio = occupiedCells / Mathf.Max(1f, usableCells);

        if (_carryFillImage != null)
        {
            _carryFillImage.color = GetCarryFillColor(carryRatio);
        }

        SetBar(
            _carryFillImage,
            _carryText,
            _carryMarkerRect,
            _carryBarRect,
            BarSize,
            carryRatio,
            $"{occupiedCells:0} / {usableCells:0}  SLOTS");

        AgentLevelProgressionController progressionController = focusedHandle.PawnRoot != null
            ? focusedHandle.PawnRoot.GetComponent<AgentLevelProgressionController>()
            : null;
        float experienceRatio = progressionController != null ? progressionController.ExperienceRatio : 0f;
        string experienceLabel = BuildExperienceLabel(progressionController);

        SetBar(
            _experienceFillImage,
            _experienceText,
            _experienceMarkerRect,
            _experienceBarRect,
            BarSize,
            experienceRatio,
            experienceLabel);
    }

    private static bool TryGetFocusedAgent(
        out IAgentReadOnly focusedAgent,
        out AgentRuntimeHandle focusedHandle)
    {
        focusedAgent = null;
        focusedHandle = default;
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || !registry.TryGetFocusedHandle(out focusedHandle))
        {
            return false;
        }

        focusedAgent = focusedHandle.ReadOnly;
        return focusedAgent != null;
    }

    private static string BuildExperienceLabel(AgentLevelProgressionController progressionController)
    {
        if (progressionController == null)
        {
            return "LV --  0 / 0  XP";
        }

        if (progressionController.IsMaxLevel)
        {
            return $"LV {progressionController.Level}  MAX";
        }

        return $"LV {progressionController.Level}  {progressionController.CurrentExperience:0} / {progressionController.RequiredExperienceToNextLevel:0}  XP";
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

    private void ApplyRuntimeColors()
    {
        if (_healthFillImage != null)
            _healthFillImage.color = HealthFillColor;

        if (_carryFillImage != null)
            _carryFillImage.color = CarryFillColor;

        if (_experienceFillImage != null)
            _experienceFillImage.color = ExperienceFillColor;
    }

    private static void SetBar(
        Image fillImage,
        Text label,
        RectTransform markerRect,
        RectTransform barRect,
        Vector2 fallbackBarSize,
        float ratio,
        string text)
    {
        float clampedRatio = Mathf.Clamp01(ratio);
        float barWidth = barRect != null && barRect.rect.width > 0.01f
            ? barRect.rect.width
            : fallbackBarSize.x;

        if (fillImage != null)
        {
            RectTransform fillRect = fillImage.rectTransform;
            float minimumFillWidth = markerRect != null ? markerRect.rect.width * 0.5f : 0f;
            float fillWidth = Mathf.Max(minimumFillWidth, barWidth * clampedRatio);
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);
        }

        if (label != null)
        {
            label.text = text;
        }

        if (markerRect != null)
        {
            float halfWidth = Mathf.Max(0f, markerRect.rect.width * 0.5f);
            float targetX = Mathf.Clamp(barWidth * clampedRatio + halfWidth, halfWidth, barWidth - halfWidth);
            markerRect.anchoredPosition = new Vector2(targetX, markerRect.anchoredPosition.y);
        }
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform target = FindChildRecursive(transform, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, objectName, System.StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

}
