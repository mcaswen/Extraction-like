using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 玩家子弹控制器。
/// </summary>
public class BulletController : MonoBehaviour
{
    public enum AttackElementType
    {
        Physical,
        Fire,
        Ice
    }

    public float MoveSpeed = 20f;
    public float Damage = 25f;
    public float LifeTime = 3f;
    public Color BulletColor = new Color(0.98f, 0.98f, 1f, 1f);
    public AttackElementType AttackElement = AttackElementType.Fire;
    public float SlowMultiplier = 1f;
    public float SlowDurationSeconds = 0f;
    public float FreezeDurationSeconds = 0f;
    public float FrozenFireBonusMultiplier = 2f;
    public Transform SourceTransform;
    public bool ReportImpactStimulus = true;

    private Rigidbody _rigidbody;

    private void Awake()
    {
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
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

        if (other.CompareTag("Player"))
        {
            return;
        }

        if (SkillEffectLayerUtility.IsSkillEffectObject(other.gameObject))
        {
            return;
        }

        AnchorSentinelRuneWeakpoint runeWeakpoint = other.GetComponentInParent<AnchorSentinelRuneWeakpoint>();
        if (runeWeakpoint != null)
        {
            runeWeakpoint.NotifyHit();
            Destroy(gameObject);
            return;
        }

        EnemyHealthController enemyHealthController = other.GetComponentInParent<EnemyHealthController>();
        if (enemyHealthController != null)
        {
            ApplyElementalDamage(enemyHealthController, other);
        }
        else if (ReportImpactStimulus)
        {
            EnemySuspicionStimulusBus.ReportProjectileImpact(transform.position, transform);
        }

        Destroy(gameObject);
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
        EnemyDamageContext damageContext = EnemyDamageContext.FromPlayer(
            ResolvePlayerSource(),
            hitPosition,
            sourcePosition,
            incomingDirection,
            EnemyDamageSourceType.Projectile);

        enemyHealthController.TakeDamage(finalDamage, damageContext);

        if (AttackElement == AttackElementType.Ice)
        {
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

    private Transform ResolvePlayerSource()
    {
        if (TryResolvePlayerRoot(SourceTransform, out Transform playerRoot))
        {
            return playerRoot;
        }

        return null;
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
