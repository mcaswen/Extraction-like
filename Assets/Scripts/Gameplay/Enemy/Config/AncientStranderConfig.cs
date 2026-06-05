using UnityEngine;

/// <summary>
/// 古代搁浅者的鱼骨横扫和远程撕咬配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_AncientStrander", menuName = "Enemies/Ancient Strander Config")]
public sealed class AncientStranderConfig : EnemyPatrolConfigBase
{
    [Header("Melee Fishbone Sweep")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the melee sweep can start.")]
    private float _meleeAttackRange = 2.8f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between melee sweeps.")]
    private float _meleeAttackInterval = 1.8f;

    [SerializeField, Min(0.1f), Tooltip("Radius of the melee sweep damage check.")]
    private float _meleeAttackRadius = 1.9f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by the melee sweep.")]
    private float _meleeDamage = 12f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the melee visual remains visible.")]
    private float _meleeVisualDuration = 0.2f;

    [Header("Ranged Fishbone Bite")]
    [SerializeField, Min(0.1f), Tooltip("Minimum distance required before the enemy uses the bite attack.")]
    private float _minimumRangedDistance = 3.4f;

    [SerializeField, Min(0.1f), Tooltip("Maximum distance at which the bite attack can be used.")]
    private float _rangedAttackRange = 7.6f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between bite attacks.")]
    private float _rangedAttackInterval = 2.4f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the bite hitbox remains active.")]
    private float _biteStrikeDuration = 0.42f;

    [SerializeField, Min(0.1f), Tooltip("Width of the generated bite hitbox.")]
    private float _biteHitboxWidth = 0.42f;

    [SerializeField, Min(0.1f), Tooltip("Height of the generated bite hitbox.")]
    private float _biteHitboxHeight = 0.42f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by the bite hitbox.")]
    private float _biteDamage = 15f;

    /// <summary>
    /// 鱼骨横扫可启动的距离。
    /// </summary>
    public float MeleeAttackRange => _meleeAttackRange;

    /// <summary>
    /// 两次鱼骨横扫之间的间隔。
    /// </summary>
    public float MeleeAttackInterval => _meleeAttackInterval;

    /// <summary>
    /// 鱼骨横扫的范围检测半径。
    /// </summary>
    public float MeleeAttackRadius => _meleeAttackRadius;

    /// <summary>
    /// 鱼骨横扫命中伤害。
    /// </summary>
    public float MeleeDamage => _meleeDamage;

    /// <summary>
    /// 鱼骨横扫可视效果的持续时间。
    /// </summary>
    public float MeleeVisualDuration => _meleeVisualDuration;

    /// <summary>
    /// 允许使用远程撕咬的最小距离。
    /// </summary>
    public float MinimumRangedDistance => _minimumRangedDistance;

    /// <summary>
    /// 远程撕咬可启动的最大距离。
    /// </summary>
    public float RangedAttackRange => _rangedAttackRange;

    /// <summary>
    /// 两次远程撕咬之间的间隔。
    /// </summary>
    public float RangedAttackInterval => _rangedAttackInterval;

    /// <summary>
    /// 撕咬命中盒保持激活的时间。
    /// </summary>
    public float BiteStrikeDuration => _biteStrikeDuration;

    /// <summary>
    /// 撕咬命中盒的宽度。
    /// </summary>
    public float BiteHitboxWidth => _biteHitboxWidth;

    /// <summary>
    /// 撕咬命中盒的高度。
    /// </summary>
    public float BiteHitboxHeight => _biteHitboxHeight;

    /// <summary>
    /// 撕咬命中造成的伤害。
    /// </summary>
    public float BiteDamage => _biteDamage;

    /// <summary>
    /// 校验鱼骨横扫和撕咬技能参数。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _meleeAttackRange = Mathf.Max(0.1f, _meleeAttackRange);
        _meleeAttackInterval = Mathf.Max(0.05f, _meleeAttackInterval);
        _meleeAttackRadius = Mathf.Max(0.1f, _meleeAttackRadius);
        _meleeDamage = Mathf.Max(0f, _meleeDamage);
        _meleeVisualDuration = Mathf.Max(0.05f, _meleeVisualDuration);
        _minimumRangedDistance = Mathf.Max(_meleeAttackRange + 0.1f, _minimumRangedDistance);
        _rangedAttackRange = Mathf.Max(_minimumRangedDistance, _rangedAttackRange);
        _rangedAttackInterval = Mathf.Max(0.05f, _rangedAttackInterval);
        _biteStrikeDuration = Mathf.Max(0.05f, _biteStrikeDuration);
        _biteHitboxWidth = Mathf.Max(0.1f, _biteHitboxWidth);
        _biteHitboxHeight = Mathf.Max(0.1f, _biteHitboxHeight);
        _biteDamage = Mathf.Max(0f, _biteDamage);
    }
}
