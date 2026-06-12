using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 持续区域伤害技能配置
    /// 定义区域半径、持续时间、tick 间隔和状态效果
    /// </summary>
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

        /// <summary>
        /// 区域伤害半径
        /// </summary>
        public float Radius => Mathf.Max(0.1f, _radius);

        /// <summary>
        /// 区域持续时间
        /// </summary>
        public float DurationSeconds => Mathf.Max(0.05f, _durationSeconds);

        /// <summary>
        /// 伤害结算间隔
        /// </summary>
        public float TickInterval => Mathf.Max(0.05f, _tickInterval);

        /// <summary>
        /// 每次 tick 附加的状态效果
        /// </summary>
        public AgentCombatStatusEffectDefinition StatusEffect => _statusEffect;

        /// <summary>
        /// 可选的区域视觉预制体
        /// </summary>
        public GameObject VisualPrefab => _visualPrefab;

        /// <summary>
        /// 未配置视觉预制体时使用的默认指示颜色
        /// </summary>
        public Color IndicatorColor => _indicatorColor;

        /// <summary>
        /// 根据 Agent 当前战斗属性计算每秒伤害
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public float CalculateDamagePerSecond(AgentCombatRuntimeStats stats)
        {
            return _damagePerSecond.Evaluate(stats);
        }

        /// <summary>
        /// 创建该配置对应的运行时技能实例
        /// </summary>
        /// <returns></returns>
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

    /// <summary>
    /// Agent 持续区域伤害技能运行时逻辑
    /// 负责在目标位置生成一个持续伤害区域
    /// </summary>
    public sealed class AgentAreaDamageOverTimeSkill : AgentCombatSkillBase
    {
        private readonly AgentAreaDamageOverTimeSkillConfig _config;

        /// <summary>
        /// 创建持续区域伤害技能实例
        /// </summary>
        /// <param name="config"></param>
        public AgentAreaDamageOverTimeSkill(AgentAreaDamageOverTimeSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            // 先解析目标点，敌人目标优先落在敌人位置，否则使用上下文前方点
            Vector3 center = AgentCombatSkillUtility.ResolveTargetPosition(context, target);

            // 再创建运行时区域实体，由实体自己负责 tick 和生命周期
            GameObject areaObject = new GameObject("AgentAreaDamageOverTime");
            areaObject.transform.position = center;
            AgentCombatAreaDamageOverTime damageArea = areaObject.AddComponent<AgentCombatAreaDamageOverTime>();
            damageArea.Initialize(
                context.CasterTransform,
                context.EnemyLayerMask,
                _config.Radius,
                _config.CalculateDamagePerSecond(context.Stats) * context.SkillModifiers.DamageMultiplier,
                _config.DurationSeconds,
                _config.TickInterval,
                _config.StatusEffect,
                context.SkillModifiers.SlowDurationBonusSeconds,
                _config.VisualPrefab,
                _config.IndicatorColor);
            return true;
        }
    }
}
