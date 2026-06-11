using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// 技能目标描述
    /// 可以表示具体敌人，也可以表示一个固定世界坐标
    /// </summary>
    public readonly struct AgentCombatSkillTarget
    {
        /// <summary>
        /// 创建技能目标
        /// </summary>
        /// <param name="enemyTarget"></param>
        /// <param name="position"></param>
        /// <param name="hasPosition"></param>
        public AgentCombatSkillTarget(global::EnemyHealthController enemyTarget, Vector3 position, bool hasPosition)
        {
            EnemyTarget = enemyTarget;
            Position = position;
            HasPosition = hasPosition;
        }

        /// <summary>
        /// 目标敌人实例
        /// </summary>
        public global::EnemyHealthController EnemyTarget { get; }

        /// <summary>
        /// 技能目标位置
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 当前目标是否包含有效位置
        /// </summary>
        public bool HasPosition { get; }

        /// <summary>
        /// 从敌人实例构造技能目标
        /// </summary>
        /// <param name="enemyTarget"></param>
        /// <returns></returns>
        public static AgentCombatSkillTarget FromEnemy(global::EnemyHealthController enemyTarget)
        {
            return new AgentCombatSkillTarget(
                enemyTarget,
                enemyTarget != null ? enemyTarget.transform.position : Vector3.zero,
                enemyTarget != null);
        }
    }
}
