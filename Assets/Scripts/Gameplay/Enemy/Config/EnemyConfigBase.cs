using System;
using UnityEngine;

/// <summary>
/// 敌人配置基类，保存敌人通用的稳定 ID 和设计器显示名称。
/// </summary>
public abstract class EnemyConfigBase : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("Stable designer-facing identifier for this enemy config.")]
    private string _enemyId = "enemy";

    [SerializeField, Tooltip("Display name used by designers to identify this enemy config.")]
    private string _displayName = "Enemy";

    /// <summary>
    /// 敌人配置的稳定标识，用于工具或表格中识别具体敌人类型。
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 设计器可读的敌人显示名。
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 在 Inspector 修改时补齐缺省身份字段，避免空 ID 进入运行时。
    /// </summary>
    protected virtual void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_enemyId))
        {
            _enemyId = name;
        }

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            _displayName = _enemyId;
        }
    }
}

/// <summary>
/// 带生命值和死亡掉落配置的敌人配置基类。
/// </summary>
public abstract class EnemyHealthConfigBase : EnemyConfigBase
{
    [Header("Attributes")]
    [SerializeField, Min(1f), Tooltip("Maximum health for this enemy.")]
    private float _maxHealth = 100f;

    [Header("Death Loot")]
    [SerializeField, Tooltip("Death loot settings owned by this enemy config.")]
    private EnemyDeathLootSettings _deathLoot = new EnemyDeathLootSettings();

    /// <summary>
    /// 敌人的最大生命值。
    /// </summary>
    public float MaxHealth => _maxHealth;

    /// <summary>
    /// 敌人死亡时使用的掉落容器设置。
    /// </summary>
    public EnemyDeathLootSettings DeathLoot => _deathLoot;

    /// <summary>
    /// 校验生命值和死亡掉落配置，保证运行时读取到的是合法范围。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _maxHealth = Mathf.Max(1f, _maxHealth);
        _deathLoot ??= new EnemyDeathLootSettings();
        _deathLoot.Validate();
    }
}

/// <summary>
/// 带巡逻和侦测参数的敌人配置基类。
/// </summary>
public abstract class EnemyPatrolConfigBase : EnemyHealthConfigBase
{
    [Header("Patrol")]
    [SerializeField, Tooltip("Current random-radius patrol settings.")]
    private EnemyPatrolSettings _patrol = new EnemyPatrolSettings();

    [Header("Detection")]
    [SerializeField, Tooltip("Player detection and disengage settings.")]
    private EnemyDetectionSettings _detection = new EnemyDetectionSettings();

    /// <summary>
    /// 巡逻模式、范围和等待时间设置。
    /// </summary>
    public EnemyPatrolSettings Patrol => _patrol;

    /// <summary>
    /// 视野、听觉预设和脱战距离设置。
    /// </summary>
    public EnemyDetectionSettings Detection => _detection;

    /// <summary>
    /// 校验巡逻和侦测子配置，防止空引用和非法数值。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _patrol ??= new EnemyPatrolSettings();
        _detection ??= new EnemyDetectionSettings();
        _patrol.Validate();
        _detection.Validate();
    }
}

/// <summary>
/// 敌人巡逻点选择模式。
/// </summary>
public enum EnemyPatrolMode
{
    RandomRadius,
    FixedRoute
}

/// <summary>
/// 敌人巡逻阶段可使用的感知复杂度预设。
/// </summary>
public enum EnemyAwarenessPreset
{
    SimpleVisionOnly,
    VisionWithSearch,
    FullSuspicion
}

/// <summary>
/// 敌人感知预设的判定工具。
/// </summary>
public static class EnemyAwarenessPresetUtility
{
    /// <summary>
    /// 判断该预设是否启用巡逻阶段的怀疑、调查和搜索逻辑。
    /// </summary>
    /// <param name="preset">感知预设。</param>
    /// <returns>启用巡逻感知逻辑时返回 true。</returns>
    public static bool UsesPatrolAwareness(this EnemyAwarenessPreset preset)
    {
        return preset != EnemyAwarenessPreset.SimpleVisionOnly;
    }

    /// <summary>
    /// 判断该预设是否允许敌人在丢失玩家后记录最后目击位置。
    /// </summary>
    /// <param name="preset">感知预设。</param>
    /// <returns>允许报告玩家最后目击位置时返回 true。</returns>
    public static bool ShouldReportPlayerLastSeen(this EnemyAwarenessPreset preset)
    {
        return preset != EnemyAwarenessPreset.SimpleVisionOnly;
    }

    /// <summary>
    /// 判断指定怀疑记录是否能被当前预设消费。
    /// </summary>
    /// <param name="preset">感知预设。</param>
    /// <param name="record">待处理的怀疑记录。</param>
    /// <returns>该记录符合预设规则时返回 true。</returns>
    public static bool AllowsSuspicionRecord(this EnemyAwarenessPreset preset, EnemySuspicionRecord record)
    {
        switch (preset)
        {
            case EnemyAwarenessPreset.SimpleVisionOnly:
                return false;
            case EnemyAwarenessPreset.VisionWithSearch:
                return record.Type == EnemySuspicionStimulusType.PlayerLastSeen;
            default:
                return true;
        }
    }
}

/// <summary>
/// 敌人巡逻参数，定义随机巡逻半径、固定路线模式和到点等待时间。
/// </summary>
[Serializable]
public sealed class EnemyPatrolSettings
{
    [SerializeField, Tooltip("How this enemy chooses patrol destinations.")]
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;

    [SerializeField, Min(0.1f), Tooltip("Radius around the spawn position used for random patrol points.")]
    private float _patrolRadius = 10f;

    [SerializeField, Min(0f), Tooltip("Seconds to wait after reaching a patrol point.")]
    private float _patrolWaitTime = 2f;

    /// <summary>
    /// 当前巡逻点选择模式。
    /// </summary>
    public EnemyPatrolMode PatrolMode => _patrolMode;

    /// <summary>
    /// 随机巡逻时以出生点为中心的采样半径。
    /// </summary>
    public float PatrolRadius => _patrolRadius;

    /// <summary>
    /// 到达巡逻点后的默认等待时长。
    /// </summary>
    public float PatrolWaitTime => _patrolWaitTime;

    /// <summary>
    /// 修正巡逻参数的最小值。
    /// </summary>
    public void Validate()
    {
        _patrolRadius = Mathf.Max(0.1f, _patrolRadius);
        _patrolWaitTime = Mathf.Max(0f, _patrolWaitTime);
    }
}

/// <summary>
/// 敌人侦测参数，封装视野角、视距、遮挡层和脱战距离。
/// </summary>
[Serializable]
public sealed class EnemyDetectionSettings
{
    [SerializeField, Tooltip("High-level patrol awareness behavior exposed to designers.")]
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;

    [SerializeField, Min(0.1f), Tooltip("View range at which this enemy can start chasing or attacking the player.")]
    private float _detectionRange = 15f;

    [SerializeField, Range(1f, 360f), Tooltip("Horizontal view angle used while patrolling.")]
    private float _viewAngle = 112f;

    [SerializeField, Tooltip("Layers that block line of sight between this enemy and the player.")]
    private LayerMask _lineOfSightBlockMask = 1;

    [SerializeField, Tooltip("Layers sampled by the gameplay vision overlay when fitting the visible area to the ground.")]
    private LayerMask _groundMask = 1;

    [SerializeField, Min(0f), Tooltip("Height offset used as this enemy's line-of-sight origin.")]
    private float _eyeHeight = 1.2f;

    [SerializeField, Min(0f), Tooltip("Height offset used as the player's line-of-sight target.")]
    private float _targetHeight = 1f;

    [SerializeField, Min(0.1f), Tooltip("Distance at which this enemy stops engaging and returns to patrol.")]
    private float _loseRange = 20f;

    /// <summary>
    /// 巡逻感知复杂度预设。
    /// </summary>
    public EnemyAwarenessPreset AwarenessPreset => _awarenessPreset;

    /// <summary>
    /// 敌人能发现目标的最大距离。
    /// </summary>
    public float DetectionRange => _detectionRange;

    /// <summary>
    /// 敌人巡逻视野的水平角度。
    /// </summary>
    public float ViewAngle => _viewAngle;

    /// <summary>
    /// 视线检测时会阻挡敌人视线的层。
    /// </summary>
    public LayerMask LineOfSightBlockMask => _lineOfSightBlockMask;

    /// <summary>
    /// 视野可视化投影到地面时使用的层。
    /// </summary>
    public LayerMask GroundMask => _groundMask;

    /// <summary>
    /// 敌人视线起点相对根节点的高度。
    /// </summary>
    public float EyeHeight => _eyeHeight;

    /// <summary>
    /// 目标被检测点相对根节点的高度。
    /// </summary>
    public float TargetHeight => _targetHeight;

    /// <summary>
    /// 目标超过该距离后敌人会脱战回到巡逻。
    /// </summary>
    public float LoseRange => _loseRange;

    /// <summary>
    /// 修正侦测参数，保证视距、角度和脱战距离处于合法范围。
    /// </summary>
    public void Validate()
    {
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _viewAngle = Mathf.Clamp(_viewAngle, 1f, 360f);
        _eyeHeight = Mathf.Max(0f, _eyeHeight);
        _targetHeight = Mathf.Max(0f, _targetHeight);
        _loseRange = Mathf.Max(_detectionRange, _loseRange);
    }
}

/// <summary>
/// 敌人死亡后生成战利品容器的配置。
/// </summary>
[Serializable]
public sealed class EnemyDeathLootSettings
{
    [SerializeField, Tooltip("Whether this enemy should spawn a loot container when defeated.")]
    private bool _spawnLootContainerOnDeath = true;

    [SerializeField, Tooltip("Loot container prefab spawned when this enemy is defeated.")]
    private GameObject _deathLootContainerPrefab;

    [SerializeField, Tooltip("Fallback spawn offset when no death loot spawn point is assigned on the prefab.")]
    private Vector3 _deathLootSpawnOffset = new Vector3(0f, 0.1f, 0f);

    /// <summary>
    /// 敌人死亡时是否生成战利品容器。
    /// </summary>
    public bool SpawnLootContainerOnDeath => _spawnLootContainerOnDeath;

    /// <summary>
    /// 死亡时生成的战利品容器预制体。
    /// </summary>
    public GameObject DeathLootContainerPrefab => _deathLootContainerPrefab;

    /// <summary>
    /// 未配置专用掉落点时使用的生成偏移。
    /// </summary>
    public Vector3 DeathLootSpawnOffset => _deathLootSpawnOffset;

    /// <summary>
    /// 预留的死亡掉落设置校验入口。
    /// </summary>
    public void Validate()
    {
    }
}
