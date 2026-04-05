using UnityEngine;

/// <summary>
/// 追猎者抛出的船锚投射物。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HunterBossAnchorProjectile : MonoBehaviour
{
    public float LifeTime = 4f;
    public float Damage = 16f;
    public float KnockbackStrength = 5.5f;

    private Rigidbody _rigidbody;
    private bool _hasHitTarget;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        EnsureCollider();
        Destroy(gameObject, LifeTime);
    }

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

        if (other.CompareTag("Enemy"))
        {
            return;
        }

        _hasHitTarget = true;

        if (other.CompareTag("Player"))
        {
            PlayerHealthController playerHealth = other.GetComponentInParent<PlayerHealthController>();
            PlayerMovementController playerMovement = other.GetComponentInParent<PlayerMovementController>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(Damage);
            }

            if (playerMovement != null)
            {
                Vector3 pushDirection = other.transform.position - transform.position;
                playerMovement.ApplyExternalImpulse(pushDirection, KnockbackStrength);
            }
        }

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

    public void Configure(float radius, float lifeTime, float immobilizeDuration, bool armedImmediately)
    {
        _radius = Mathf.Max(0.2f, radius);
        _lifeTime = Mathf.Max(0.1f, lifeTime);
        _immobilizeDuration = Mathf.Max(0f, immobilizeDuration);
        _isArmed = armedImmediately;

        EnsureTrigger();
        EnsureVisual();
        Destroy(gameObject, _lifeTime);
    }

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
        if (!_isArmed || !other.CompareTag("Player"))
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
