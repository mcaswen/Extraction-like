using UnityEngine;

[CreateAssetMenu(fileName = "SO_Enemy_Ranged", menuName = "Enemies/Ranged Config")]
public sealed class RangedEnemyConfig : EnemyPatrolConfigBase
{
    [Header("Ranged Attack")]
    [SerializeField, Tooltip("Projectile prefab fired by this enemy.")]
    private GameObject _enemyBulletPrefab;

    [SerializeField, Min(0f), Tooltip("Projectile movement speed.")]
    private float _bulletMoveSpeed = 15f;

    [SerializeField, Min(0f), Tooltip("Damage dealt by the projectile.")]
    private float _bulletDamage = 15f;

    [SerializeField, Min(0.05f), Tooltip("Seconds before the projectile is destroyed.")]
    private float _bulletLifeTime = 3f;

    [SerializeField, Min(0.1f), Tooltip("Distance at which the enemy stops and shoots.")]
    private float _attackRange = 10f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between shots.")]
    private float _attackInterval = 2f;

    public GameObject EnemyBulletPrefab => _enemyBulletPrefab;
    public float BulletMoveSpeed => _bulletMoveSpeed;
    public float BulletDamage => _bulletDamage;
    public float BulletLifeTime => _bulletLifeTime;
    public float AttackRange => _attackRange;
    public float AttackInterval => _attackInterval;

    protected override void OnValidate()
    {
        base.OnValidate();
        _bulletMoveSpeed = Mathf.Max(0f, _bulletMoveSpeed);
        _bulletDamage = Mathf.Max(0f, _bulletDamage);
        _bulletLifeTime = Mathf.Max(0.05f, _bulletLifeTime);
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _attackInterval = Mathf.Max(0.05f, _attackInterval);
    }
}
