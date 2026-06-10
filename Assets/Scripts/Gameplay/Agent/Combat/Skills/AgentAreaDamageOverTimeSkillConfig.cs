using UnityEngine;

namespace Gameplay.Agent.Combat
{
    [CreateAssetMenu(
        fileName = "SO_Agent_AreaDamageOverTimeSkill",
        menuName = "SO/Agent/Combat/Skills/Area Damage Over Time")]
    public sealed class AgentAreaDamageOverTimeSkillConfig : AgentCombatSkillConfigBase
    {
        [Header("Area Over Time")]
        [SerializeField] private AgentCombatStatScaling _damagePerSecond =
            new AgentCombatStatScaling(AgentCombatStatScalingSource.Defense, 0.5f);
        [SerializeField, Min(0.1f)] private float _radius = 4f;
        [SerializeField, Min(0.05f)] private float _durationSeconds = 5f;
        [SerializeField, Min(0.05f)] private float _tickInterval = 1f;
        [SerializeField] private AgentCombatStatusEffectDefinition _statusEffect;

        [Header("Visual")]
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private Color _indicatorColor = new Color(0.56f, 0.42f, 0.24f, 0.55f);

        public float Radius => Mathf.Max(0.1f, _radius);
        public float DurationSeconds => Mathf.Max(0.05f, _durationSeconds);
        public float TickInterval => Mathf.Max(0.05f, _tickInterval);
        public AgentCombatStatusEffectDefinition StatusEffect => _statusEffect;
        public GameObject VisualPrefab => _visualPrefab;
        public Color IndicatorColor => _indicatorColor;

        public float CalculateDamagePerSecond(AgentCombatRuntimeStats stats)
        {
            return _damagePerSecond.Evaluate(stats);
        }

        public override AgentCombatSkillBase CreateRuntimeSkill()
        {
            return new AgentAreaDamageOverTimeSkill(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _radius = Mathf.Max(0.1f, _radius);
            _durationSeconds = Mathf.Max(0.05f, _durationSeconds);
            _tickInterval = Mathf.Max(0.05f, _tickInterval);
        }
    }

    public sealed class AgentAreaDamageOverTimeSkill : AgentCombatSkillBase
    {
        private readonly AgentAreaDamageOverTimeSkillConfig _config;

        public AgentAreaDamageOverTimeSkill(AgentAreaDamageOverTimeSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            Vector3 center = AgentCombatSkillUtility.ResolveTargetPosition(context, target);
            GameObject areaObject = new GameObject("AgentAreaDamageOverTime");
            areaObject.transform.position = center;
            AgentCombatAreaDamageOverTime damageArea = areaObject.AddComponent<AgentCombatAreaDamageOverTime>();
            damageArea.Initialize(
                context.CasterTransform,
                context.EnemyLayerMask,
                _config.Radius,
                _config.CalculateDamagePerSecond(context.Stats),
                _config.DurationSeconds,
                _config.TickInterval,
                _config.StatusEffect,
                _config.VisualPrefab,
                _config.IndicatorColor);
            return true;
        }
    }
}
