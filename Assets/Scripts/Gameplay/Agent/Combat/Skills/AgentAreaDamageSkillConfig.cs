using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
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

        public float Radius => Mathf.Max(0.1f, _radius);
        public AgentCombatStatusEffectDefinition StatusEffect => _statusEffect;
        public GameObject VisualPrefab => _visualPrefab;
        public Color IndicatorColor => _indicatorColor;
        public float IndicatorDuration => Mathf.Max(0.05f, _indicatorDuration);

        public float CalculateDamage(AgentCombatRuntimeStats stats)
        {
            return _damage.Evaluate(stats);
        }

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

    public sealed class AgentAreaDamageSkill : AgentCombatSkillBase
    {
        private readonly AgentAreaDamageSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        public AgentAreaDamageSkill(AgentAreaDamageSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            Vector3 center = AgentCombatSkillUtility.ResolveTargetPosition(context, target);
            AgentCombatSkillUtility.CollectEnemiesInSphere(
                center,
                _config.Radius,
                context.EnemyLayerMask,
                _targets);

            float damage = _config.CalculateDamage(context.Stats);
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

            if (hitAnyTarget && _config.VisualPrefab != null)
            {
                AgentCombatSkillUtility.SpawnVisualPrefab(
                    _config.VisualPrefab,
                    center,
                    Quaternion.identity,
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
