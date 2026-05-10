using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 现代搁浅者的触手命中盒。
/// 使用动态盒体来近似触手扫过玩家的命中区域。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ModernStranderTentacleHitbox : MonoBehaviour
{
    private ModernStranderBehaviorController _owner;
    private BoxCollider _boxCollider;
    private float _width;
    private float _height;

    public void Initialize(ModernStranderBehaviorController owner, float width, float height)
    {
        _owner = owner;
        _width = Mathf.Max(0.1f, width);
        _height = Mathf.Max(0.1f, height);

        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
    }

    public void UpdateHitboxTransform(Vector3 origin, Vector3 target)
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
        }

        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        transform.position = origin + delta * 0.5f;
        transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        _boxCollider.size = new Vector3(_width, _height, distance);
        _boxCollider.center = Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyOwner(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyOwner(other);
    }

    private void NotifyOwner(Collider other)
    {
        if (_owner == null || !_owner.isActiveAndEnabled || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerHealthController playerHealth = other.GetComponentInParent<PlayerHealthController>();
        PlayerMovementController playerMovement = other.GetComponentInParent<PlayerMovementController>();
        _owner.NotifyTentacleHit(playerHealth, playerMovement);
    }
}

/// <summary>
/// 腐蚀性地面黏液区。
/// 玩家站在上面会持续受到腐蚀效果。
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class CorrosiveSlimePuddle : MonoBehaviour
{
    private SphereCollider _triggerCollider;
    private float _radius;
    private float _lifeTime;
    private float _damagePerSecond;
    private float _corrosionDuration;
    private float _tickInterval;

    public void Configure(float radius, float lifeTime, float damagePerSecond, float corrosionDuration, float tickInterval)
    {
        _radius = Mathf.Max(0.2f, radius);
        _lifeTime = Mathf.Max(0.1f, lifeTime);
        _damagePerSecond = Mathf.Max(0f, damagePerSecond);
        _corrosionDuration = Mathf.Max(0f, corrosionDuration);
        _tickInterval = Mathf.Max(0.05f, tickInterval);

        EnsureVisual();
        EnsureTrigger();
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
        Destroy(gameObject, _lifeTime);
    }

    private void EnsureTrigger()
    {
        _triggerCollider = GetComponent<SphereCollider>();
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = _radius;
        _triggerCollider.center = new Vector3(0f, 0.2f, 0f);
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
            rendererComponent.material.color = new Color(0.16f, 0.68f, 0.2f, 0.75f);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        PlayerHealthController playerHealth = other.GetComponentInParent<PlayerHealthController>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.ApplyCorrosion(_damagePerSecond, _corrosionDuration, _tickInterval);
    }
}
