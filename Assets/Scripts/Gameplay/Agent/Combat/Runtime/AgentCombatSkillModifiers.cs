using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// 单次技能释放使用的天赋修正。
    /// 默认值等价于无修正，便于旧调用路径继续安全工作。
    /// </summary>
    public readonly struct AgentCombatSkillModifiers
    {
        private readonly float _damageMultiplier;

        /// <summary>
        /// 创建技能修正快照。
        /// </summary>
        /// <param name="damageMultiplier">技能伤害倍率。</param>
        /// <param name="durationBonusSeconds">技能实体持续时间加成。</param>
        /// <param name="slowDurationBonusSeconds">命中减速持续时间加成。</param>
        public AgentCombatSkillModifiers(
            float damageMultiplier,
            float durationBonusSeconds,
            float slowDurationBonusSeconds)
        {
            _damageMultiplier = Mathf.Max(0f, damageMultiplier);
            DurationBonusSeconds = Mathf.Max(0f, durationBonusSeconds);
            SlowDurationBonusSeconds = Mathf.Max(0f, slowDurationBonusSeconds);
        }

        /// <summary>
        /// 技能伤害倍率。
        /// </summary>
        public float DamageMultiplier => _damageMultiplier > 0f ? _damageMultiplier : 1f;

        /// <summary>
        /// 技能实体持续时间加成。
        /// </summary>
        public float DurationBonusSeconds { get; }

        /// <summary>
        /// 命中减速持续时间加成。
        /// </summary>
        public float SlowDurationBonusSeconds { get; }
    }
}
