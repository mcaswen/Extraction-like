using System;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗状态效果定义
    /// 描述技能或子弹命中后附加的减速、冰冻效果
    /// </summary>
    [Serializable]
    public struct AgentCombatStatusEffectDefinition
    {
        [SerializeField] private bool _applySlow;
        [SerializeField, Range(0.1f, 1f)] private float _slowMultiplier;
        [SerializeField, Min(0f)] private float _slowDurationSeconds;
        [SerializeField] private bool _applyFreeze;
        [SerializeField, Min(0f)] private float _freezeDurationSeconds;

        /// <summary>
        /// 是否应用减速效果
        /// </summary>
        public bool ApplySlow => _applySlow && _slowDurationSeconds > 0f;

        /// <summary>
        /// 减速倍率
        /// </summary>
        public float SlowMultiplier => Mathf.Clamp(_slowMultiplier <= 0f ? 1f : _slowMultiplier, 0.1f, 1f);

        /// <summary>
        /// 减速持续时间
        /// </summary>
        public float SlowDurationSeconds => Mathf.Max(0f, _slowDurationSeconds);

        /// <summary>
        /// 是否应用冰冻效果
        /// </summary>
        public bool ApplyFreeze => _applyFreeze && _freezeDurationSeconds > 0f;

        /// <summary>
        /// 冰冻持续时间
        /// </summary>
        public float FreezeDurationSeconds => Mathf.Max(0f, _freezeDurationSeconds);

        /// <summary>
        /// 将状态效果应用到敌人身上
        /// </summary>
        /// <param name="enemyHealthController"></param>
        /// <param name="slowDurationBonusSeconds"></param>
        public void ApplyTo(
            global::EnemyHealthController enemyHealthController,
            float slowDurationBonusSeconds = 0f)
        {
            if (enemyHealthController == null || (!ApplySlow && !ApplyFreeze))
                return;

            // 状态控制器按需挂载，避免所有敌人预制体都必须提前配置
            global::EnemyStatusEffectController statusController =
                enemyHealthController.GetComponent<global::EnemyStatusEffectController>();
            if (statusController == null)
                statusController = enemyHealthController.gameObject.AddComponent<global::EnemyStatusEffectController>();

            if (ApplySlow)
            {
                float slowDuration = SlowDurationSeconds + Mathf.Max(0f, slowDurationBonusSeconds);
                statusController.ApplySlow(SlowMultiplier, slowDuration);
            }

            if (ApplyFreeze)
                statusController.ApplyFreeze(FreezeDurationSeconds);
        }

        /// <summary>
        /// 转换为普通攻击子弹可使用的附加状态配置
        /// </summary>
        /// <returns></returns>
        public AgentCombatProjectileStatus ToProjectileStatus()
        {
            return new AgentCombatProjectileStatus(
                ApplySlow ? SlowMultiplier : 1f,
                ApplySlow ? SlowDurationSeconds : 0f,
                ApplyFreeze ? FreezeDurationSeconds : 0f);
        }
    }
}
