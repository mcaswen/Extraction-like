using UnityEngine;

/// <summary>
/// 远程敌人的投射物和攻击距离配置。
/// </summary>
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

    /// <summary>
    /// 敌人射击时生成的子弹预制体。
    /// </summary>
    public GameObject EnemyBulletPrefab => _enemyBulletPrefab;

    /// <summary>
    /// 子弹飞行速度。
    /// </summary>
    public float BulletMoveSpeed => _bulletMoveSpeed;

    /// <summary>
    /// 子弹命中伤害。
    /// </summary>
    public float BulletDamage => _bulletDamage;

    /// <summary>
    /// 子弹自动销毁前的存活时间。
    /// </summary>
    public float BulletLifeTime => _bulletLifeTime;

    /// <summary>
    /// 远程敌人停止移动并开火的距离。
    /// </summary>
    public float AttackRange => _attackRange;

    /// <summary>
    /// 两次射击之间的最短间隔。
    /// </summary>
    public float AttackInterval => _attackInterval;

    /// <summary>
    /// 校验远程攻击配置的取值范围。
    /// </summary>
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
