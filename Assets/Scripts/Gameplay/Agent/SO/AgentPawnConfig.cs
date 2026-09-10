using UnityEngine;
using Gameplay.Agent.Combat;

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
        [SerializeField, Min(0f)] private float _defense = 100f;

        [Header("行动参数")]
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _moveStoppingDistance = 0.25f;
        [SerializeField] private float _interactionDistance = 0f;
        [SerializeField, Min(0.1f)] private float _navigationReadyTimeout = 2f;
        [SerializeField, Min(0.1f)] private float _navigationProgressTimeout = 3f;
        [SerializeField, Min(0.1f)] private float _combatLostSightTimeout = 2f;
        public float NavigationReadyTimeout => Mathf.Max(0.1f, _navigationReadyTimeout);
        public float NavigationProgressTimeout => Mathf.Max(0.1f, _navigationProgressTimeout);
        public float CombatLostSightTimeout => Mathf.Max(0.1f, _combatLostSightTimeout);

        [Header("目标发现参数")]
        [SerializeField] private bool _enableTargetDiscovery = true;
        [SerializeField] private float _targetDiscoveryRange = 30f;
        [SerializeField] private float _targetDiscoveryInterval = 0.5f;

        [Header("战斗参数")]
        [SerializeField] private AgentCombatStyleConfig _combatStyleConfig;
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
        /// Agent 攻击基础值
        /// </summary>
        public float Attack => _attackDamage;

        /// <summary>
        /// Agent 防御基础值
        /// </summary>
        public float Defense => Mathf.Max(0f, _defense);

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
        /// Agent 战斗流派配置
        /// </summary>
        public AgentCombatStyleConfig CombatStyleConfig => _combatStyleConfig;

        /// <summary>
        /// 攻击有效距离
        /// </summary>
        public float AttackRange => _combatStyleConfig != null ? _combatStyleConfig.NormalAttackRange : _attackRange;

        /// <summary>
        /// 单次攻击伤害
        /// </summary>
        public float AttackDamage => _combatStyleConfig != null
            ? _combatStyleConfig.CalculateNormalAttackDamage(CreateCombatRuntimeStats())
            : _attackDamage;

        /// <summary>
        /// 攻击间隔
        /// </summary>
        public float AttackInterval => _combatStyleConfig != null ? _combatStyleConfig.NormalAttackInterval : _attackInterval;

        /// <summary>
        /// 进入恢复需求的生命比例阈值
        /// </summary>
        public float LowHealthRecoveryThreshold => _lowHealthRecoveryThreshold;

        public AgentCombatRuntimeStats CreateCombatRuntimeStats()
        {
            return new AgentCombatRuntimeStats(MaxHealth, Attack, Defense);
        }
    }
}
