using UnityEngine;

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
    private float _roarCooldown = 7f;

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

    public float DetectionRange => _detectionRange;
    public float LoseRange => _loseRange;
    public float ChaseSpeed => _chaseSpeed;
    public float MeleeAttackRange => _meleeAttackRange;
    public float MeleeAttackInterval => _meleeAttackInterval;
    public float MeleeAttackRadius => _meleeAttackRadius;
    public float MeleeDamage => _meleeDamage;
    public float MeleeKnockbackStrength => _meleeKnockbackStrength;
    public float MeleeVisualDuration => _meleeVisualDuration;
    public GameObject AnchorProjectilePrefab => _anchorProjectilePrefab;
    public GameObject VortexFieldPrefab => _vortexFieldPrefab;
    public float VortexTriggerDistance => _vortexTriggerDistance;
    public int VortexTriggerMeleeCount => _vortexTriggerMeleeCount;
    public float VortexRadius => _vortexRadius;
    public float VortexChargeDuration => _vortexChargeDuration;
    public float VortexImmobilizeDuration => _vortexImmobilizeDuration;
    public float AnchorThrowSpeed => _anchorThrowSpeed;
    public float AnchorThrowDamage => _anchorThrowDamage;
    public float AnchorThrowKnockback => _anchorThrowKnockback;
    public float AnchorProjectileLifeTime => _anchorProjectileLifeTime;
    public float AnchorThrowCooldown => _anchorThrowCooldown;
    public float RageThreshold => _rageThreshold;
    public float RoarChargeDuration => _roarChargeDuration;
    public float RoarCooldown => _roarCooldown;
    public float RoarRange => _roarRange;
    public float RoarAngle => _roarAngle;
    public float RoarDamage => _roarDamage;
    public LayerMask CoverMask => _coverMask;
    public float CoverCheckHeight => _coverCheckHeight;
    public float RageShieldMaxHealthRatio => _rageShieldMaxHealthRatio;
    public float ForceFieldDefenseMultiplier => _forceFieldDefenseMultiplier;
    public float ForceFieldFrozenMoveSpeedMultiplier => _forceFieldFrozenMoveSpeedMultiplier;
    public float ForceFieldFireDamageMultiplier => _forceFieldFireDamageMultiplier;
    public float TrembleMoveSpeedMultiplier => _trembleMoveSpeedMultiplier;
    public float TrembleAttackMultiplier => _trembleAttackMultiplier;
    public float TrembleDuration => _trembleDuration;

    protected override void OnValidate()
    {
        base.OnValidate();
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _loseRange = Mathf.Max(_detectionRange, _loseRange);
        _chaseSpeed = Mathf.Max(0f, _chaseSpeed);
        _meleeAttackRange = Mathf.Max(0.1f, _meleeAttackRange);
        _meleeAttackInterval = Mathf.Max(0.05f, _meleeAttackInterval);
        _meleeAttackRadius = Mathf.Max(0.1f, _meleeAttackRadius);
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
