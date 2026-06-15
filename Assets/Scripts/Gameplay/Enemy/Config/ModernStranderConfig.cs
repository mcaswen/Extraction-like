using UnityEngine;

/// <summary>
/// 现代搁浅者的触手、腐蚀和黏液池技能配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_ModernStrander", menuName = "Enemies/Modern Strander Config")]
public sealed class ModernStranderConfig : EnemyPatrolConfigBase
{
    [Header("Tentacle Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the tentacle attack can start.")]
    private float _attackRange = 3.2f;

    [SerializeField, Min(0.1f), Tooltip("Maximum distance for the immediate tentacle counterattack after taking direct player damage.")]
    private float _directDamageCounterAttackRange = 10.5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between tentacle attacks.")]
    private float _attackInterval = 5f;

    [SerializeField, Min(0.05f), Tooltip("Maximum seconds a tentacle strike or latch remains active.")]
    private float _tentacleLatchDuration = 1.1f;

    [SerializeField, Min(0.1f), Tooltip("Width of the generated tentacle hitbox.")]
    private float _tentacleHitboxWidth = 0.55f;

    [SerializeField, Min(0.1f), Tooltip("Height of the generated tentacle hitbox.")]
    private float _tentacleHitboxHeight = 0.55f;

    [SerializeField, Min(0f), Tooltip("Direct pull speed in world units per second applied during a latch.")]
    private float _latchPullStrength = 2.5f;

    [SerializeField, Min(0f), Tooltip("Corrosion damage per second applied during a latch.")]
    private float _corrosionDamagePerSecond = 10f;

    [SerializeField, Min(0f), Tooltip("Corrosion status duration applied during a latch.")]
    private float _corrosionDuration = 2.5f;

    [SerializeField, Min(0.05f), Tooltip("Corrosion tick interval applied during a latch.")]
    private float _corrosionTickInterval = 0.25f;

    [SerializeField, Min(0f), Tooltip("Immediate damage dealt on first tentacle contact.")]
    private float _initialContactDamage = 6f;

    [Header("Corrosive Slime")]
    [SerializeField, Tooltip("Optional prefab for the corrosive puddle spawned after a successful latch.")]
    private GameObject _corrosivePuddlePrefab;

    [SerializeField, Min(0.1f), Tooltip("Seconds before the corrosive puddle is destroyed.")]
    private float _puddleLifetime = 5f;

    [SerializeField, Min(0.2f), Tooltip("Radius of the corrosive puddle trigger.")]
    private float _puddleRadius = 1.1f;

    [SerializeField, Min(0f), Tooltip("Damage per second applied by the corrosive puddle.")]
    private float _puddleDamagePerSecond = 6f;

    [SerializeField, Min(0f), Tooltip("Corrosion duration applied by the corrosive puddle.")]
    private float _puddleCorrosionDuration = 1.8f;

    [SerializeField, Min(0.05f), Tooltip("Corrosion tick interval applied by the corrosive puddle.")]
    private float _puddleTickInterval = 0.25f;

    /// <summary>
    /// 触手攻击可启动的距离。
    /// </summary>
    public float AttackRange => _attackRange;

    public float DirectDamageCounterAttackRange => _directDamageCounterAttackRange;

    /// <summary>
    /// 两次触手攻击之间的最短间隔。
    /// </summary>
    public float AttackInterval => _attackInterval;

    /// <summary>
    /// 触手命中盒或吸附状态可持续的最长时间。
    /// </summary>
    public float TentacleLatchDuration => _tentacleLatchDuration;

    /// <summary>
    /// 动态触手命中盒的宽度。
    /// </summary>
    public float TentacleHitboxWidth => _tentacleHitboxWidth;

    /// <summary>
    /// 动态触手命中盒的高度。
    /// </summary>
    public float TentacleHitboxHeight => _tentacleHitboxHeight;

    /// <summary>
    /// 触手吸附目标时每秒直接拉拽的世界距离。
    /// </summary>
    public float LatchPullStrength => _latchPullStrength;

    /// <summary>
    /// 触手吸附期间施加的腐蚀每秒伤害。
    /// </summary>
    public float CorrosionDamagePerSecond => _corrosionDamagePerSecond;

    /// <summary>
    /// 触手吸附命中后附加腐蚀状态的持续时间。
    /// </summary>
    public float CorrosionDuration => _corrosionDuration;

    /// <summary>
    /// 腐蚀伤害的结算间隔。
    /// </summary>
    public float CorrosionTickInterval => _corrosionTickInterval;

    /// <summary>
    /// 触手首次接触目标时造成的即时伤害。
    /// </summary>
    public float InitialContactDamage => _initialContactDamage;

    /// <summary>
    /// 触手成功吸附后生成的腐蚀黏液池预制体。
    /// </summary>
    public GameObject CorrosivePuddlePrefab => _corrosivePuddlePrefab;

    /// <summary>
    /// 腐蚀黏液池的存活时间。
    /// </summary>
    public float PuddleLifetime => _puddleLifetime;

    /// <summary>
    /// 腐蚀黏液池触发器半径。
    /// </summary>
    public float PuddleRadius => _puddleRadius;

    /// <summary>
    /// 腐蚀黏液池每秒造成的伤害。
    /// </summary>
    public float PuddleDamagePerSecond => _puddleDamagePerSecond;

    /// <summary>
    /// 黏液池施加腐蚀状态的持续时间。
    /// </summary>
    public float PuddleCorrosionDuration => _puddleCorrosionDuration;

    /// <summary>
    /// 黏液池腐蚀伤害的结算间隔。
    /// </summary>
    public float PuddleTickInterval => _puddleTickInterval;

    /// <summary>
    /// 校验现代搁浅者技能参数，确保运行时生成命中盒和黏液池时有合法尺寸。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _directDamageCounterAttackRange = Mathf.Max(_attackRange, _directDamageCounterAttackRange);
        _attackInterval = Mathf.Max(0.05f, _attackInterval);
        _tentacleLatchDuration = Mathf.Max(0.05f, _tentacleLatchDuration);
        _tentacleHitboxWidth = Mathf.Max(0.1f, _tentacleHitboxWidth);
        _tentacleHitboxHeight = Mathf.Max(0.1f, _tentacleHitboxHeight);
        _latchPullStrength = Mathf.Max(0f, _latchPullStrength);
        _corrosionDamagePerSecond = Mathf.Max(0f, _corrosionDamagePerSecond);
        _corrosionDuration = Mathf.Max(0f, _corrosionDuration);
        _corrosionTickInterval = Mathf.Max(0.05f, _corrosionTickInterval);
        _initialContactDamage = Mathf.Max(0f, _initialContactDamage);
        _puddleLifetime = Mathf.Max(0.1f, _puddleLifetime);
        _puddleRadius = Mathf.Max(0.2f, _puddleRadius);
        _puddleDamagePerSecond = Mathf.Max(0f, _puddleDamagePerSecond);
        _puddleCorrosionDuration = Mathf.Max(0f, _puddleCorrosionDuration);
        _puddleTickInterval = Mathf.Max(0.05f, _puddleTickInterval);
    }
}
