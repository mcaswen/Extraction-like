using UnityEngine;
using System.Collections; // 必须引入这个命名空间才能使用协程 (Coroutine)
public class PlayerShootingController : MonoBehaviour
{

    // 新增：枪口的位置（子弹从哪里发射）
    public Transform FirePoint;//射出点
    [Header("子弹设置")]
    public GameObject BulletPrefab;// 拖入你刚才做的子弹预制体

    public float WeaponDamage = 25f;//伤害
    public float WeaponRange = 100f;//射程

    [Header("可视化设置")]
    public LineRenderer BulletTrail; // 弹道线段组件
    public float TrailDuration = 0.05f; // 弹道显示在屏幕上的时间（秒）

    void Update()
    {
        // 沉浸式交互规则：背包打开时禁止开枪
        if (GameUIController.Instance != null && GameUIController.Instance.IsInventoryOpen)
        {
            return; // 直接跳出，不执行射击指令
        }

        if (Input.GetMouseButtonDown(0))//判断鼠标左键按下
        {
            Shoot();
        }
    }

    private void Shoot()
    {//确认设计点  和子弹
        if (FirePoint == null || BulletPrefab == null)
        {
            Debug.LogWarning("请在面板中指定 FirePoint 和 BulletPrefab！");
            return;
        }

        // 核心：在枪口的位置，以枪口当前的面朝方向，生成一颗实体子弹！  实例化生成
        Instantiate(BulletPrefab, FirePoint.position, FirePoint.rotation);
    }
}