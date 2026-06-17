using UnityEngine;

/// <summary>
/// 追猎者 Boss 的移动、近战、漩涡抛锚、怒吼和力场配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_HunterBoss", menuName = "Enemies/Hunter Boss Config")]
public sealed class HunterBossConfig : EnemyHealthConfigBase
{
    [Header("Movement")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the boss starts chasing the player.")]
    private float _detectionRange = 18f;

    [SerializeField, Min(0.1f), Tooltip("Distance at which the boss stops meaningful engagement checks.")]
    private float _loseRange = 24f;

    [SerializeField, Min(0f), Tooltip("Units per second while chasing the player.")]
    private float _chaseSpeed = 3.5f;

    [Header("Melee Anchor Sweep")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the melee sweep can start.")]
    private float _meleeAttackRange = 4f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between melee sweeps.")]
    private float _meleeAttackInterval = 2.4f;

    [SerializeField, Min(0.1f), Tooltip("Radius of the melee sweep damage check.")]
    private float _meleeAttackRadius = 2.2f;

    [SerializeField, Min(0.1f), Tooltip("Total vertical height of the melee sweep damage cylinder.")]
    private float _meleeAttackHeight = 6f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by the melee sweep.")]
    private float _meleeDamage = 18f;

    [SerializeField, Min(0f), Tooltip("Impulse strength applied by the melee sweep.")]
    private float _meleeKnockbackStrength = 6f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the melee visual remains visible.")]
    private float _meleeVisualDuration = 0.22f;

    [Header("Vortex + Anchor Throw")]
    [SerializeField, Tooltip("Projectile prefab thrown after the vortex charge.")]
    private GameObject _anchorProjectilePrefab;

    [SerializeField, Tooltip("Optional vortex field prefab. A runtime default is created if this is empty.")]
    private GameObject _vortexFieldPrefab;

    [SerializeField, Min(0.1f), Tooltip("Distance at which the boss can choose vortex attack.")]
    private float _vortexTriggerDistance = 10f;

    [SerializeField, Min(1), Tooltip("Melee hits required before triggering vortex + anchor throw.")]
    private int _vortexTriggerMeleeCount = 3;

    [SerializeField, Min(0.2f), Tooltip("Radius of the vortex field.")]
    private float _vortexRadius = 3f;

    [SerializeField, Min(0.05f), Tooltip("Seconds spent charging the vortex before throwing.")]
    private float _vortexChargeDuration = 2f;

    [SerializeField, Min(0f), Tooltip("Immobilize duration applied by the armed vortex.")]
    private float _vortexImmobilizeDuration = 3f;

    [SerializeField, Min(0f), Tooltip("Launch speed of the anchor projectile.")]
    private float _anchorThrowSpeed = 14f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by the anchor projectile.")]
    private float _anchorThrowDamage = 16f;

    [SerializeField, Min(0f), Tooltip("Knockback applied by the anchor projectile.")]
    private float _anchorThrowKnockback = 5.5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds before the anchor projectile is destroyed.")]
    private float _anchorProjectileLifeTime = 4f;

    [SerializeField, Min(0.05f), Tooltip("Cooldown after an anchor throw.")]
    private float _anchorThrowCooldown = 3f;

    [Header("Roar")]
    [SerializeField, Range(0f, 1f), Tooltip("Health ratio below which the rage roar triggers once.")]
    private float _rageThreshold = 0.5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds spent charging the roar.")]
    private float _roarChargeDuration = 3f;

    [SerializeField, Min(0.05f), Tooltip("Cooldown after the roar.")]
    private float _roarCooldown = 2f;

    [SerializeField, Min(0.1f), Tooltip("Maximum distance affected by the roar.")]
    private float _roarRange = 12f;

    [SerializeField, Range(1f, 360f), Tooltip("Forward cone angle affected by the roar.")]
    private float _roarAngle = 120f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by roar without cover.")]
    private float _roarDamage = 999f;

    [SerializeField, Tooltip("Layers that block roar damage.")]
    private LayerMask _coverMask;

    [SerializeField, Min(0f), Tooltip("Height offset used when checking cover against the player.")]
    private float _coverCheckHeight = 1.1f;

    [Header("Rage Force Field")]
    [SerializeField, Range(0f, 1f), Tooltip("Shield gained as a fraction of maximum health when rage triggers.")]
    private float _rageShieldMaxHealthRatio = 0.2f;

    [SerializeField, Min(1f), Tooltip("Defense multiplier while the magic force field is active.")]
    private float _forceFieldDefenseMultiplier = 2f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Movement speed multiplier while the force field is frozen by ice magic.")]
    private float _forceFieldFrozenMoveSpeedMultiplier = 0.55f;

    [SerializeField, Min(1f), Tooltip("Fire damage multiplier when breaking a frozen force field.")]
    private float _forceFieldFireDamageMultiplier = 3f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Player move speed multiplier applied by tremble.")]
    private float _trembleMoveSpeedMultiplier = 0.8f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Player attack multiplier applied by tremble.")]
    private float _trembleAttackMultiplier = 0.8f;

    [SerializeField, Min(0f), Tooltip("Seconds tremble debuff lasts after roar hit.")]
    private float _trembleDuration = 3f;

    /// <summary>
    /// Boss 开始追击玩家的距离。
    /// </summary>
    public float DetectionRange => _detectionRange;

    /// <summary>
    /// Boss 放弃追击并停止战斗检查的距离。
    /// </summary>
    public float LoseRange => _loseRange;

    /// <summary>
    /// Boss 追击时使用的移动速度。
    /// </summary>
    public float ChaseSpeed => _chaseSpeed;

    /// <summary>
    /// 近战挥锚可启动的距离。
    /// </summary>
    public float MeleeAttackRange => _meleeAttackRange;

    /// <summary>
    /// 两次近战挥锚之间的间隔。
    /// </summary>
    public float MeleeAttackInterval => _meleeAttackInterval;

    /// <summary>
    /// 近战挥锚的范围检测半径。
    /// </summary>
    public float MeleeAttackRadius => _meleeAttackRadius;

    /// <summary>
    /// 近战挥锚的圆柱检测总高度。
    /// </summary>
    public float MeleeAttackHeight => _meleeAttackHeight;

    /// <summary>
    /// 近战挥锚命中的伤害。
    /// </summary>
    public float MeleeDamage => _meleeDamage;

    /// <summary>
    /// 近战挥锚命中时施加的击退强度。
    /// </summary>
    public float MeleeKnockbackStrength => _meleeKnockbackStrength;

    /// <summary>
    /// 近战挥锚可视效果持续时间。
    /// </summary>
    public float MeleeVisualDuration => _meleeVisualDuration;

    /// <summary>
    /// 漩涡结束后抛出的船锚投射物预制体。
    /// </summary>
    public GameObject AnchorProjectilePrefab => _anchorProjectilePrefab;

    /// <summary>
    /// 漩涡力场预制体，未配置时运行时会创建默认效果。
    /// </summary>
    public GameObject VortexFieldPrefab => _vortexFieldPrefab;

    /// <summary>
    /// Boss 可选择漩涡攻击的距离。
    /// </summary>
    public float VortexTriggerDistance => _vortexTriggerDistance;

    /// <summary>
    /// 触发漩涡攻击前需要累计的近战命中次数。
    /// </summary>
    public int VortexTriggerMeleeCount => _vortexTriggerMeleeCount;

    /// <summary>
    /// 漩涡力场半径。
    /// </summary>
    public float VortexRadius => _vortexRadius;

    /// <summary>
    /// 漩涡蓄力到抛锚前的等待时间。
    /// </summary>
    public float VortexChargeDuration => _vortexChargeDuration;

    /// <summary>
    /// 漩涡武装后对玩家施加的定身时间。
    /// </summary>
    public float VortexImmobilizeDuration => _vortexImmobilizeDuration;

    /// <summary>
    /// 船锚投射物飞行速度。
    /// </summary>
    public float AnchorThrowSpeed => _anchorThrowSpeed;

    /// <summary>
    /// 船锚投射物命中伤害。
    /// </summary>
    public float AnchorThrowDamage => _anchorThrowDamage;

    /// <summary>
    /// 船锚投射物命中击退强度。
    /// </summary>
    public float AnchorThrowKnockback => _anchorThrowKnockback;

    /// <summary>
    /// 船锚投射物自动销毁前的存活时间。
    /// </summary>
    public float AnchorProjectileLifeTime => _anchorProjectileLifeTime;

    /// <summary>
    /// 抛锚结束后的冷却时间。
    /// </summary>
    public float AnchorThrowCooldown => _anchorThrowCooldown;

    /// <summary>
    /// 怒吼阶段触发的生命值比例阈值。
    /// </summary>
    public float RageThreshold => _rageThreshold;

    /// <summary>
    /// 怒吼蓄力时间。
    /// </summary>
    public float RoarChargeDuration => _roarChargeDuration;

    /// <summary>
    /// 怒吼结束后的冷却时间。
    /// </summary>
    public float RoarCooldown => _roarCooldown;

    /// <summary>
    /// 怒吼影响的最大距离。
    /// </summary>
    public float RoarRange => _roarRange;

    /// <summary>
    /// 怒吼影响的正前方扇形角度。
    /// </summary>
    public float RoarAngle => _roarAngle;

    /// <summary>
    /// 玩家未被掩体保护时受到的怒吼伤害。
    /// </summary>
    public float RoarDamage => _roarDamage;

    /// <summary>
    /// 怒吼遮挡检测使用的掩体层。
    /// </summary>
    public LayerMask CoverMask => _coverMask;

    /// <summary>
    /// 怒吼掩体检测时采样玩家高度。
    /// </summary>
    public float CoverCheckHeight => _coverCheckHeight;

    /// <summary>
    /// 怒吼触发时按最大生命值比例获得的护盾量。
    /// </summary>
    public float RageShieldMaxHealthRatio => _rageShieldMaxHealthRatio;

    /// <summary>
    /// 力场激活期间受到伤害的防御倍率。
    /// </summary>
    public float ForceFieldDefenseMultiplier => _forceFieldDefenseMultiplier;

    /// <summary>
    /// 力场被冰系冻结时 Boss 的移动速度倍率。
    /// </summary>
    public float ForceFieldFrozenMoveSpeedMultiplier => _forceFieldFrozenMoveSpeedMultiplier;

    /// <summary>
    /// 火系打破冻结力场时的伤害倍率。
    /// </summary>
    public float ForceFieldFireDamageMultiplier => _forceFieldFireDamageMultiplier;

    /// <summary>
    /// 怒吼震慑给玩家施加的移动速度倍率。
    /// </summary>
    public float TrembleMoveSpeedMultiplier => _trembleMoveSpeedMultiplier;

    /// <summary>
    /// 怒吼震慑给玩家施加的攻击倍率。
    /// </summary>
    public float TrembleAttackMultiplier => _trembleAttackMultiplier;

    /// <summary>
    /// 怒吼震慑持续时间。
    /// </summary>
    public float TrembleDuration => _trembleDuration;

    /// <summary>
    /// 校验 Boss 全部技能参数，保证状态机读取时数值一致。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _loseRange = Mathf.Max(_detectionRange, _loseRange);
        _chaseSpeed = Mathf.Max(0f, _chaseSpeed);
        _meleeAttackRange = Mathf.Max(0.1f, _meleeAttackRange);
        _meleeAttackInterval = Mathf.Max(0.05f, _meleeAttackInterval);
        _meleeAttackRadius = Mathf.Max(0.1f, _meleeAttackRadius);
        _meleeAttackHeight = Mathf.Max(0.1f, _meleeAttackHeight);
        _meleeDamage = Mathf.Max(0f, _meleeDamage);
        _meleeKnockbackStrength = Mathf.Max(0f, _meleeKnockbackStrength);
        _meleeVisualDuration = Mathf.Max(0.05f, _meleeVisualDuration);
        _vortexTriggerDistance = Mathf.Max(_meleeAttackRange, _vortexTriggerDistance);
        _vortexTriggerMeleeCount = Mathf.Max(1, _vortexTriggerMeleeCount);
        _vortexRadius = Mathf.Max(0.2f, _vortexRadius);
        _vortexChargeDuration = Mathf.Max(0.05f, _vortexChargeDuration);
        _vortexImmobilizeDuration = Mathf.Max(0f, _vortexImmobilizeDuration);
        _anchorThrowSpeed = Mathf.Max(0f, _anchorThrowSpeed);
        _anchorThrowDamage = Mathf.Max(0f, _anchorThrowDamage);
        _anchorThrowKnockback = Mathf.Max(0f, _anchorThrowKnockback);
        _anchorProjectileLifeTime = Mathf.Max(0.05f, _anchorProjectileLifeTime);
        _anchorThrowCooldown = Mathf.Max(0.05f, _anchorThrowCooldown);
        _rageThreshold = Mathf.Clamp01(_rageThreshold);
        _roarChargeDuration = Mathf.Max(0.05f, _roarChargeDuration);
        _roarCooldown = Mathf.Max(0.05f, _roarCooldown);
        _roarRange = Mathf.Max(0.1f, _roarRange);
        _roarAngle = Mathf.Clamp(_roarAngle, 1f, 360f);
        _roarDamage = Mathf.Max(0f, _roarDamage);
        _coverCheckHeight = Mathf.Max(0f, _coverCheckHeight);
        _rageShieldMaxHealthRatio = Mathf.Clamp01(_rageShieldMaxHealthRatio);
        _forceFieldDefenseMultiplier = Mathf.Max(1f, _forceFieldDefenseMultiplier);
        _forceFieldFrozenMoveSpeedMultiplier = Mathf.Clamp(_forceFieldFrozenMoveSpeedMultiplier, 0.1f, 1f);
        _forceFieldFireDamageMultiplier = Mathf.Max(1f, _forceFieldFireDamageMultiplier);
        _trembleMoveSpeedMultiplier = Mathf.Clamp(_trembleMoveSpeedMultiplier, 0.1f, 1f);
        _trembleAttackMultiplier = Mathf.Clamp(_trembleAttackMultiplier, 0.1f, 1f);
        _trembleDuration = Mathf.Max(0f, _trembleDuration);
    }
}
