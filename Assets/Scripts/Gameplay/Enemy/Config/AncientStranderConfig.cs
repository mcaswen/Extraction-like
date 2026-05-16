using UnityEngine;

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

    public float MeleeAttackRange => _meleeAttackRange;
    public float MeleeAttackInterval => _meleeAttackInterval;
    public float MeleeAttackRadius => _meleeAttackRadius;
    public float MeleeDamage => _meleeDamage;
    public float MeleeVisualDuration => _meleeVisualDuration;
    public float MinimumRangedDistance => _minimumRangedDistance;
    public float RangedAttackRange => _rangedAttackRange;
    public float RangedAttackInterval => _rangedAttackInterval;
    public float BiteStrikeDuration => _biteStrikeDuration;
    public float BiteHitboxWidth => _biteHitboxWidth;
    public float BiteHitboxHeight => _biteHitboxHeight;
    public float BiteDamage => _biteDamage;

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
