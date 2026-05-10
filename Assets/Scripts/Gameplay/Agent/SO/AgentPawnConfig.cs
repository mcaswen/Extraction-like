using UnityEngine;

namespace Gameplay.Agent.SO
{
    /// <summary>
    /// Agent身体层静态配置
    /// 当前阶段先只承载 Pawn Root 真正会用到的最小基础参数
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_PawnConfig",
        menuName = "SO/Agent/PawnConfig")]
    public sealed class AgentPawnConfig : ScriptableObject
    {
        [Header("基础属性")]
        [SerializeField] private int _maxHealth = 100;

        [Header("行动参数")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _moveStoppingDistance = 0.25f;
        [SerializeField] private float _interactionDistance = 1.5f;

        [Header("目标发现参数")]
        [SerializeField] private bool _enableTargetDiscovery = true;
        [SerializeField] private float _targetDiscoveryRange = 30f;
        [SerializeField] private float _targetDiscoveryInterval = 0.5f;

        [Header("战斗参数")]
        [SerializeField] private float _attackRange = 6f;
        [SerializeField] private float _attackDamage = 25f;
        [SerializeField] private float _attackInterval = 0.65f;

        [Header("恢复相关")]
        [SerializeField] private float _lowHealthRecoveryThreshold = 0.35f;

        public int MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float MoveStoppingDistance => _moveStoppingDistance;
        public float InteractionDistance => _interactionDistance;
        public bool EnableTargetDiscovery => _enableTargetDiscovery;
        public float TargetDiscoveryRange => _targetDiscoveryRange;
        public float TargetDiscoveryInterval => _targetDiscoveryInterval;
        public float AttackRange => _attackRange;
        public float AttackDamage => _attackDamage;
        public float AttackInterval => _attackInterval;
        public float LowHealthRecoveryThreshold => _lowHealthRecoveryThreshold;
    }
}
