using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗能力控制器
    /// 负责应用战斗风格配置、构建运行时技能并尝试释放就绪技能
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentCombatController : MonoBehaviour
    {
        [SerializeField] private AgentCombatStyleConfig _styleConfig;
        [SerializeField] private LayerMask _enemyLayerMask = ~0;
        [SerializeField, Min(0.05f)] private float _skillActionLockSeconds = 0.45f;

        private readonly List<AgentCombatSkillBase> _runtimeSkills = new List<AgentCombatSkillBase>();
        private AgentCombatShooter _shooter;
        private AgentCombatRuntimeStats _runtimeStats = new AgentCombatRuntimeStats(1, 0f, 0f);

        /// <summary>
        /// 当前使用的战斗风格配置
        /// </summary>
        public AgentCombatStyleConfig StyleConfig => _styleConfig;

        /// <summary>
        /// 当前最大生命值配置
        /// </summary>
        public int MaxHealth => _runtimeStats.MaxHealth;

        /// <summary>
        /// 普通攻击射程
        /// </summary>
        public float AttackRange => _styleConfig != null ? _styleConfig.NormalAttackRange : 0f;

        /// <summary>
        /// 普通攻击伤害
        /// </summary>
        public float AttackDamage => _styleConfig != null
            ? _styleConfig.CalculateNormalAttackDamage(_runtimeStats)
            : _runtimeStats.Attack;

        /// <summary>
        /// 普通攻击间隔
        /// </summary>
        public float AttackInterval => _styleConfig != null ? _styleConfig.NormalAttackInterval : 0f;

        private void Awake()
        {
            CacheComponents();
            RebuildRuntimeSkills();
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            _skillActionLockSeconds = Mathf.Max(0.05f, _skillActionLockSeconds);
            CacheComponents();
        }

        /// <summary>
        /// 应用战斗风格和运行时属性，并在配置变化时重建技能实例
        /// </summary>
        /// <param name="styleConfig"></param>
        /// <param name="runtimeStats"></param>
        public void ApplyConfig(
            AgentCombatStyleConfig styleConfig,
            AgentCombatRuntimeStats runtimeStats)
        {
            if (_styleConfig == styleConfig &&
                _runtimeStats.Equals(runtimeStats) &&
                _runtimeSkills.Count > 0)
            {
                return;
            }

            _styleConfig = styleConfig;
            _runtimeStats = runtimeStats;
            RebuildRuntimeSkills();
        }

        /// <summary>
        /// 尝试对当前敌人释放第一个已就绪技能
        /// </summary>
        /// <param name="enemyTarget"></param>
        /// <param name="timeSeconds"></param>
        /// <param name="actionLockSeconds"></param>
        /// <returns></returns>
        public bool TryCastReadySkill(
            global::EnemyHealthController enemyTarget,
            double timeSeconds,
            out float actionLockSeconds)
        {
            actionLockSeconds = 0f;
            if (_styleConfig == null || enemyTarget == null || _runtimeSkills.Count <= 0)
                return false;

            AgentCombatSkillContext context = new AgentCombatSkillContext(
                transform,
                _styleConfig,
                _runtimeStats,
                _enemyLayerMask,
                _skillActionLockSeconds);
            AgentCombatSkillTarget target = AgentCombatSkillTarget.FromEnemy(enemyTarget);

            // 技能按配置顺序尝试释放，成功一个后本轮攻击节点进入动作锁定
            for (int i = 0; i < _runtimeSkills.Count; i++)
            {
                AgentCombatSkillBase skill = _runtimeSkills[i];
                if (skill != null &&
                    skill.TryCast(context, target, timeSeconds, out actionLockSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildRuntimeSkills()
        {
            CacheComponents();
            _runtimeSkills.Clear();

            if (_styleConfig == null)
                return;

            // 普通攻击发射器跟随风格元素，技能则由各自配置单独创建运行时实例
            ConfigureShooter();
            IReadOnlyList<AgentCombatSkillConfigBase> skills = _styleConfig.Skills;
            if (skills == null)
                return;

            for (int i = 0; i < skills.Count; i++)
            {
                AgentCombatSkillConfigBase skillConfig = skills[i];
                if (skillConfig == null)
                    continue;

                AgentCombatSkillBase runtimeSkill = skillConfig.CreateRuntimeSkill();
                if (runtimeSkill != null)
                    _runtimeSkills.Add(runtimeSkill);
            }
        }

        private void ConfigureShooter()
        {
            if (_shooter == null || _styleConfig == null)
                return;

            _shooter.ConfigureProjectileElement(
                _styleConfig.Element,
                AgentCombatProjectileStatus.None);
        }

        private void CacheComponents()
        {
            if (_shooter == null)
                _shooter = GetComponent<AgentCombatShooter>();
        }
    }
}
