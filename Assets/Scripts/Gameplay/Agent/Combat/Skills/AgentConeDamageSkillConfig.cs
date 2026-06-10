using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
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

        public float Radius => Mathf.Max(0.1f, _radius);
        public float AngleDegrees => Mathf.Clamp(_angleDegrees, 1f, 360f);
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

    public sealed class AgentConeDamageSkill : AgentCombatSkillBase
    {
        private readonly AgentConeDamageSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        public AgentConeDamageSkill(AgentConeDamageSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool CanCast(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            if (target.EnemyTarget == null)
                return false;

            return AgentCombatSkillUtility.IsInsidePlanarCone(
                context.Position,
                context.Forward,
                target.EnemyTarget.transform.position,
                _config.Radius,
                _config.AngleDegrees);
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
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
