using UnityEngine;

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

    public float AttackRange => _attackRange;
    public float AttackDamage => _attackDamage;
    public float AttackInterval => _attackInterval;

    protected override void OnValidate()
    {
        base.OnValidate();
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _attackDamage = Mathf.Max(0f, _attackDamage);
        _attackInterval = Mathf.Max(0.05f, _attackInterval);
    }
}
