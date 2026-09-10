using System.Collections.Generic;
using Gameplay.Agent.Talent;
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
        [Header("Debug Visuals")]
        [SerializeField] private bool _playPrototypeSkillVfx = true;
        [SerializeField] private bool _suppressConfiguredSkillVfx = true;
        [SerializeField, Min(0.01f)] private float _prototypeSkillVfxRangeScale = 1f;

        private readonly List<AgentCombatSkillBase> _runtimeSkills = new List<AgentCombatSkillBase>();
        private AgentCombatShooter _shooter;
        private AgentTalentRuntimeController _talentController;
        private AgentCombatRuntimeStats _runtimeStats = new AgentCombatRuntimeStats(1, 0f, 0f);
        private global::TotemModifierSet _totemModifiers;
        private double _nextAttackTime;
        public bool IsAttackReady(double timeSeconds) => timeSeconds >= _nextAttackTime;
        public void LockAttack(double timeSeconds, float duration) => _nextAttackTime = System.Math.Max(_nextAttackTime, timeSeconds + Mathf.Max(0.05f, duration));

        /// <summary>
        /// 当前使用的战斗风格配置
        /// </summary>
        public AgentCombatStyleConfig StyleConfig => _styleConfig;

        /// <summary>
        /// 当前最大生命值配置
        /// </summary>
        public int MaxHealth => EffectiveRuntimeStats.MaxHealth;

        /// <summary>
        /// 当前有效防御属性
        /// </summary>
        public float Defense => EffectiveRuntimeStats.Defense;

        /// <summary>
        /// 普通攻击射程
        /// </summary>
        public float AttackRange => _styleConfig != null
            ? _totemModifiers.ApplyAttackRange(_styleConfig.NormalAttackRange)
            : 0f;

        /// <summary>
        /// 普通攻击伤害
        /// </summary>
        public float AttackDamage => _styleConfig != null
            ? _totemModifiers.ApplyNormalAttackDamage(_styleConfig.CalculateNormalAttackDamage(EffectiveRuntimeStats))
            : EffectiveRuntimeStats.Attack;

        /// <summary>
        /// 普通攻击间隔
        /// </summary>
        public float AttackInterval => _styleConfig != null ? _styleConfig.NormalAttackInterval : 0f;

        private void Awake()
        {
            CacheComponents();
            ReconcileRuntimeSkills();
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            _skillActionLockSeconds = Mathf.Max(0.05f, _skillActionLockSeconds);
            _prototypeSkillVfxRangeScale = Mathf.Max(0.01f, _prototypeSkillVfxRangeScale);
            CacheComponents();
        }

        /// <summary>
        /// 应用战斗风格和运行时属性，并在配置变化时重建技能实例
        /// </summary>
        /// <param name="styleConfig"></param>
        /// <param name="runtimeStats"></param>
        public void ApplyConfig(
            AgentCombatStyleConfig styleConfig,
            AgentCombatRuntimeStats runtimeStats,
            global::TotemModifierSet totemModifiers = default)
        {
            _styleConfig = styleConfig;
            _runtimeStats = runtimeStats;
            _totemModifiers = totemModifiers;
            ReconcileRuntimeSkills();
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

            AgentCombatSkillTarget target = AgentCombatSkillTarget.FromEnemy(enemyTarget);
            AgentCombatRuntimeStats effectiveStats = EffectiveRuntimeStats;

            // 技能按配置顺序尝试释放，成功一个后本轮攻击节点进入动作锁定
            for (int i = 0; i < _runtimeSkills.Count; i++)
            {
                AgentCombatSkillBase skill = _runtimeSkills[i];
                AgentCombatSkillContext context = CreateSkillContext(skill, effectiveStats);
                if (skill != null &&
                    skill.TryCast(context, target, timeSeconds, out actionLockSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        private AgentCombatRuntimeStats EffectiveRuntimeStats
        {
            get
            {
                AgentCombatRuntimeStats stats =
                    _talentController != null ? _talentController.ApplyStatModifiers(_runtimeStats) : _runtimeStats;
                return new AgentCombatRuntimeStats(
                    _totemModifiers.ApplyMaxHealth(stats.MaxHealth),
                    stats.Attack,
                    stats.Defense);
            }
        }

        private AgentCombatSkillContext CreateSkillContext(
            AgentCombatSkillBase skill,
            AgentCombatRuntimeStats effectiveStats)
        {
            AgentCombatSkillModifiers skillModifiers =
                _talentController != null && skill != null && skill.Config != null
                    ? _talentController.CreateSkillModifiers(skill.Config.SkillId)
                    : default;

            if (skill != null && skill.Config != null)
            {
                skillModifiers = new AgentCombatSkillModifiers(
                    skillModifiers.DamageMultiplier * _totemModifiers.GetSkillDamageMultiplier(skill.Config.Element),
                    skillModifiers.DurationBonusSeconds,
                    skillModifiers.SlowDurationBonusSeconds);
            }

            return new AgentCombatSkillContext(
                transform,
                _styleConfig,
                effectiveStats,
                _enemyLayerMask,
                _skillActionLockSeconds,
                skillModifiers,
                _playPrototypeSkillVfx,
                _suppressConfiguredSkillVfx,
                _prototypeSkillVfxRangeScale,
                AttackRange);
        }

        private void ReconcileRuntimeSkills()
        {
            CacheComponents();
            ConfigureShooter();
            IReadOnlyList<AgentCombatSkillConfigBase> skills=_styleConfig != null ? _styleConfig.Skills : null;
            if (skills == null) { _runtimeSkills.Clear(); return; }
            int index=0;
            bool unchanged=true;
            foreach (var config in skills)
            {
                if (config == null) continue;
                if (index>=_runtimeSkills.Count || _runtimeSkills[index].Config!=config) unchanged=false;
                index++;
            }
            if (unchanged && index==_runtimeSkills.Count) return;

            var previous=new List<AgentCombatSkillBase>(_runtimeSkills);
            _runtimeSkills.Clear();
            foreach (var config in skills)
            {
                if (config == null) continue;
                var retained=previous.Find(skill=>skill.Config==config);
                if (retained != null)
                {
                    previous.Remove(retained);
                    _runtimeSkills.Add(retained);
                    continue;
                }
                var created=config.CreateRuntimeSkill();
                if (created == null) continue;
                var equivalent=previous.Find(skill=>skill.GetType()==created.GetType() &&
                    string.Equals(skill.Config.SkillId,config.SkillId,System.StringComparison.Ordinal));
                if (equivalent != null)
                {
                    created.PreserveCooldownFrom(equivalent);
                    previous.Remove(equivalent);
                }
                _runtimeSkills.Add(created);
            }
        }

        private void ConfigureShooter()
        {
            if (_shooter == null || _styleConfig == null)
                return;

            _shooter.ConfigureProjectileElement(
                _styleConfig.Element,
                AgentCombatProjectileStatus.None,
                AttackRange);
        }

        private void CacheComponents()
        {
            if (_shooter == null)
                _shooter = GetComponent<AgentCombatShooter>();

            if (_talentController == null)
                _talentController = GetComponent<AgentTalentRuntimeController>();

            if (_talentController != null)
                _talentController.EnsureInitialUnlocksApplied();
        }
    }
}
