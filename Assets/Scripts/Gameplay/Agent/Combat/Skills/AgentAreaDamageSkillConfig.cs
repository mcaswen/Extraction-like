using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 圆形范围伤害技能配置
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_AreaDamageSkill",
        menuName = "SO/Agent/Combat/Skills/Area Damage")]
    public sealed class AgentAreaDamageSkillConfig : AgentCombatSkillConfigBase
    {
        [Header("Area")]
        [SerializeField] private AgentCombatStatScaling _damage =
            new AgentCombatStatScaling(AgentCombatStatScalingSource.Attack, 3f);
        [SerializeField, Min(0.1f)] private float _radius = 4f;
        [SerializeField] private AgentCombatStatusEffectDefinition _statusEffect;

        [Header("Visual")]
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private Color _indicatorColor = new Color(0.72f, 0.95f, 1f, 0.5f);
        [SerializeField, Min(0.05f)] private float _indicatorDuration = 0.45f;

        /// <summary>
        /// 伤害半径
        /// </summary>
        public float Radius => Mathf.Max(0.1f, _radius);

        /// <summary>
        /// 命中后附加的状态效果
        /// </summary>
        public AgentCombatStatusEffectDefinition StatusEffect => _statusEffect;

        /// <summary>
        /// 可选命中特效预制体
        /// </summary>
        public GameObject VisualPrefab => _visualPrefab;

        /// <summary>
        /// 默认范围指示颜色
        /// </summary>
        public Color IndicatorColor => _indicatorColor;

        /// <summary>
        /// 指示器或视觉自动销毁时间
        /// </summary>
        public float IndicatorDuration => Mathf.Max(0.05f, _indicatorDuration);

        /// <summary>
        /// 根据运行时属性计算技能伤害
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public float CalculateDamage(AgentCombatRuntimeStats stats)
        {
            return _damage.Evaluate(stats);
        }

        /// <summary>
        /// 创建该配置对应的运行时技能实例
        /// </summary>
        /// <returns></returns>
        public override AgentCombatSkillBase CreateRuntimeSkill()
        {
            return new AgentAreaDamageSkill(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _radius = Mathf.Max(0.1f, _radius);
            _indicatorDuration = Mathf.Max(0.05f, _indicatorDuration);
        }
    }

    /// <summary>
    /// Agent 圆形范围伤害技能运行时逻辑
    /// </summary>
    public sealed class AgentAreaDamageSkill : AgentCombatSkillBase
    {
        private readonly AgentAreaDamageSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        /// <summary>
        /// 创建圆形范围伤害技能实例
        /// </summary>
        /// <param name="config"></param>
        public AgentAreaDamageSkill(AgentAreaDamageSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            Vector3 center = AgentCombatSkillUtility.ResolveTargetPosition(context, target);
            // 先收集范围内目标，再统一按当前属性结算本次伤害
            AgentCombatSkillUtility.CollectEnemiesInSphere(
                center,
                _config.Radius,
                context.EnemyLayerMask,
                _targets);

            float damage = _config.CalculateDamage(context.Stats) * context.SkillModifiers.DamageMultiplier;
            bool hitAnyTarget = false;
            foreach (global::EnemyHealthController enemyHealth in _targets)
            {
                if (enemyHealth == null)
                    continue;

                hitAnyTarget = true;
                AgentCombatSkillUtility.ApplyDamageAndStatus(
                    enemyHealth,
                    damage,
                    context,
                    enemyHealth.transform.position,
                    global::EnemyDamageSourceType.Magic,
                    _config.StatusEffect);
            }

            // 只有命中目标时播放视觉反馈，避免空放时制造误导性范围特效
            if (hitAnyTarget)
            {
                bool playedPrototype = AgentPrototypeSkillVfxBridge.TryPlayWinterfall(
                    context,
                    _config,
                    center,
                    _config.Radius);
                if (playedPrototype && context.SuppressConfiguredSkillVfx)
                    return true;
            }

            if (hitAnyTarget && _config.VisualPrefab != null)
            {
                AgentCombatSkillUtility.SpawnVisualPrefab(
                    _config.VisualPrefab,
                    center,
                    Quaternion.identity,
                    AgentCombatSkillUtility.CreatePlanarRangeVisualScale(
                        _config.Radius,
                        AgentCombatSkillUtility.AuthoredCircleVisualRadius),
                    _config.IndicatorDuration);
            }
            else if (hitAnyTarget)
            {
                AgentCombatSkillUtility.SpawnCircleIndicator(center, _config.Radius, _config.IndicatorColor, _config.IndicatorDuration);
            }

            return hitAnyTarget;
        }
    }
}
