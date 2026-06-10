using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    [DisallowMultipleComponent]
    public sealed class AgentCombatController : MonoBehaviour
    {
        [SerializeField] private AgentCombatStyleConfig _styleConfig;
        [SerializeField] private LayerMask _enemyLayerMask = ~0;
        [SerializeField, Min(0.05f)] private float _skillActionLockSeconds = 0.45f;

        private readonly List<AgentCombatSkillBase> _runtimeSkills = new List<AgentCombatSkillBase>();
        private AgentCombatShooter _shooter;
        private AgentCombatRuntimeStats _runtimeStats = new AgentCombatRuntimeStats(1, 0f, 0f);

        public AgentCombatStyleConfig StyleConfig => _styleConfig;
        public int MaxHealth => _runtimeStats.MaxHealth;
        public float AttackRange => _styleConfig != null ? _styleConfig.NormalAttackRange : 0f;
        public float AttackDamage => _styleConfig != null
            ? _styleConfig.CalculateNormalAttackDamage(_runtimeStats)
            : _runtimeStats.Attack;
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
