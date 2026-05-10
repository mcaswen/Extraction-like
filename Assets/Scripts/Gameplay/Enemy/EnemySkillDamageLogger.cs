using UnityEngine;

/// <summary>
/// Prints one damage summary when an enemy skill finishes
/// </summary>
public static class EnemySkillDamageLogger
{
    private const bool EnableSkillDamageLogs = true;

    /// <summary>
    /// Prints the total damage caused by a completed enemy skill
    /// </summary>
    /// <param name="source"></param>
    /// <param name="skillName"></param>
    /// <param name="totalDamage"></param>
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
