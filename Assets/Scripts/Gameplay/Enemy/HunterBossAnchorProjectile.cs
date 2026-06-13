using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 追猎者抛出的船锚投射物。
/// 命中玩家后造成伤害和击退，并在命中或寿命结束时销毁。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HunterBossAnchorProjectile : MonoBehaviour
{
    /// <summary>
    /// 投射物自动销毁前的存活时间。
    /// </summary>
    public float LifeTime = 4f;

    /// <summary>
    /// 投射物命中伤害。
    /// </summary>
    public float Damage = 16f;

    /// <summary>
    /// 投射物命中时施加给玩家的击退强度。
    /// </summary>
    public float KnockbackStrength = 5.5f;

    /// <summary>
    /// 发射该投射物的 Boss 对象。
    /// </summary>
    public GameObject SourceEnemy;

    /// <summary>
    /// 日志中显示的技能名称。
    /// </summary>
    public string SkillName = "Anchor Throw";

    private Rigidbody _rigidbody;
    private bool _hasHitTarget;

    private void Awake()
    {
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
    }

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        EnsureCollider();
        EnsureTriggerColliders();
        Destroy(gameObject, LifeTime);
    }

    /// <summary>
    /// 以指定速度发射船锚投射物。
    /// </summary>
    /// <param name="velocity">投射物初速度。</param>
    public void Launch(Vector3 velocity)
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        _rigidbody.useGravity = false;
        _rigidbody.velocity = velocity;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (_hasHitTarget || other == null)
        {
            return;
        }

        if (SkillEffectLayerUtility.IsSkillEffectObject(other.gameObject))
        {
            return;
        }

        if (!TryResolvePlayerTarget(
                other,
                out ICombatDamageReceiver damageReceiver,
                out PlayerMovementController playerMovement))
        {
            return;
        }

        _hasHitTarget = true;
        float totalDamage = 0f;

        if (damageReceiver != null)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitDirection = hitPoint - transform.position;
            totalDamage = damageReceiver.TakeCombatDamage(Damage, hitPoint, hitDirection, SourceEnemy);
        }

        if (playerMovement != null)
        {
            Vector3 pushDirection = other.transform.position - transform.position;
            playerMovement.ApplyExternalImpulse(pushDirection, KnockbackStrength);
        }

        EnemySkillDamageLogger.LogSkillDamage(SourceEnemy != null ? SourceEnemy : gameObject, SkillName, totalDamage);
        Destroy(gameObject);
    }

    private void EnsureCollider()
    {
        Collider projectileCollider = GetComponent<Collider>();
        if (projectileCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.35f;
            projectileCollider = sphereCollider;
        }

        projectileCollider.isTrigger = true;
    }

    private void EnsureTriggerColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider projectileCollider = colliders[i];
            if (projectileCollider != null)
                projectileCollider.isTrigger = true;
        }
    }

    private static bool TryResolvePlayerTarget(
        Collider other,
        out ICombatDamageReceiver damageReceiver,
        out PlayerMovementController playerMovement)
    {
        PlayerTargetResolver.TryGetDamageReceiver(other, out damageReceiver);
        playerMovement = other.GetComponentInParent<PlayerMovementController>();
        if (damageReceiver != null || playerMovement != null)
            return true;

        Transform playerRoot = other.transform.root;
        if (playerRoot == null || !PlayerTargetResolver.IsPlayerTarget(playerRoot))
            return false;

        playerMovement = playerRoot.GetComponent<PlayerMovementController>();
        return PlayerTargetResolver.TryGetDamageReceiver(playerRoot, out damageReceiver) ||
               playerMovement != null;
    }
}

/// <summary>
/// 追猎者的漩涡范围。
/// 玩家在范围内会被定身，并在技能触发前有时间离开。
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class HunterBossVortexField : MonoBehaviour
{
    private SphereCollider _triggerCollider;
    private float _lifeTime;
    private float _radius;
    private float _immobilizeDuration;
    private bool _isArmed;

    /// <summary>
    /// 配置漩涡范围、寿命、定身时间和初始武装状态。
    /// </summary>
    /// <param name="radius">漩涡半径。</param>
    /// <param name="lifeTime">漩涡寿命。</param>
    /// <param name="immobilizeDuration">定身持续时间。</param>
    /// <param name="armedImmediately">是否创建后立刻生效。</param>
    public void Configure(float radius, float lifeTime, float immobilizeDuration, bool armedImmediately)
    {
        _radius = Mathf.Max(0.2f, radius);
        _lifeTime = Mathf.Max(0.1f, lifeTime);
        _immobilizeDuration = Mathf.Max(0f, immobilizeDuration);
        _isArmed = armedImmediately;

        EnsureTrigger();
        EnsureVisual();
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
        Destroy(gameObject, _lifeTime);
    }

    /// <summary>
    /// 武装漩涡，使玩家停留在范围内时会被定身。
    /// </summary>
    public void Arm()
    {
        _isArmed = true;
    }

    private void EnsureTrigger()
    {
        _triggerCollider = GetComponent<SphereCollider>();
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = _radius;
        _triggerCollider.center = new Vector3(0f, 0.3f, 0f);
    }

    private void EnsureVisual()
    {
        Transform visual = transform.Find("Visual");
        if (visual == null)
        {
            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualObject.name = "Visual";
            visualObject.transform.SetParent(transform, false);
            Destroy(visualObject.GetComponent<Collider>());
            visual = visualObject.transform;
        }

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(_radius * 2f, 0.03f, _radius * 2f);

        Renderer rendererComponent = visual.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = new Color(0.25f, 0.45f, 1f, 0.55f);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isArmed || !PlayerTargetResolver.IsPlayerTarget(other.transform))
        {
            return;
        }

        PlayerMovementController playerMovement = other.GetComponentInParent<PlayerMovementController>();
        if (playerMovement != null)
        {
            playerMovement.ApplyImmobilize(_immobilizeDuration);
        }
    }
}
