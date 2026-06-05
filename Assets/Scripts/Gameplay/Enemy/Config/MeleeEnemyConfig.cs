using UnityEngine;

/// <summary>
/// 基础近战敌人的设计配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_BasicMelee", menuName = "Enemies/Basic Melee Config")]
public sealed class MeleeEnemyConfig : EnemyPatrolConfigBase
{
    [Header("Melee Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the enemy can hit the player.")]
    private float _attackRange = 2.5f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by each melee hit.")]
    private float _attackDamage = 15f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between melee hits.")]
    private float _attackInterval = 1.5f;

    /// <summary>
    /// 近战攻击可命中的距离。
    /// </summary>
    public float AttackRange => _attackRange;

    /// <summary>
    /// 每次近战命中的伤害。
    /// </summary>
    public float AttackDamage => _attackDamage;

    /// <summary>
    /// 两次近战攻击之间的最短间隔。
    /// </summary>
    public float AttackInterval => _attackInterval;

    /// <summary>
    /// 校验近战配置的取值范围。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _attackDamage = Mathf.Max(0f, _attackDamage);
        _attackInterval = Mathf.Max(0.05f, _attackInterval);
    }
}
