using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 玩家子弹控制器。
/// 负责飞行、命中判定、元素效果和直接伤害上下文传递。
/// </summary>
public class BulletController : MonoBehaviour
{
    /// <summary>
    /// 玩家子弹携带的元素类型。
    /// </summary>
    public enum AttackElementType
    {
        Physical,
        Fire,
        Ice
    }

    /// <summary>
    /// 子弹飞行速度。
    /// </summary>
    public float MoveSpeed = 20f;

    /// <summary>
    /// 子弹基础伤害。
    /// </summary>
    public float Damage = 25f;

    /// <summary>
    /// 子弹自动销毁前的存活时间。
    /// </summary>
    public float LifeTime = 3f;

    /// <summary>
    /// 子弹运行时材质颜色。
    /// </summary>
    public Color BulletColor = new Color(0.98f, 0.98f, 1f, 1f);

    /// <summary>
    /// 子弹当前携带的元素类型。
    /// </summary>
    public AttackElementType AttackElement = AttackElementType.Fire;

    /// <summary>
    /// 冰元素减速倍率。
    /// </summary>
    public float SlowMultiplier = 1f;

    /// <summary>
    /// 冰元素减速持续时间。
    /// </summary>
    public float SlowDurationSeconds = 0f;

    /// <summary>
    /// 冰元素冰冻持续时间。
    /// </summary>
    public float FreezeDurationSeconds = 0f;

    /// <summary>
    /// 火元素命中冰冻目标时的伤害倍率。
    /// </summary>
    public float FrozenFireBonusMultiplier = 2f;

    /// <summary>
    /// 子弹来源，用于让被命中的敌人追击正确攻击者。
    /// </summary>
    public Transform SourceTransform;

    /// <summary>
    /// 子弹未命中敌人时是否报告投射物命中刺激。
    /// </summary>
    public bool ReportImpactStimulus = true;

    private Rigidbody _rigidbody;

    private void Awake()
    {
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
        EnsureTriggerColliders();
    }

    private void Start()
    {
        ApplyBulletColor();

        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            _rigidbody.velocity = transform.forward * MoveSpeed;
        }

        Destroy(gameObject, LifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (SkillEffectLayerUtility.IsSkillEffectObject(other.gameObject))
        {
            return;
        }

        // 玩家子弹先处理锚点守卫符文，避免符文命中被普通敌人血量逻辑吞掉。
        AnchorSentinelRuneWeakpoint runeWeakpoint = other.GetComponentInParent<AnchorSentinelRuneWeakpoint>();
        EnemyHealthController enemyHealthController = other.GetComponentInParent<EnemyHealthController>();
        bool hitEnemyTarget = runeWeakpoint != null || enemyHealthController != null || IsEnemyCollider(other);
        if (!hitEnemyTarget)
        {
            return;
        }

        if (runeWeakpoint != null)
        {
            runeWeakpoint.NotifyHit();
            Destroy(gameObject);
            return;
        }

        if (enemyHealthController != null)
        {
            ApplyElementalDamage(enemyHealthController, other);
        }

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

    private static bool IsEnemyCollider(Collider other)
    {
        return other.CompareTag("Enemy") ||
               other.GetComponentInParent<EnemyHealthController>() != null;
    }

    private void ApplyBulletColor()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];
            if (rendererComponent == null)
            {
                continue;
            }

            Material sourceMaterial = rendererComponent.sharedMaterial;
            Shader shader = sourceMaterial != null && sourceMaterial.shader != null
                ? sourceMaterial.shader
                : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material runtimeMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : new Material(shader);

            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor("_BaseColor", BulletColor);
            }

            if (runtimeMaterial.HasProperty("_Color"))
            {
                runtimeMaterial.color = BulletColor;
            }

            rendererComponent.material = runtimeMaterial;
        }
    }

    private void ApplyElementalDamage(EnemyHealthController enemyHealthController, Collider hitCollider)
    {
        if (enemyHealthController == null)
        {
            return;
        }

        EnemyStatusEffectController statusEffectController = enemyHealthController.GetComponent<EnemyStatusEffectController>();
        if (statusEffectController == null)
        {
            statusEffectController = enemyHealthController.gameObject.AddComponent<EnemyStatusEffectController>();
        }

        float finalDamage = Damage;
        HunterBossBehaviorController hunterBoss = enemyHealthController.GetComponent<HunterBossBehaviorController>();
        bool canBreakFrozenForceField = hunterBoss != null && hunterBoss.IsForceFieldFrozen;
        if (AttackElement == AttackElementType.Fire && (statusEffectController.IsFrozen || canBreakFrozenForceField))
        {
            // 火元素命中冰冻目标或冰冻力场时会增伤，并解除对应冻结状态。
            float fireBonusMultiplier = hunterBoss != null
                ? Mathf.Max(FrozenFireBonusMultiplier, hunterBoss.ForceFieldFireDamageMultiplier)
                : FrozenFireBonusMultiplier;
            finalDamage *= Mathf.Max(1f, fireBonusMultiplier);
            statusEffectController.BreakFreeze();
            statusEffectController.BreakSlow();
            hunterBoss?.NotifyFrozenForceFieldBrokenByFire();
        }

        Vector3 hitPosition = hitCollider != null
            ? hitCollider.ClosestPoint(transform.position)
            : enemyHealthController.transform.position;
        Vector3 sourcePosition = SourceTransform != null ? SourceTransform.position : transform.position;
        Vector3 incomingDirection = hitPosition - sourcePosition;
        // 这里携带攻击者上下文，保证被玩家或 Agent 命中的巡逻敌人能直接反击来源。
        EnemyDamageContext damageContext = EnemyDamageContext.FromAttacker(
            ResolveDamageSource(),
            hitPosition,
            sourcePosition,
            incomingDirection,
            EnemyDamageSourceType.Projectile);

        enemyHealthController.TakeDamage(finalDamage, damageContext);

        if (AttackElement == AttackElementType.Ice)
        {
            // 冰元素伤害完成后再施加状态，避免状态禁用脚本影响本次受击结算。
            if (SlowDurationSeconds > 0f)
            {
                statusEffectController.ApplySlow(SlowMultiplier, SlowDurationSeconds);
            }

            if (FreezeDurationSeconds > 0f)
            {
                statusEffectController.ApplyFreeze(FreezeDurationSeconds);
            }
        }
    }

    private Transform ResolveDamageSource()
    {
        if (TryResolvePlayerRoot(SourceTransform, out Transform playerRoot))
        {
            return playerRoot;
        }

        return SourceTransform;
    }

    private static bool TryResolvePlayerRoot(Transform source, out Transform playerRoot)
    {
        playerRoot = null;
        if (source == null)
        {
            return false;
        }

        PlayerHealthController playerHealth = source.GetComponentInParent<PlayerHealthController>();
        if (playerHealth != null)
        {
            playerRoot = playerHealth.transform;
            return true;
        }

        if (source.CompareTag("Player"))
        {
            playerRoot = source;
            return true;
        }

        return false;
    }
}
