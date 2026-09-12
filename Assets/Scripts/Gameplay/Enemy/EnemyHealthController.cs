using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// 敌人受到伤害的来源类型。
/// </summary>
public enum EnemyDamageSourceType
{
    Unknown,
    Projectile,
    Magic,
    Melee,
    Environment
}

/// <summary>
/// 敌人受击上下文。
/// 用于区分直接攻击者、命中点、攻击方向以及是否由玩家直接造成。
/// </summary>
public readonly struct EnemyDamageContext
{
    /// <summary>
    /// 造成伤害的攻击者。
    /// </summary>
    public readonly Transform Attacker;

    /// <summary>
    /// 本次伤害命中的世界坐标。
    /// </summary>
    public readonly Vector3 HitPosition;

    /// <summary>
    /// 攻击来源的世界坐标。
    /// </summary>
    public readonly Vector3 SourcePosition;

    /// <summary>
    /// 从来源指向命中点的归一化方向。
    /// </summary>
    public readonly Vector3 IncomingDirection;

    /// <summary>
    /// 本次伤害是否携带明确攻击者。
    /// </summary>
    public readonly bool IsDirectDamage;

    /// <summary>
    /// 本次直接伤害是否来自玩家。
    /// </summary>
    public readonly bool IsDirectPlayerDamage;

    /// <summary>
    /// 伤害来源类型。
    /// </summary>
    public readonly EnemyDamageSourceType SourceType;

    /// <summary>
    /// 创建一份敌人受击上下文。
    /// </summary>
    /// <param name="attacker">攻击者。</param>
    /// <param name="hitPosition">命中位置。</param>
    /// <param name="sourcePosition">攻击来源位置。</param>
    /// <param name="incomingDirection">攻击方向。</param>
    /// <param name="isDirectDamage">是否为直接伤害。</param>
    /// <param name="isDirectPlayerDamage">是否为玩家直接伤害。</param>
    /// <param name="sourceType">伤害来源类型。</param>
    public EnemyDamageContext(
        Transform attacker,
        Vector3 hitPosition,
        Vector3 sourcePosition,
        Vector3 incomingDirection,
        bool isDirectDamage,
        bool isDirectPlayerDamage,
        EnemyDamageSourceType sourceType)
    {
        Attacker = attacker;
        HitPosition = hitPosition;
        SourcePosition = sourcePosition;
        IncomingDirection = incomingDirection.sqrMagnitude > 0.0001f
            ? incomingDirection.normalized
            : Vector3.zero;
        IsDirectDamage = isDirectDamage;
        IsDirectPlayerDamage = isDirectPlayerDamage;
        SourceType = sourceType;
    }

    /// <summary>
    /// 空受击上下文，用于兼容没有攻击者信息的旧伤害调用。
    /// </summary>
    public static EnemyDamageContext Empty => new EnemyDamageContext(
        null,
        Vector3.zero,
        Vector3.zero,
        Vector3.zero,
        false,
        false,
        EnemyDamageSourceType.Unknown);

    /// <summary>
    /// 根据任意攻击者创建直接伤害上下文。
    /// </summary>
    /// <param name="attacker">攻击者 Transform。</param>
    /// <param name="hitPosition">命中位置。</param>
    /// <param name="sourcePosition">攻击来源位置。</param>
    /// <param name="incomingDirection">攻击方向。</param>
    /// <param name="sourceType">伤害来源类型。</param>
    /// <returns>构造好的受击上下文。</returns>
    public static EnemyDamageContext FromAttacker(
        Transform attacker,
        Vector3 hitPosition,
        Vector3 sourcePosition,
        Vector3 incomingDirection,
        EnemyDamageSourceType sourceType)
    {
        return new EnemyDamageContext(
            attacker,
            hitPosition,
            sourcePosition,
            incomingDirection,
            attacker != null,
            PlayerTargetResolver.IsPlayerTarget(attacker),
            sourceType);
    }

    /// <summary>
    /// 根据玩家 Transform 创建玩家直接伤害上下文。
    /// </summary>
    /// <param name="playerTransform">玩家 Transform。</param>
    /// <param name="hitPosition">命中位置。</param>
    /// <param name="sourcePosition">攻击来源位置。</param>
    /// <param name="incomingDirection">攻击方向。</param>
    /// <param name="sourceType">伤害来源类型。</param>
    /// <returns>构造好的玩家受击上下文。</returns>
    public static EnemyDamageContext FromPlayer(
        Transform playerTransform,
        Vector3 hitPosition,
        Vector3 sourcePosition,
        Vector3 incomingDirection,
        EnemyDamageSourceType sourceType)
    {
        return new EnemyDamageContext(
            playerTransform,
            hitPosition,
            sourcePosition,
            incomingDirection,
            playerTransform != null,
            playerTransform != null,
            sourceType);
    }
}

/// <summary>
/// 接收敌人直接受击反应的接口。
/// 敌人行为控制器实现它后，可以在被明确攻击者命中时立刻追击。
/// </summary>
public interface IEnemyDirectDamageReceiver
{
    /// <summary>
    /// 通知组件该敌人收到了带攻击者上下文的直接伤害。
    /// </summary>
    /// <param name="context">本次受击上下文。</param>
    void NotifyDirectDamage(EnemyDamageContext context);
}

/// <summary>
/// 可被战斗系统伤害的通用目标接口。
/// 玩家和 Agent 均可通过该接口接收敌人攻击。
/// </summary>
public interface ICombatDamageReceiver
{
    /// <summary>
    /// 目标的战斗根节点。
    /// </summary>
    Transform DamageRootTransform { get; }

    /// <summary>
    /// 当前目标是否仍可接受战斗伤害。
    /// </summary>
    bool IsCombatDamageReceiverAlive { get; }

    /// <summary>
    /// 对目标造成战斗伤害，并返回实际扣除的生命值。
    /// </summary>
    /// <param name="damage">原始伤害值。</param>
    /// <param name="hitPoint">命中位置。</param>
    /// <param name="hitDirection">命中方向。</param>
    /// <param name="source">伤害来源对象。</param>
    /// <returns>实际造成的伤害。</returns>
    float TakeCombatDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, GameObject source);
}

/// <summary>
/// 战斗伤害接收者查找和应用工具。
/// </summary>
public static class CombatDamageUtility
{
    public static float CalculateDefenseDamageMultiplier(float defense, float minimumMultiplier = 0f)
    {
        float safeDefense = Mathf.Max(0f, defense);
        float defenseMultiplier = 100f / (100f + safeDefense);
        return Mathf.Max(Mathf.Max(0f, minimumMultiplier), defenseMultiplier);
    }

    public static float CalculateDamageTakenMultiplier(
        float defense,
        float additionalMultiplier,
        float minimumMultiplier = 0f)
    {
        float defenseMultiplier = CalculateDefenseDamageMultiplier(defense, 0f);
        float safeAdditionalMultiplier = Mathf.Max(0f, additionalMultiplier);
        return Mathf.Max(Mathf.Max(0f, minimumMultiplier), defenseMultiplier * safeAdditionalMultiplier);
    }

    /// <summary>
    /// 从指定组件及其父节点上查找仍然存活的战斗伤害接收者。
    /// </summary>
    /// <param name="component">命中的组件。</param>
    /// <param name="receiver">成功时返回伤害接收者。</param>
    /// <returns>找到可用接收者时返回 true。</returns>
    public static bool TryGetDamageReceiver(Component component, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (component == null)
        {
            return false;
        }

        if (TryGetAgentHealthReceiver(component.transform, out receiver))
        {
            return true;
        }

        if (TryFindDamageReceiver(component.GetComponentsInParent<MonoBehaviour>(), out receiver))
        {
            return true;
        }

        return TryFindDamageReceiver(component.GetComponentsInChildren<MonoBehaviour>(), out receiver);
    }

    /// <summary>
    /// 从指定 Transform 及其父节点上查找仍然存活的战斗伤害接收者。
    /// </summary>
    /// <param name="target">命中的 Transform。</param>
    /// <param name="receiver">成功时返回伤害接收者。</param>
    /// <returns>找到可用接收者时返回 true。</returns>
    public static bool TryGetDamageReceiver(Transform target, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (target == null)
        {
            return false;
        }

        if (TryGetAgentHealthReceiver(target, out receiver))
        {
            return true;
        }

        if (TryFindDamageReceiver(target.GetComponentsInParent<MonoBehaviour>(), out receiver))
        {
            return true;
        }

        return TryFindDamageReceiver(target.GetComponentsInChildren<MonoBehaviour>(), out receiver);
    }

    private static bool TryFindDamageReceiver(MonoBehaviour[] behaviours, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (behaviours == null)
        {
            return false;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatDamageReceiver candidate &&
                candidate.IsCombatDamageReceiverAlive)
            {
                receiver = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAgentHealthReceiver(Transform target, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (target == null)
        {
            return false;
        }

        Gameplay.Agent.Core.AgentHealthController agentHealth =
            target.GetComponentInParent<Gameplay.Agent.Core.AgentHealthController>();
        if (agentHealth == null)
        {
            agentHealth = target.GetComponentInChildren<Gameplay.Agent.Core.AgentHealthController>();
        }

        if (agentHealth == null || !agentHealth.IsCombatDamageReceiverAlive)
        {
            return false;
        }

        receiver = agentHealth;
        return true;
    }

    /// <summary>
    /// 对指定 Transform 所属的战斗目标造成伤害。
    /// </summary>
    /// <param name="target">目标 Transform。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="hitPoint">命中位置。</param>
    /// <param name="hitDirection">命中方向。</param>
    /// <param name="source">伤害来源对象。</param>
    /// <returns>实际造成的伤害。</returns>
    public static float ApplyDamageTo(
        Transform target,
        float damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        GameObject source)
    {
        return TryGetDamageReceiver(target, out ICombatDamageReceiver receiver)
            ? ApplyDamageTo(receiver, damage, hitPoint, hitDirection, source)
            : 0f;
    }

    /// <summary>
    /// 对指定战斗伤害接收者造成伤害。
    /// </summary>
    /// <param name="receiver">伤害接收者。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="hitPoint">命中位置。</param>
    /// <param name="hitDirection">命中方向。</param>
    /// <param name="source">伤害来源对象。</param>
    /// <returns>实际造成的伤害。</returns>
    public static float ApplyDamageTo(
        ICombatDamageReceiver receiver,
        float damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        GameObject source)
    {
        if (receiver == null || !receiver.IsCombatDamageReceiverAlive)
        {
            return 0f;
        }

        return receiver.TakeCombatDamage(damage, hitPoint, hitDirection, source);
    }
}

/// <summary>
/// 可接收敌人来源群死亡掉落规则的运行时组件。
/// </summary>
public interface IEnemyDeathLootRuleReceiver
{
    /// <summary>
    /// 设置当前敌人死亡时是否允许生成战利品。
    /// </summary>
    /// <param name="canSpawnDeathLoot">允许死亡掉落时为 true。</param>
    void SetCanSpawnDeathLoot(bool canSpawnDeathLoot);
}

/// <summary>
/// 敌人生命控制器。
/// 负责受伤、护盾、血条刷新、死亡和死亡掉落容器生成。
/// </summary>
public class EnemyHealthController : MonoBehaviour, IEnemyDeathLootRuleReceiver
{
    [SerializeField, HideInInspector]
    private EnemyHealthConfigBase _config;

    /// <summary>
    /// 敌人最大生命值，运行时会由敌人配置覆盖。
    /// </summary>
    [HideInInspector] public float MaxHealth = 100f;

    [Header("Health UI")]
    /// <summary>
    /// 世界空间血条填充图。
    /// </summary>
    public Image HealthFillImage;

    [Header("References")]
    /// <summary>
    /// 死亡掉落容器生成点，未配置时使用敌人位置加配置偏移。
    /// </summary>
    public Transform DeathLootSpawnPoint;

    private float _currentHealth;
    private float _currentShield;
    private float _damageTakenMultiplier = 1f;
    private float _baseMaxHealth = 100f;
    private float _maxHealthMultiplier = 1f;
    private bool _hasDied;
    private bool _hasInitializedHealth;
    private bool _canSpawnDeathLoot = true;
    private EnemyDeathLootSettings _deathLootSettings;

    /// <summary>
    /// 敌人当前是否仍可作为战斗目标。
    /// </summary>
    public bool IsAlive
    {
        get
        {
            InitializeHealthIfNeeded();
            return enabled && gameObject.activeInHierarchy && !_hasDied && _currentHealth > 0f;
        }
    }

    private void Awake()
    {
        InitializeHealthIfNeeded();
    }

    private void Start()
    {
        WhiteboxCharacterVisualUtility.ApplyCharacterWhite(gameObject);
        InitializeHealthIfNeeded();
        UpdateHealthBar();
    }

    /// <summary>
    /// 应用敌人生命配置，并重置当前生命。
    /// </summary>
    /// <param name="config">敌人生命配置。</param>
    public void ApplyConfig(EnemyHealthConfigBase config)
    {
        if (config == null)
        {
            Debug.LogError($"[{name}] EnemyHealthController requires an enemy config.", this);
            enabled = false;
            return;
        }

        _config = config;
        ApplyConfigIfAssigned();
        if (!_hasDied)
        {
            _currentHealth = MaxHealth;
            _hasInitializedHealth = true;
            UpdateHealthBar();
        }
    }

    private void ApplyConfigIfAssigned()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] EnemyHealthController requires an enemy config.", this);
            enabled = false;
            return;
        }

        _baseMaxHealth = Mathf.Max(1f, _config.MaxHealth);
        MaxHealth = ResolveScaledMaxHealth();
        _deathLootSettings = _config.DeathLoot;
    }

    /// <summary>
    /// 应用敌人来源群等级带来的最大生命倍率
    /// 该倍率会保留到敌人自身配置写入之后
    /// </summary>
    /// <param name="multiplier"></param>
    public void ApplyMaxHealthMultiplier(float multiplier)
    {
        float previousMaxHealth = Mathf.Max(1f, MaxHealth);
        float previousHealthRatio = _hasInitializedHealth && !_hasDied
            ? Mathf.Clamp01(_currentHealth / previousMaxHealth)
            : 1f;

        _maxHealthMultiplier = Mathf.Max(0.01f, multiplier);
        if (_config != null)
            ApplyConfigIfAssigned();
        else
            MaxHealth = ResolveScaledMaxHealth();

        if (_hasInitializedHealth && !_hasDied)
        {
            _currentHealth = Mathf.Clamp(MaxHealth * previousHealthRatio, 0f, MaxHealth);
            UpdateHealthBar();
        }
    }

    /// <summary>
    /// 应用敌人来源群规则中的死亡掉落开关。
    /// </summary>
    /// <param name="canSpawnDeathLoot">允许死亡掉落时为 true。</param>
    public void SetCanSpawnDeathLoot(bool canSpawnDeathLoot)
    {
        _canSpawnDeathLoot = canSpawnDeathLoot;
    }

    /// <summary>
    /// 对敌人造成伤害。
    /// </summary>
    /// <param name="damageAmount">伤害值。</param>
    public void TakeDamage(float damageAmount)
    {
        TakeDamage(damageAmount, EnemyDamageContext.Empty);
    }

    /// <summary>
    /// 对敌人造成带受击上下文的伤害。
    /// </summary>
    /// <param name="damageAmount">伤害值。</param>
    /// <param name="context">受击上下文。</param>
    public void TakeDamage(float damageAmount, EnemyDamageContext context)
    {
        if (_hasDied)
        {
            return;
        }

        float remainingDamage = damageAmount * Mathf.Max(0f, _damageTakenMultiplier);
        if (_currentShield > 0f)
        {
            float absorbedDamage = Mathf.Min(_currentShield, remainingDamage);
            _currentShield -= absorbedDamage;
            remainingDamage -= absorbedDamage;
        }

        if (remainingDamage <= 0f)
        {
            UpdateHealthBar();
            NotifyDamageReaction(context, damageAmount > 0f && _damageTakenMultiplier > 0f);
            return;
        }

        _currentHealth -= remainingDamage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, MaxHealth);
        UpdateHealthBar();
        NotifyDamageReaction(context, damageAmount > 0f && _damageTakenMultiplier > 0f);

        if (_currentHealth <= 0f)
        {
            NotifyAgentKillCredit(context.Attacker);
            Die();
        }
    }

    private void NotifyDamageReaction(EnemyDamageContext context, bool alertCluster)
    {
        if (context.IsDirectDamage && context.Attacker != null)
        {
            // 本体仍执行原受伤反应，同群接战使用独立通知，不走全局怀疑总线。
            var receivers = ListPool<IEnemyDirectDamageReceiver>.Get();
            try
            {
                GetComponents(receivers);
                for (int i = 0; i < receivers.Count; i++) receivers[i]?.NotifyDirectDamage(context);
            }
            finally { ListPool<IEnemyDirectDamageReceiver>.Release(receivers); }

            // 在 Die 移除成员前通知，致死一击也能唤醒剩余同伴。
            if (alertCluster) EnemyClusterCombatAlert.Notify(this, context.Attacker);

            return;
        }

        EnemySuspicionStimulusBus.ReportEnemyDamaged(transform.position, transform);
    }

    private void NotifyAgentKillCredit(Transform attacker)
    {
        if (attacker == null)
        {
            return;
        }

        Gameplay.Agent.Talent.AgentTalentRuntimeController talentController =
            attacker.GetComponentInParent<Gameplay.Agent.Talent.AgentTalentRuntimeController>();
        if (talentController != null)
        {
            talentController.NotifyEnemyDefeated(this);
        }

        Gameplay.Agent.Progression.AgentLevelProgressionController progressionController =
            attacker.GetComponentInParent<Gameplay.Agent.Progression.AgentLevelProgressionController>();
        if (progressionController != null)
        {
            progressionController.NotifyEnemyDefeated(this);
        }
    }

    /// <summary>
    /// 获取当前血量比例。
    /// </summary>
    /// <returns>当前生命值与最大生命值的比例。</returns>
    public float GetCurrentHealthRatio()
    {
        InitializeHealthIfNeeded();
        if (MaxHealth <= 0f)
        {
            return 0f;
        }

        return _currentHealth / MaxHealth;
    }

    /// <summary>
    /// 为敌人增加临时护盾。
    /// </summary>
    /// <param name="shieldAmount">新增护盾量。</param>
    public void AddShield(float shieldAmount)
    {
        if (_hasDied || shieldAmount <= 0f)
        {
            return;
        }

        _currentShield += shieldAmount;
        UpdateHealthBar();
    }

    /// <summary>
    /// 当前剩余护盾值。
    /// </summary>
    public float CurrentShield => _currentShield;

    /// <summary>
    /// 设置敌人受到伤害时的倍率。
    /// </summary>
    /// <param name="multiplier">伤害倍率。</param>
    public void SetDamageTakenMultiplier(float multiplier)
    {
        _damageTakenMultiplier = Mathf.Max(0f, multiplier);
    }

    // 目标系统可能早于 Start 查询敌人状态，因此血量初始化需要可重入。
    private void InitializeHealthIfNeeded()
    {
        if (_hasInitializedHealth || _hasDied)
        {
            return;
        }

        _baseMaxHealth = Mathf.Max(1f, MaxHealth);
        if (_config != null)
        {
            ApplyConfigIfAssigned();
        }
        else
        {
            MaxHealth = ResolveScaledMaxHealth();
        }

        _currentHealth = MaxHealth;
        _hasInitializedHealth = true;
    }

    private float ResolveScaledMaxHealth()
    {
        return Mathf.Max(1f, _baseMaxHealth * Mathf.Max(0.01f, _maxHealthMultiplier));
    }

    private void UpdateHealthBar()
    {
        if (HealthFillImage != null)
        {
            HealthFillImage.fillAmount = MaxHealth <= 0f ? 0f : Mathf.Clamp01((_currentHealth + _currentShield) / MaxHealth);
        }
    }

    private void Die()
    {
        if (_hasDied)
        {
            return;
        }

        _hasDied = true;
        Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance?.NotifyEnemyDefeated(this);
        RaidFlowController.Instance?.NotifyEnemyKilled(gameObject.name);
        SpawnDeathLootContainer();
        Destroy(gameObject);
    }

    private void SpawnDeathLootContainer()
    {
        if (_deathLootSettings == null ||
            !_canSpawnDeathLoot ||
            !_deathLootSettings.SpawnLootContainerOnDeath ||
            _deathLootSettings.DeathLootContainerPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.position
            : transform.position + _deathLootSettings.DeathLootSpawnOffset;
        Quaternion spawnRotation = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.rotation
            : Quaternion.identity;

        GameObject lootContainerObject = Instantiate(_deathLootSettings.DeathLootContainerPrefab, spawnPosition, spawnRotation);
        WhiteboxCharacterVisualUtility.ApplySolidColor(lootContainerObject, new Color(0.96f, 0.96f, 0.98f, 1f));
    }
}

/// <summary>
/// 敌人运行时状态效果控制器。
/// 负责冰冻、减速和魔法封印，以及对应的移动/脚本禁用和染色反馈。
/// </summary>
[DisallowMultipleComponent]
public class EnemyStatusEffectController : MonoBehaviour
{
    [Header("Visual")]
    /// <summary>
    /// 冰冻状态使用的染色颜色。
    /// </summary>
    public Color FrozenTintColor = new Color(0.58f, 0.86f, 1f, 1f);

    /// <summary>
    /// 魔法封印状态使用的染色颜色。
    /// </summary>
    public Color SealedTintColor = new Color(0.95f, 0.62f, 1f, 1f);

    /// <summary>
    /// 状态染色强度。
    /// </summary>
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

    /// <summary>
    /// 敌人当前是否处于冰冻状态。
    /// </summary>
    public bool IsFrozen => _freezeDurationRemaining > 0f;

    /// <summary>
    /// 敌人当前是否处于魔法封印状态。
    /// </summary>
    public bool IsMagicSealed => _magicSealDurationRemaining > 0f;

    /// <summary>
    /// 当前有效移动速度倍率，没有减速时返回 1。
    /// </summary>
    public float SlowMultiplier => _slowDurationRemaining > 0f ? _slowMultiplier : 1f;

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

    /// <summary>
    /// 对敌人施加或刷新冰冻。
    /// </summary>
    /// <param name="durationSeconds">冰冻持续时间。</param>
    public void ApplyFreeze(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _freezeDurationRemaining = Mathf.Max(_freezeDurationRemaining, durationSeconds);
    }

    /// <summary>
    /// 对敌人施加或刷新减速。
    /// </summary>
    /// <param name="slowMultiplier">移动速度倍率。</param>
    /// <param name="durationSeconds">减速持续时间。</param>
    public void ApplySlow(float slowMultiplier, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _slowDurationRemaining = Mathf.Max(_slowDurationRemaining, durationSeconds);
        _slowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
    }

    /// <summary>
    /// 对敌人施加或刷新魔法封印。
    /// </summary>
    /// <param name="durationSeconds">封印持续时间。</param>
    public void ApplyMagicSeal(float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        _magicSealDurationRemaining = Mathf.Max(_magicSealDurationRemaining, durationSeconds);
    }

    /// <summary>
    /// 立即解除冰冻状态。
    /// </summary>
    public void BreakFreeze()
    {
        _freezeDurationRemaining = 0f;
    }

    /// <summary>
    /// 立即解除减速状态。
    /// </summary>
    public void BreakSlow()
    {
        _slowDurationRemaining = 0f;
        _slowMultiplier = 1f;
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
            bool shouldBlockByFreeze = IsFrozen && !IsFreezeMovementOnlyScript(script);
            bool shouldBlockBySeal = IsMagicSealed && IsMagicSensitiveScript(script);
            bool shouldEnable = defaultEnabled && !shouldBlockByFreeze && !shouldBlockBySeal;

            // 冰冻会暂停大多数行为控制器；魔法封印只暂停依赖技能的敌人脚本。
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

    private static bool IsFreezeMovementOnlyScript(MonoBehaviour script)
    {
        HunterBossBehaviorController hunterBoss = script as HunterBossBehaviorController;
        return hunterBoss != null && hunterBoss.IsForceFieldActive;
    }
}
