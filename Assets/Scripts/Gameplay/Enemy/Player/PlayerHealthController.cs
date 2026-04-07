using UnityEngine;

/// <summary>
/// 玩家生命控制器。
/// </summary>
public class PlayerHealthController : MonoBehaviour
{
    public float MaxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    [Header("Status Effect")]
    public Color CorrosionTintColor = new Color(0.45f, 1f, 0.55f, 1f);
    public float CorrosionTintStrength = 0.45f;

    private float _corrosionDurationRemaining;
    private float _corrosionTickTimer;
    private float _corrosionTickInterval = 0.25f;
    private float _corrosionDamagePerTick;
    private Renderer[] _cachedRenderers;
    private Color[] _originalRendererColors;

    private void Start()
    {
        WhiteboxCharacterVisualUtility.ApplyCharacterWhite(gameObject);
        CurrentHealth = MaxHealth;
        CacheRendererColors();
    }

    private void Update()
    {
        TickCorrosionEffect();
    }

    /// <summary>
    /// 对玩家造成伤害。
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDead)
        {
            return;
        }

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

        Debug.Log($"玩家受到攻击，当前血量: {CurrentHealth}");
        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 施加或刷新腐蚀持续伤害。
    /// </summary>
    public void ApplyCorrosion(float damagePerSecond, float duration, float tickInterval = 0.25f)
    {
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

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Debug.Log("玩家死亡，任务失败。");
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
