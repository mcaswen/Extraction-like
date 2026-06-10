using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
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

        public float Length => Mathf.Max(0.1f, _length);
        public float Width => Mathf.Max(0.1f, _width);
        public float Height => Mathf.Max(0.1f, _height);
        public float ForwardDistance => Mathf.Max(0f, _forwardDistance);
        public float DurationSeconds => Mathf.Max(0.05f, _durationSeconds);
        public GameObject WallPrefab => _wallPrefab;
        public Color WallColor => _wallColor;

        public float CalculateDamage(AgentCombatRuntimeStats stats)
        {
            return _damage.Evaluate(stats);
        }

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

    public sealed class AgentWallSkill : AgentCombatSkillBase
    {
        private static readonly Collider[] HitBuffer = new Collider[64];

        private readonly AgentWallSkillConfig _config;
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

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
            Vector3 center = context.Position + forward * _config.ForwardDistance + Vector3.up * (_config.Height * 0.5f);
            Vector3 size = new Vector3(_config.Length, _config.Height, _config.Width);

            GameObject wallObject = _config.WallPrefab != null
                ? Object.Instantiate(_config.WallPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "AgentEarthWall";
            wallObject.transform.SetPositionAndRotation(center, rotation);
            wallObject.transform.localScale = size;

            if (wallObject.GetComponentInChildren<Collider>() == null)
                wallObject.AddComponent<BoxCollider>();

            Rigidbody rigidbodyComponent = wallObject.GetComponent<Rigidbody>();
            if (rigidbodyComponent == null)
                rigidbodyComponent = wallObject.AddComponent<Rigidbody>();

            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.useGravity = false;
            AgentCombatSkillUtility.ApplyColor(wallObject, _config.WallColor);
            Object.Destroy(wallObject, _config.DurationSeconds);

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

            float damage = _config.CalculateDamage(context.Stats);
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
