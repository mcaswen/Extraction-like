using System.Collections.Generic;
using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 土墙技能配置
    /// 定义墙体尺寸、持续时间和生成位置
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_WallSkill",
        menuName = "SO/Agent/Combat/Skills/Wall")]
    public sealed class AgentWallSkillConfig : AgentCombatSkillConfigBase
    {
        [Header("Wall")]
        [SerializeField] private AgentCombatStatScaling _damage =
            new AgentCombatStatScaling(AgentCombatStatScalingSource.Defense, 0.8f);
        [SerializeField, Min(0.1f)] private float _length = 4f;
        [SerializeField, Min(0.1f)] private float _width = 0.65f;
        [SerializeField, Min(0.1f)] private float _height = 2.4f;
        [SerializeField, Min(0f)] private float _forwardDistance = 1.8f;
        [SerializeField, Min(0.05f)] private float _durationSeconds = 10f;
        [SerializeField] private GameObject _wallPrefab;
        [SerializeField] private Color _wallColor = new Color(0.46f, 0.36f, 0.28f, 1f);

        /// <summary>
        /// 墙体长度
        /// </summary>
        public float Length => Mathf.Max(0.1f, _length);

        /// <summary>
        /// 墙体厚度
        /// </summary>
        public float Width => Mathf.Max(0.1f, _width);

        /// <summary>
        /// 墙体高度
        /// </summary>
        public float Height => Mathf.Max(0.1f, _height);

        /// <summary>
        /// 墙体中心相对施法者前方的距离
        /// </summary>
        public float ForwardDistance => Mathf.Max(0f, _forwardDistance);

        /// <summary>
        /// 墙体持续时间
        /// </summary>
        public float DurationSeconds => Mathf.Max(0.05f, _durationSeconds);

        /// <summary>
        /// 可选墙体预制体
        /// </summary>
        public GameObject WallPrefab => _wallPrefab;

        /// <summary>
        /// 默认墙体颜色
        /// </summary>
        public Color WallColor => _wallColor;

        /// <summary>
        /// 根据运行时属性计算生成冲击伤害
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
            return new AgentWallSkill(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _length = Mathf.Max(0.1f, _length);
            _width = Mathf.Max(0.1f, _width);
            _height = Mathf.Max(0.1f, _height);
            _forwardDistance = Mathf.Max(0f, _forwardDistance);
            _durationSeconds = Mathf.Max(0.05f, _durationSeconds);
        }
    }

    /// <summary>
    /// Agent 土墙技能运行时逻辑
    /// 负责生成临时墙体并对生成范围内敌人造成冲击伤害
    /// </summary>
    public sealed class AgentWallSkill : AgentCombatSkillBase
    {
        private static readonly Collider[] HitBuffer = new Collider[64];

        private readonly AgentWallSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        /// <summary>
        /// 创建土墙技能实例
        /// </summary>
        /// <param name="config"></param>
        public AgentWallSkill(AgentWallSkillConfig config)
            : base(config)
        {
            _config = config;
        }

        protected override bool CanCast(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            if (target.EnemyTarget == null)
                return false;

            Vector3 toTarget = target.EnemyTarget.transform.position - context.Position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return true;

            return Vector3.Dot(context.Forward, toTarget.normalized) > 0.2f;
        }

        protected override bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            Vector3 forward = context.Forward;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            // 墙体中心抬高半个高度，使底部落在地面附近
            Vector3 center = context.Position + forward * _config.ForwardDistance + Vector3.up * (_config.Height * 0.5f);
            Vector3 size = new Vector3(_config.Length, _config.Height, _config.Width);
            float durationSeconds = Mathf.Max(0.05f, _config.DurationSeconds + context.SkillModifiers.DurationBonusSeconds);

            bool playedPrototype = AgentPrototypeSkillVfxBridge.TryPlayStoneWall(
                context,
                _config,
                center,
                forward,
                _config.Length,
                _config.Width,
                _config.Height,
                durationSeconds);
            if (!playedPrototype || !context.SuppressConfiguredSkillVfx)
            {
                // 有预制体时使用美术资产，否则生成最小 Cube 兜底
                GameObject wallObject = _config.WallPrefab != null
                    ? Object.Instantiate(_config.WallPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallObject.name = "AgentEarthWall";
                wallObject.transform.SetPositionAndRotation(center, rotation);
                wallObject.transform.localScale = size;
                SkillEffectLayerUtility.ApplyToRoot(wallObject);

                if (wallObject.GetComponentInChildren<Collider>() == null)
                    wallObject.AddComponent<BoxCollider>();

                Rigidbody rigidbodyComponent = wallObject.GetComponent<Rigidbody>();
                if (rigidbodyComponent == null)
                    rigidbodyComponent = wallObject.AddComponent<Rigidbody>();

                rigidbodyComponent.isKinematic = true;
                rigidbodyComponent.useGravity = false;
                AgentCombatSkillUtility.ApplyColor(wallObject, _config.WallColor);
                Object.Destroy(wallObject, durationSeconds);
            }

            // 墙体生成瞬间对重叠敌人结算一次冲击伤害
            ApplyImpactDamage(context, center, rotation, size);
            return true;
        }

        private void ApplyImpactDamage(
            AgentCombatSkillContext context,
            Vector3 center,
            Quaternion rotation,
            Vector3 size)
        {
            _targets.Clear();
            // 使用墙体同尺寸盒体查询，保证伤害范围与实际墙体体积一致
            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                size * 0.5f,
                HitBuffer,
                rotation,
                context.EnemyLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = HitBuffer[i];
                if (hit == null)
                    continue;

                global::EnemyHealthController enemyHealth =
                    hit.GetComponentInParent<global::EnemyHealthController>();
                if (enemyHealth != null && enemyHealth.IsAlive)
                    _targets.Add(enemyHealth);
            }

            float damage = _config.CalculateDamage(context.Stats) * context.SkillModifiers.DamageMultiplier;
            foreach (global::EnemyHealthController enemyHealth in _targets)
            {
                AgentCombatSkillUtility.ApplyDamageAndStatus(
                    enemyHealth,
                    damage,
                    context,
                    enemyHealth.transform.position,
                    global::EnemyDamageSourceType.Magic,
                    default);
            }
        }
    }
}
