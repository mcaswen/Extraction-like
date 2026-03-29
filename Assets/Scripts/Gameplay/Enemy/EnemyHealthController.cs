using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 敌人血条控制脚本
/// </summary>
public class EnemyHealthController : MonoBehaviour
{
    public float MaxHealth = 100f;//生命值

    // 内部使用的当前血量
    private float _currentHealth;

    [Header("血条UI引用")]
    public Image HealthFillImage;//血条组件
    /// <summary>
    /// 开始脚本
    /// </summary>
    void Start()
    {
        _currentHealth = MaxHealth;//初始化血条
        UpdateHealthBar();//更新血条UI
    }
    /// <summary>
    /// 扣血
    /// </summary>
    /// <param name="damageAmount"></param>扣出数量
    public void TakeDamage(float damageAmount)
    {
        _currentHealth -= damageAmount;//扣除伤害
        _currentHealth = Mathf.Clamp(_currentHealth, 0, MaxHealth);//控制血量范围

        UpdateHealthBar();//更新血条UI

        if (_currentHealth <= 0)
        {
            Die();//死
        }
    }
    /// <summary>
    /// 更新血条UI
    /// </summary>
    private void UpdateHealthBar()
    {
        if (HealthFillImage != null)//判空血条UI
        {
            HealthFillImage.fillAmount = _currentHealth / MaxHealth;//控制百分比
        }
    }
    /// <summary>
    /// 死亡函数
    /// </summary>
    private void Die()
    {
        Debug.Log("敌人死亡！");//输出文字
        Destroy(gameObject);//消除物体
    }
}