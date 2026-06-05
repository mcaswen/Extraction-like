using UnityEngine;

/// <summary>
/// 潮汐畸变体的近战电击和远程水柱技能配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_TidalAberration", menuName = "Enemies/Tidal Aberration Config")]
public sealed class TidalAberrationConfig : EnemyPatrolConfigBase
{
    [Header("Melee Electric Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the electric melee attack can start.")]
    private float _meleeAttackRange = 3f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between electric melee attacks.")]
    private float _meleeAttackInterval = 2.2f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the melee latch remains active.")]
    private float _meleeLatchDuration = 0.8f;

    [SerializeField, Min(0f), Tooltip("Immediate damage dealt by the melee contact.")]
    private float _meleeContactDamage = 10f;

    [SerializeField, Min(0f), Tooltip("Seconds the player cannot cast magic after melee contact.")]
    private float _silenceDuration = 1.5f;

    [SerializeField, Min(0f), Tooltip("Damage per second dealt while the melee latch is active.")]
    private float _electricTickDamagePerSecond = 4f;

    [SerializeField, Min(0.05f), Tooltip("Tick interval for electric latch damage.")]
    private float _electricTickInterval = 0.25f;

    [Header("Ranged Water Jet")]
    [SerializeField, Min(0.1f), Tooltip("Minimum distance required before the enemy uses water jet.")]
    private float _minimumRangedDistance = 4.5f;

    [SerializeField, Min(0.1f), Tooltip("Maximum distance at which water jet can be used.")]
    private float _rangedAttackRange = 9f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between water jet attacks.")]
    private float _rangedAttackInterval = 8f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the water jet visual/cast remains active.")]
    private float _waterJetDuration = 0.18f;

    [SerializeField, Min(0f), Tooltip("Damage dealt when water jet hits the player.")]
    private float _waterJetDamage = 14f;

    [SerializeField, Min(0f), Tooltip("Impulse strength applied when water jet hits the player.")]
    private float _waterJetKnockbackStrength = 5.2f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Move speed multiplier applied to the player after water jet knockback.")]
    private float _knockbackMoveSpeedMultiplier = 0.5f;

    [SerializeField, Min(0f), Tooltip("Seconds the knockback slow debuff lasts after water jet hit.")]
    private float _knockbackSlowDuration = 1f;

    [SerializeField, Min(0.1f), Tooltip("Raycast distance used by water jet.")]
    private float _waterJetMaxDistance = 10f;

    /// <summary>
    /// 近战电击可启动的距离。
    /// </summary>
    public float MeleeAttackRange => _meleeAttackRange;

    /// <summary>
    /// 两次近战电击之间的间隔。
    /// </summary>
    public float MeleeAttackInterval => _meleeAttackInterval;

    /// <summary>
    /// 电击吸附状态的持续时间。
    /// </summary>
    public float MeleeLatchDuration => _meleeLatchDuration;

    /// <summary>
    /// 近战接触造成的即时伤害。
    /// </summary>
    public float MeleeContactDamage => _meleeContactDamage;

    /// <summary>
    /// 电击命中后让玩家无法施法的时间。
    /// </summary>
    public float SilenceDuration => _silenceDuration;

    /// <summary>
    /// 电击吸附期间每秒造成的伤害。
    /// </summary>
    public float ElectricTickDamagePerSecond => _electricTickDamagePerSecond;

    /// <summary>
    /// 电击吸附伤害的结算间隔。
    /// </summary>
    public float ElectricTickInterval => _electricTickInterval;

    /// <summary>
    /// 允许使用远程水柱的最小距离。
    /// </summary>
    public float MinimumRangedDistance => _minimumRangedDistance;

    /// <summary>
    /// 远程水柱可命中的最大距离。
    /// </summary>
    public float RangedAttackRange => _rangedAttackRange;

    /// <summary>
    /// 两次水柱攻击之间的间隔。
    /// </summary>
    public float RangedAttackInterval => _rangedAttackInterval;

    /// <summary>
    /// 水柱射线和视觉效果的持续时间。
    /// </summary>
    public float WaterJetDuration => _waterJetDuration;

    /// <summary>
    /// 水柱命中时造成的伤害。
    /// </summary>
    public float WaterJetDamage => _waterJetDamage;

    /// <summary>
    /// 水柱命中时施加的击退强度。
    /// </summary>
    public float WaterJetKnockbackStrength => _waterJetKnockbackStrength;

    /// <summary>
    /// 水柱击退后施加给玩家的移动速度倍率。
    /// </summary>
    public float KnockbackMoveSpeedMultiplier => _knockbackMoveSpeedMultiplier;

    /// <summary>
    /// 水柱击退后的减速持续时间。
    /// </summary>
    public float KnockbackSlowDuration => _knockbackSlowDuration;

    /// <summary>
    /// 水柱射线检测的最大距离。
    /// </summary>
    public float WaterJetMaxDistance => _waterJetMaxDistance;

    /// <summary>
    /// 校验潮汐畸变体的近战和远程攻击参数。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _meleeAttackRange = Mathf.Max(0.1f, _meleeAttackRange);
        _meleeAttackInterval = Mathf.Max(0.05f, _meleeAttackInterval);
        _meleeLatchDuration = Mathf.Max(0.05f, _meleeLatchDuration);
        _meleeContactDamage = Mathf.Max(0f, _meleeContactDamage);
        _silenceDuration = Mathf.Max(0f, _silenceDuration);
        _electricTickDamagePerSecond = Mathf.Max(0f, _electricTickDamagePerSecond);
        _electricTickInterval = Mathf.Max(0.05f, _electricTickInterval);
        _minimumRangedDistance = Mathf.Max(_meleeAttackRange + 0.1f, _minimumRangedDistance);
        _rangedAttackRange = Mathf.Max(_minimumRangedDistance, _rangedAttackRange);
        _rangedAttackInterval = Mathf.Max(0.05f, _rangedAttackInterval);
        _waterJetDuration = Mathf.Max(0.05f, _waterJetDuration);
        _waterJetDamage = Mathf.Max(0f, _waterJetDamage);
        _waterJetKnockbackStrength = Mathf.Max(0f, _waterJetKnockbackStrength);
        _knockbackMoveSpeedMultiplier = Mathf.Clamp(_knockbackMoveSpeedMultiplier, 0.1f, 1f);
        _knockbackSlowDuration = Mathf.Max(0f, _knockbackSlowDuration);
        _waterJetMaxDistance = Mathf.Max(0.1f, _waterJetMaxDistance);
    }
}
