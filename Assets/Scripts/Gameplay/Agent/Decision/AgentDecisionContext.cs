using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 决策上下文
    /// 保存本次评分需要的 Agent 属性、位置和周围敌人快照
    /// </summary>
    public readonly struct AgentDecisionContext
    {
        public AgentDecisionContext(
            Vector3 agentPosition,
            float attack,
            float currentHealth,
            float maxHealth,
            float defense,
            string currentTargetId,
            IReadOnlyList<global::EnemyHealthController> riskEnemies)
        {
            AgentPosition = agentPosition;
            Attack = Mathf.Max(1f, attack);
            CurrentHealth = Mathf.Max(0f, currentHealth);
            MaxHealth = Mathf.Max(1f, maxHealth);
            Defense = Mathf.Max(0f, defense);
            CurrentTargetId = currentTargetId ?? string.Empty;
            RiskEnemies = riskEnemies;
        }

        public Vector3 AgentPosition { get; }
        public float Attack { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public float Defense { get; }
        public string CurrentTargetId { get; }
        public IReadOnlyList<global::EnemyHealthController> RiskEnemies { get; }
        public float HealthRatio => MaxHealth <= 0f ? 0f : CurrentHealth / MaxHealth;
    }
}
