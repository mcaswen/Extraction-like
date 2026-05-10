using Gameplay.SkillEffect;
using UnityEngine;
/// <summary>
/// 敌人子弹控制器
/// </summary>
public class EnemyBulletController : MonoBehaviour
{
    public float MoveSpeed = 15f; // 敌人的子弹可以稍微慢一点，给玩家走位躲避的空间
    public float Damage = 15f;//伤害
    public float LifeTime = 3f;//存在时间
    public GameObject SourceEnemy;
    public string SkillName = "Ranged Shot";

    private Rigidbody _rigidbody;//刚体组件

    private void Awake()
    {
        SkillEffectLayerUtility.ApplyToRoot(gameObject);
    }

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();//实例化刚体
        _rigidbody.velocity = transform.forward * MoveSpeed;//经典方向×速度  表示指定某个方向的速度    可以用velocity表示  也可以用Vector3来接受

        Destroy(gameObject, LifeTime);//计时消除子弹实例
    }
    /// <summary>
    /// 触发trigger调用方法
    /// </summary>
    /// <param name="other"></param>other表示的撞击到的物体
    private void OnTriggerEnter(Collider other)
    {
        if (SkillEffectLayerUtility.IsSkillEffectObject(other.gameObject))
        {
            return;
        }

        // 如果碰到了敌人自己（或者其他敌人），直接忽略！防止痛击我的队友
        if (other.CompareTag("Enemy"))
        {
            return;
        }

        // 检查是不是打中了玩家
        PlayerHealthController playerHealthController = other.GetComponentInParent<PlayerHealthController>();
        if (playerHealthController == null && other.CompareTag("Player"))
        {
            Transform playerRoot = other.transform.root;
            if (playerRoot != null && playerRoot.CompareTag("Player"))
            {
                playerHealthController = playerRoot.GetComponent<PlayerHealthController>();
                if (playerHealthController == null)
                {
                    playerHealthController = playerRoot.gameObject.AddComponent<PlayerHealthController>();
                }
            }
        }
        float totalDamage = 0f;
        if (playerHealthController != null)
        {
            // 打中玩家，玩家扣血
            totalDamage = playerHealthController.TakeDamage(Damage);
        }

        EnemySkillDamageLogger.LogSkillDamage(SourceEnemy != null ? SourceEnemy : gameObject, SkillName, totalDamage);

        // 碰到任何东西（除了敌人自己）都销毁子弹
        Destroy(gameObject);
    }
}
