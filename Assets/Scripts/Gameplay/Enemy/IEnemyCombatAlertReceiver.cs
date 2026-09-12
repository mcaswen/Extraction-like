using UnityEngine;

/// <summary>接收同群成员发现的直接攻击者；通知本身不代表本体受伤。</summary>
public interface IEnemyCombatAlertReceiver
{
    /// <summary>空闲时响应攻击者，已有有效战斗则保留当前目标和攻击时序。</summary>
    void NotifyCombatAlert(Transform attacker);
}
