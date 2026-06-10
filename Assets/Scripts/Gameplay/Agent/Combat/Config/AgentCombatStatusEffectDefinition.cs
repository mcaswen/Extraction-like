using System;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    [Serializable]
    public struct AgentCombatStatusEffectDefinition
    {
        [SerializeField] private bool _applySlow;
        [SerializeField, Range(0.1f, 1f)] private float _slowMultiplier;
        [SerializeField, Min(0f)] private float _slowDurationSeconds;
        [SerializeField] private bool _applyFreeze;
        [SerializeField, Min(0f)] private float _freezeDurationSeconds;

        public bool ApplySlow => _applySlow && _slowDurationSeconds > 0f;
        public float SlowMultiplier => Mathf.Clamp(_slowMultiplier <= 0f ? 1f : _slowMultiplier, 0.1f, 1f);
        public float SlowDurationSeconds => Mathf.Max(0f, _slowDurationSeconds);
        public bool ApplyFreeze => _applyFreeze && _freezeDurationSeconds > 0f;
        public float FreezeDurationSeconds => Mathf.Max(0f, _freezeDurationSeconds);

        public void ApplyTo(global::EnemyHealthController enemyHealthController)
        {
            if (enemyHealthController == null || (!ApplySlow && !ApplyFreeze))
                return;

            global::EnemyStatusEffectController statusController =
                enemyHealthController.GetComponent<global::EnemyStatusEffectController>();
            if (statusController == null)
                statusController = enemyHealthController.gameObject.AddComponent<global::EnemyStatusEffectController>();

            if (ApplySlow)
                statusController.ApplySlow(SlowMultiplier, SlowDurationSeconds);

            if (ApplyFreeze)
                statusController.ApplyFreeze(FreezeDurationSeconds);
        }

        public AgentCombatProjectileStatus ToProjectileStatus()
        {
            return new AgentCombatProjectileStatus(
                ApplySlow ? SlowMultiplier : 1f,
                ApplySlow ? SlowDurationSeconds : 0f,
                ApplyFreeze ? FreezeDurationSeconds : 0f);
        }
    }
}
