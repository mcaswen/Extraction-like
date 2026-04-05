using UnityEngine;

/// <summary>
/// 玩家子弹控制器。
/// </summary>
public class BulletController : MonoBehaviour
{
    public float MoveSpeed = 20f;
    public float Damage = 25f;
    public float LifeTime = 3f;

    private Rigidbody _rigidbody;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            _rigidbody.velocity = transform.forward * MoveSpeed;
        }

        Destroy(gameObject, LifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return;
        }

        AnchorSentinelRuneWeakpoint runeWeakpoint = other.GetComponent<AnchorSentinelRuneWeakpoint>();
        if (runeWeakpoint != null)
        {
            runeWeakpoint.NotifyHit();
            Destroy(gameObject);
            return;
        }

        EnemyHealthController enemyHealthController = other.GetComponent<EnemyHealthController>();
        if (enemyHealthController != null)
        {
            enemyHealthController.TakeDamage(Damage);
        }

        Destroy(gameObject);
    }
}
