using UnityEngine;
/// <summary>
/// 角色血条控制脚本
/// </summary>
public class PlayerHealthController : MonoBehaviour
{
    public float MaxHealth = 100f;//生命值

    // 属性：帕斯卡命名法。外部只能读取，内部才能修改（符合封装原则）
    public float CurrentHealth { get; private set; }
    /// <summary>
    /// 脚本开始
    /// </summary>
    void Start()
    {
        CurrentHealth = MaxHealth;//初始化血条
    }
    /// <summary>
    /// 扣血操作
    /// </summary>
    /// <param name="damage"></param>扣血数量
    public void TakeDamage(float damage)
    {
        CurrentHealth -= damage;//直接扣血
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);//控制血量范围

        Debug.Log("玩家受到攻击！当前血量: " + CurrentHealth);//控制台输出文字

        if (CurrentHealth <= 0)//人物没血
        {
            Die();//死函数
        }
    }
    /// <summary>
    /// 人物死亡
    /// </summary>
    private void Die()
    {
        Debug.Log("玩家死亡！游戏结束！");//输出文字
    }
}