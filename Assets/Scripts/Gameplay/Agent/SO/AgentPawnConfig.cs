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

        /// <summary>
        /// Agent 最大生命值
        /// </summary>
        public int MaxHealth => _maxHealth;

        /// <summary>
        /// Agent 默认移动速度
        /// </summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>
        /// 普通移动目标停止距离
        /// </summary>
        public float MoveStoppingDistance => _moveStoppingDistance;

        /// <summary>
        /// 交互目标停止距离
        /// </summary>
        public float InteractionDistance => _interactionDistance;

        /// <summary>
        /// 是否启用运行时目标发现
        /// </summary>
        public bool EnableTargetDiscovery => _enableTargetDiscovery;

        /// <summary>
        /// 目标发现半径
        /// </summary>
        public float TargetDiscoveryRange => _targetDiscoveryRange;

        /// <summary>
        /// 目标发现扫描间隔
        /// </summary>
        public float TargetDiscoveryInterval => _targetDiscoveryInterval;

        /// <summary>
        /// 攻击有效距离
        /// </summary>
        public float AttackRange => _attackRange;

        /// <summary>
        /// 单次攻击伤害
        /// </summary>
        public float AttackDamage => _attackDamage;

        /// <summary>
        /// 攻击间隔
        /// </summary>
        public float AttackInterval => _attackInterval;

        /// <summary>
        /// 进入恢复需求的生命比例阈值
        /// </summary>
        public float LowHealthRecoveryThreshold => _lowHealthRecoveryThreshold;
    }
}
