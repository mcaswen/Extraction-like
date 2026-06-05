using UnityEngine;

/// <summary>
/// 敌人技能结算日志工具，用于在技能结束时输出一次总伤害摘要。
/// </summary>
public static class EnemySkillDamageLogger
{
    private const bool EnableSkillDamageLogs = true;

    /// <summary>
    /// 输出一个敌人技能完成后的总伤害。
    /// </summary>
    /// <param name="source">技能来源对象。</param>
    /// <param name="skillName">技能名称。</param>
    /// <param name="totalDamage">本次技能累计造成的伤害。</param>
    public static void LogSkillDamage(Object source, string skillName, float totalDamage)
    {
        if (!EnableSkillDamageLogs)
        {
            return;
        }

        string sourceName = source != null ? source.name : "UnknownEnemy";
        string safeSkillName = string.IsNullOrEmpty(skillName) ? "Unknown Skill" : skillName;
        Debug.Log($"[EnemySkillDamage] {sourceName} finished {safeSkillName}, total damage: {Mathf.Max(0f, totalDamage):0.##}");
    }
}
