using Gameplay.SkillEffect;
using Gameplay.Perception;
using UnityEngine;

/// <summary>
/// 敌人子弹控制器。
/// 负责飞行、命中通用战斗目标并输出技能伤害日志。
/// </summary>
public class EnemyBulletController : MonoBehaviour
{
    /// <summary>
    /// 子弹飞行速度，通常略慢于玩家子弹以留出走位空间。
    /// </summary>
    public float MoveSpeed = 15f;

    /// <summary>
    /// 子弹命中造成的伤害。
    /// </summary>
    public float Damage = 15f;

    /// <summary>
    /// 子弹自动销毁前的存活时间。
    /// </summary>
    public float LifeTime = 3f;

    /// <summary>
    /// 发射该子弹的敌人对象。
    /// </summary>
    public GameObject SourceEnemy;

    /// <summary>
    /// 日志中显示的技能名称。
    /// </summary>
    public string SkillName = "Ranged Shot";

    private Rigidbody _rigidbody;
    private bool _hasHitTarget;

    private void FixedUpdate()
    {
        if (_hasHitTarget || _rigidbody == null) return;
        Vector3 from=_rigidbody.position;
        if (ProjectileSweepQuery.TryFirstHit(transform,SourceEnemy != null ? SourceEnemy.transform : null,from,from+_rigidbody.velocity*Time.fixedDeltaTime,0.06f,out Collider hit))
            OnTriggerEnter(hit);
    }

    private void Awake()
    {
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
        EnsureTriggerColliders();
    }

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.velocity = transform.forward * MoveSpeed;

        Destroy(gameObject, LifeTime);
    }

    /// <summary>
    /// 子弹触发碰撞时结算伤害并销毁。
    /// </summary>
    /// <param name="other">命中的碰撞体。</param>
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHitTarget || !ProjectileSweepQuery.IsBlocking(other,transform,SourceEnemy != null ? SourceEnemy.transform : null))
        {
            return;
        }

        _hasHitTarget=true;

        if (!TryGetPlayerDamageReceiver(other, out ICombatDamageReceiver damageReceiver))
        {
            Destroy(gameObject);
            return;
        }

        float totalDamage = 0f;
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitDirection = hitPoint - transform.position;
        totalDamage = damageReceiver.TakeCombatDamage(
            Damage,
            hitPoint,
            hitDirection,
            SourceEnemy);

        EnemySkillDamageLogger.LogSkillDamage(SourceEnemy != null ? SourceEnemy : gameObject, SkillName, totalDamage);

        Destroy(gameObject);
    }

    private void EnsureTriggerColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider bulletCollider = colliders[i];
            if (bulletCollider != null)
                bulletCollider.isTrigger = true;
        }
    }

    private static bool TryGetPlayerDamageReceiver(
        Collider other,
        out ICombatDamageReceiver damageReceiver)
    {
        damageReceiver = null;
        return PlayerTargetResolver.TryGetDamageReceiver(other, out damageReceiver);
    }
}
