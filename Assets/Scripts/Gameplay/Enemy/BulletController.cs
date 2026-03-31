using UnityEngine;

public class BulletController : MonoBehaviour
{
    public float MoveSpeed = 20f; // 子弹飞行速度
    public float Damage = 25f;    // 子弹伤害
    public float LifeTime = 3f;   // 存活时间（防止子弹飞出地图无限消耗内存）

    private Rigidbody _rigidbody;//获取子弹刚体
    /// <summary>
    /// 脚本开始运行
    /// </summary>
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();//获得刚体组件

        // 1. 让子弹一出生，就朝着自己的正前方以恒定速度飞行
        _rigidbody.velocity = transform.forward * MoveSpeed;

        // 2. 设定定时销毁（3秒后如果啥都没打中，自动消失）
        Destroy(gameObject, LifeTime);
    }

    // 物理引擎的碰撞检测函数：当有物体进入子弹的 Trigger 时自动触发
    private void OnTriggerEnter(Collider other)
    {
        // 如果子弹碰到了玩家自己，直接忽略，不销毁子弹也不扣血
        if (other.CompareTag("Player"))
        {
            return;
        }

        // 检查碰到的物体是不是敌人
        EnemyHealthController enemyHealthController = other.GetComponent<EnemyHealthController>();
        if (enemyHealthController != null)
        {
            // 是敌人，造成伤害
            enemyHealthController.TakeDamage(Damage);
        }

        // 只要碰到了任何东西（墙壁、地面、敌人），子弹就立刻销毁自己
        Destroy(gameObject);
    }
}