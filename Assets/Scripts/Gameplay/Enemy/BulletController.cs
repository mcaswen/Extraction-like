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

    private Rigidbody _rigidbody;

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
            ApplyElementalDamage(enemyHealthController);
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

    private void ApplyElementalDamage(EnemyHealthController enemyHealthController)
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
        if (AttackElement == AttackElementType.Fire && statusEffectController.IsFrozen)
        {
            finalDamage *= Mathf.Max(1f, FrozenFireBonusMultiplier);
            statusEffectController.BreakFreeze();
        }

        enemyHealthController.TakeDamage(finalDamage);

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
}
