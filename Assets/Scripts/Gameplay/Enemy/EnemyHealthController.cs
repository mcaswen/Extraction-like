using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 敌人生命控制器。
/// 负责受伤、血条刷新、死亡和死亡掉落容器生成。
/// </summary>
public class EnemyHealthController : MonoBehaviour
{
    public float MaxHealth = 100f;

    [Header("Health UI")]
    public Image HealthFillImage;

    [Header("Death Loot")]
    public bool SpawnLootContainerOnDeath = true;
    public GameObject DeathLootContainerPrefab;
    public Transform DeathLootSpawnPoint;
    public Vector3 DeathLootSpawnOffset = new Vector3(0f, 0.1f, 0f);

    private float _currentHealth;
    private bool _hasDied;

    private void Start()
    {
        WhiteboxCharacterVisualUtility.ApplyCharacterWhite(gameObject);
        _currentHealth = MaxHealth;
        UpdateHealthBar();
    }

    /// <summary>
    /// 对敌人造成伤害。
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (_hasDied)
        {
            return;
        }

        _currentHealth -= damageAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, MaxHealth);
        UpdateHealthBar();

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 获取当前血量比例。
    /// </summary>
    public float GetCurrentHealthRatio()
    {
        if (MaxHealth <= 0f)
        {
            return 0f;
        }

        return _currentHealth / MaxHealth;
    }

    private void UpdateHealthBar()
    {
        if (HealthFillImage != null)
        {
            HealthFillImage.fillAmount = MaxHealth <= 0f ? 0f : _currentHealth / MaxHealth;
        }
    }

    private void Die()
    {
        if (_hasDied)
        {
            return;
        }

        _hasDied = true;
        RaidFlowController.Instance?.NotifyEnemyKilled(gameObject.name);
        SpawnDeathLootContainer();
        Destroy(gameObject);
    }

    private void SpawnDeathLootContainer()
    {
        if (!SpawnLootContainerOnDeath || DeathLootContainerPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.position
            : transform.position + DeathLootSpawnOffset;
        Quaternion spawnRotation = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.rotation
            : Quaternion.identity;

        GameObject lootContainerObject = Instantiate(DeathLootContainerPrefab, spawnPosition, spawnRotation);
        WhiteboxCharacterVisualUtility.ApplySolidColor(lootContainerObject, new Color(0.96f, 0.96f, 0.98f, 1f));
        LootBoxEntity lootBox = lootContainerObject.GetComponent<LootBoxEntity>();
        if (lootBox == null)
        {
            lootBox = lootContainerObject.GetComponentInChildren<LootBoxEntity>();
        }

        if (lootBox != null)
        {
            lootBox.PrecalculateLootIfNeeded();
        }
    }
}

/// <summary>
/// Runtime status effects for enemies: freeze, slow and magic seal.
/// </summary>
[DisallowMultipleComponent]
public class EnemyStatusEffectController : MonoBehaviour
{
    [Header("Visual")]
    public Color FrozenTintColor = new Color(0.58f, 0.86f, 1f, 1f);
    public Color SealedTintColor = new Color(0.95f, 0.62f, 1f, 1f);
    public float TintStrength = 0.55f;

    private float _freezeDurationRemaining;
    private float _slowDurationRemaining;
    private float _magicSealDurationRemaining;
    private float _slowMultiplier = 1f;

    private NavMeshAgent _navMeshAgent;
    private float _defaultAgentSpeed;
    private bool _defaultAgentAutoBraking;
    private Renderer[] _cachedRenderers;
    private Color[] _originalColors;

    private readonly List<MonoBehaviour> _trackedBehaviorScripts = new List<MonoBehaviour>();
    private readonly Dictionary<MonoBehaviour, bool> _defaultScriptState = new Dictionary<MonoBehaviour, bool>();

    public bool IsFrozen => _freezeDurationRemaining > 0f;
    public bool IsMagicSealed => _magicSealDurationRemaining > 0f;

    private void Awake()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_navMeshAgent != null)
        {
            _defaultAgentSpeed = Mathf.Max(0.01f, _navMeshAgent.speed);
            _defaultAgentAutoBraking = _navMeshAgent.autoBraking;
        }

        CacheRendererColors();
        CacheBehaviorScripts();
    }

    private void OnDisable()
    {
        _freezeDurationRemaining = 0f;
        _slowDurationRemaining = 0f;
        _magicSealDurationRemaining = 0f;
        _slowMultiplier = 1f;
        ApplyAgentState();
        ApplyBehaviorScriptState();
        RestoreRendererColors();
    }

    private void Update()
    {
        TickDurations();
        ApplyAgentState();
        ApplyBehaviorScriptState();
        UpdateVisual();
    }

    public void ApplyFreeze(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _freezeDurationRemaining = Mathf.Max(_freezeDurationRemaining, durationSeconds);
    }

    public void ApplySlow(float slowMultiplier, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _slowDurationRemaining = Mathf.Max(_slowDurationRemaining, durationSeconds);
        _slowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
    }

    public void ApplyMagicSeal(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _magicSealDurationRemaining = Mathf.Max(_magicSealDurationRemaining, durationSeconds);
    }

    public void BreakFreeze()
    {
        _freezeDurationRemaining = 0f;
    }

    private void TickDurations()
    {
        if (_freezeDurationRemaining > 0f)
        {
            _freezeDurationRemaining = Mathf.Max(0f, _freezeDurationRemaining - Time.deltaTime);
        }

        if (_slowDurationRemaining > 0f)
        {
            _slowDurationRemaining = Mathf.Max(0f, _slowDurationRemaining - Time.deltaTime);
            if (_slowDurationRemaining <= 0f)
            {
                _slowMultiplier = 1f;
            }
        }

        if (_magicSealDurationRemaining > 0f)
        {
            _magicSealDurationRemaining = Mathf.Max(0f, _magicSealDurationRemaining - Time.deltaTime);
        }
    }

    private void ApplyAgentState()
    {
        if (_navMeshAgent == null)
        {
            return;
        }

        if (IsFrozen)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            return;
        }

        float effectiveSpeedMultiplier = _slowDurationRemaining > 0f ? _slowMultiplier : 1f;
        _navMeshAgent.speed = _defaultAgentSpeed * effectiveSpeedMultiplier;
        _navMeshAgent.autoBraking = _defaultAgentAutoBraking;
    }

    private void ApplyBehaviorScriptState()
    {
        if (_trackedBehaviorScripts.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < _trackedBehaviorScripts.Count; i++)
        {
            MonoBehaviour script = _trackedBehaviorScripts[i];
            if (script == null)
            {
                continue;
            }

            bool defaultEnabled = _defaultScriptState.TryGetValue(script, out bool value) && value;
            bool shouldBlockByFreeze = IsFrozen;
            bool shouldBlockBySeal = IsMagicSealed && IsMagicSensitiveScript(script);
            bool shouldEnable = defaultEnabled && !shouldBlockByFreeze && !shouldBlockBySeal;

            if (script.enabled != shouldEnable)
            {
                script.enabled = shouldEnable;
            }
        }
    }

    private void UpdateVisual()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        if (!IsFrozen && !IsMagicSealed)
        {
            RestoreRendererColors();
            return;
        }

        Color tintColor = IsFrozen ? FrozenTintColor : SealedTintColor;
        float pulse = 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f;
        float effectiveStrength = TintStrength * (0.5f + pulse * 0.5f);

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalColors[i], tintColor, effectiveStrength);
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

    private void CacheBehaviorScripts()
    {
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour script = scripts[i];
            if (script == null || script == this)
            {
                continue;
            }

            string typeName = script.GetType().Name;
            if (!typeName.EndsWith("BehaviorController"))
            {
                continue;
            }

            _trackedBehaviorScripts.Add(script);
            _defaultScriptState[script] = script.enabled;
        }
    }

    private static bool IsMagicSensitiveScript(MonoBehaviour script)
    {
        if (script == null)
        {
            return false;
        }

        string typeName = script.GetType().Name;
        return typeName == "AnchorSentinelBehaviorController" ||
               typeName == "TidalAberrationBehaviorController" ||
               typeName == "ModernStranderBehaviorController" ||
               typeName == "HunterBossBehaviorController";
    }
}
