using UnityEngine;

/// <summary>
/// Scene pickup used for magic books/relics that unlock player magic progression.
/// Place this on world objects scattered in the island.
/// </summary>
public class MagicKnowledgePickup : MonoBehaviour, IInteractable
{
    [Header("Lore")]
    public string PickupName = "Pre-Civilization Magic Record";
    [TextArea(2, 4)]
    public string Description = "Study this record to unlock an ability.";

    [Header("Unlock")]
    public MagicUnlockType UnlockType = MagicUnlockType.IceFreeze;
    [Min(1)]
    public int RunePatternPoints = 1;
    public bool ConsumeOnUnlock = true;

    [Header("Visual")]
    public bool EnableHighlightPulse = true;
    public Color HighlightColor = new Color(0.66f, 0.86f, 1f, 1f);
    public float HighlightStrength = 0.35f;
    public float HighlightPulseSpeed = 4f;

    private bool _consumed;
    private Renderer[] _cachedRenderers;
    private Color[] _originalColors;

    private void Awake()
    {
        CacheRendererColors();
    }

    private void Update()
    {
        if (_consumed || !EnableHighlightPulse)
        {
            return;
        }

        UpdateHighlightVisual();
    }

    public string GetPromptText()
    {
        if (_consumed)
        {
            return $"{PickupName} (studied)";
        }

        string actionText = UnlockType == MagicUnlockType.RunePattern ? "attune" : "study";
        return $"[F] {actionText} {PickupName}";
    }

    public void Interact()
    {
        if (_consumed)
        {
            return;
        }

        if (PlayerShootingController.Instance == null)
        {
            Debug.LogWarning($"[MagicKnowledgePickup] PlayerShootingController not found for {PickupName}.");
            return;
        }

        bool didUnlock = PlayerShootingController.Instance.TryUnlockMagic(UnlockType, RunePatternPoints, PickupName);
        if (!didUnlock)
        {
            return;
        }

        _consumed = true;
        RestoreRendererColors();

        if (ConsumeOnUnlock)
        {
            Destroy(gameObject);
            return;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private void CacheRendererColors()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_cachedRenderers.Length];

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            _originalColors[i] = rendererComponent != null && rendererComponent.material.HasProperty("_Color")
                ? rendererComponent.material.color
                : Color.white;
        }
    }

    private void UpdateHighlightVisual()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        float pulse = 0.5f + Mathf.Sin(Time.time * Mathf.Max(0.1f, HighlightPulseSpeed)) * 0.5f;
        float strength = Mathf.Clamp01(HighlightStrength) * (0.5f + pulse * 0.5f);
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalColors[i], HighlightColor, strength);
        }
    }

    private void RestoreRendererColors()
    {
        if (_cachedRenderers == null || _originalColors == null)
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

            rendererComponent.material.color = _originalColors[i];
        }
    }
}
