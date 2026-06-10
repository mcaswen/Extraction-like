using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public readonly struct AgentCombatSkillTarget
    {
        public AgentCombatSkillTarget(global::EnemyHealthController enemyTarget, Vector3 position, bool hasPosition)
        {
            EnemyTarget = enemyTarget;
            Position = position;
            HasPosition = hasPosition;
        }

        public global::EnemyHealthController EnemyTarget { get; }
        public Vector3 Position { get; }
        public bool HasPosition { get; }

        public static AgentCombatSkillTarget FromEnemy(global::EnemyHealthController enemyTarget)
        {
            return new AgentCombatSkillTarget(
                enemyTarget,
                enemyTarget != null ? enemyTarget.transform.position : Vector3.zero,
                enemyTarget != null);
        }
    }
}
