using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 扇形范围伤害技能配置
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_ConeDamageSkill",
        menuName = "SO/Agent/Combat/Skills/Cone Damage")]
    public sealed class AgentConeDamageSkillConfig : AgentCombatSkillConfigBase
    {
        [Header("Cone")]
        [SerializeField] private AgentCombatStatScaling _damage =
            new AgentCombatStatScaling(AgentCombatStatScalingSource.Attack, 2.5f);
        [SerializeField, Min(0.1f)] private float _radius = 8f;
        [SerializeField, Range(1f, 360f)] private float _angleDegrees = 120f;
        [SerializeField] private AgentCombatStatusEffectDefinition _statusEffect;

        [Header("Visual")]
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private Color _indicatorColor = new Color(0.5f, 0.88f, 1f, 0.45f);
        [SerializeField, Min(0.05f)] private float _indicatorDuration = 0.35f;

        /// <summary>
        /// 扇形半径
        /// </summary>
        public float Radius => Mathf.Max(0.1f, _radius);

        /// <summary>
        /// 扇形角度
        /// </summary>
        public float AngleDegrees => Mathf.Clamp(_angleDegrees, 1f, 360f);

        /// <summary>
        /// 命中后附加的状态效果
        /// </summary>
        public AgentCombatStatusEffectDefinition StatusEffect => _statusEffect;

        /// <summary>
        /// 可选命中特效预制体
        /// </summary>
        public GameObject VisualPrefab => _visualPrefab;

        /// <summary>
        /// 默认扇形指示颜色
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
            return new AgentConeDamageSkill(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _radius = Mathf.Max(0.1f, _radius);
            _angleDegrees = Mathf.Clamp(_angleDegrees, 1f, 360f);
            _indicatorDuration = Mathf.Max(0.05f, _indicatorDuration);
        }
    }

    /// <summary>
    /// Agent 扇形范围伤害技能运行时逻辑
    /// </summary>
    public sealed class AgentConeDamageSkill : AgentCombatSkillBase
    {
        private readonly AgentConeDamageSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        /// <summary>
        /// 创建扇形范围伤害技能实例
        /// </summary>
        /// <param name="config"></param>
        public AgentConeDamageSkill(AgentConeDamageSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool CanCast(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            if (target.EnemyTarget == null)
                return false;

            // 目标必须在当前朝向扇形内，避免背身时释放扇形技能
            return AgentCombatSkillUtility.IsInsidePlanarCone(
                context.Position,
                context.Forward,
                target.EnemyTarget.transform.position,
                _config.Radius,
                _config.AngleDegrees);
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            // 先用球形范围做粗筛，再用水平扇形做精筛
            AgentCombatSkillUtility.CollectEnemiesInSphere(
                context.Position,
                _config.Radius,
                context.EnemyLayerMask,
                _targets);

            float damage = _config.CalculateDamage(context.Stats);
            bool hitAnyTarget = false;
            foreach (global::EnemyHealthController enemyHealth in _targets)
            {
                if (enemyHealth == null)
                    continue;

                if (!AgentCombatSkillUtility.IsInsidePlanarCone(
                        context.Position,
                        context.Forward,
                        enemyHealth.transform.position,
                        _config.Radius,
                        _config.AngleDegrees))
                {
                    continue;
                }

                hitAnyTarget = true;
                AgentCombatSkillUtility.ApplyDamageAndStatus(
                    enemyHealth,
                    damage,
                    context,
                    enemyHealth.transform.position,
                    global::EnemyDamageSourceType.Magic,
                    _config.StatusEffect);
            }

            if (hitAnyTarget)
            {
                // 命中后优先播放配置视觉，否则生成默认扇形指示器
                if (_config.VisualPrefab != null)
                {
                    AgentCombatSkillUtility.SpawnVisualPrefab(
                        _config.VisualPrefab,
                        context.Position,
                        Quaternion.LookRotation(context.Forward, Vector3.up),
                        _config.IndicatorDuration);
                }
                else
                {
                    AgentCombatSkillUtility.SpawnConeIndicator(
                        context.Position,
                        context.Forward,
                        _config.Radius,
                        _config.AngleDegrees,
                        _config.IndicatorColor,
                        _config.IndicatorDuration);
                }
            }

            return hitAnyTarget;
        }
    }
}
