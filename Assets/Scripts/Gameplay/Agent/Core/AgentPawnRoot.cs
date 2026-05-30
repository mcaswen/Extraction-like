using UnityEngine;
using UnityEngine.AI;
using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.SO;

namespace Gameplay.Agent.Core
{
    /// <summary>
    /// Agent实体总入口
    /// 当前阶段负责承载最小身体事实，并桥接 Brain 与干预层
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(AgentCombatShooter))]
    public sealed class AgentPawnRoot : MonoBehaviour, IAgentReadOnly, IAgentCommandReceiver
    {
        private const int RangeGizmoSegmentCount = 64;
        private static readonly Color TargetDiscoveryRangeGizmoColor = new Color(0.1f, 0.65f, 1f, 0.85f);
        private static readonly Color AttackRangeGizmoColor = new Color(1f, 0.28f, 0.18f, 0.85f);

        [Header("Data")]
        [SerializeField] private string _agentId;
        [SerializeField] private AgentPawnConfig _pawnConfig;

        [Header("Components")]
        [SerializeField] private NavMeshAgent _navMeshAgent;

        [Header("Editor Gizmos")]
        [SerializeField] private bool _showRangeGizmos = true;

        private int _currentHealth;
        private bool _isInitialized;
        private bool _isRegistered;
        private AgentId _runtimeAgentId;

        private AgentBrainController _brainController;
        private AgentInterventionController _interventionController;

        public AgentId AgentId => _runtimeAgentId;
        public string AgentIdValue => _runtimeAgentId.Value;
        public Transform CachedTransform => transform;
        public NavMeshAgent NavMeshAgent => _navMeshAgent;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        public AgentMacroStateId CurrentMacroStateId =>
            _brainController != null ? _brainController.CurrentMacroStateId : AgentMacroStateId.None;

        public string CurrentMacroStateName => CurrentMacroStateId.ToString();

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _pawnConfig != null ? _pawnConfig.MaxHealth : 0;
        public float HealthRatio => MaxHealth <= 0 ? 0f : (float)_currentHealth / MaxHealth;
        public bool IsDead => _currentHealth <= 0;
        public bool EnableTargetDiscovery => _pawnConfig != null && _pawnConfig.EnableTargetDiscovery;
        public float TargetDiscoveryRange => _pawnConfig != null ? _pawnConfig.TargetDiscoveryRange : 0f;
        public float TargetDiscoveryInterval => _pawnConfig != null ? _pawnConfig.TargetDiscoveryInterval : 0.5f;

        public BehaviorBlackboard Blackboard =>
            _brainController != null ? _brainController.Blackboard : null;

        private void Awake()
        {
            CacheComponents();
            EnsureInitialized();
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            if (EnsureInitialized())
                RegisterWithRuntime();
        }

        private void OnDisable()
        {
            UnregisterFromRuntime();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showRangeGizmos || _pawnConfig == null)
                return;

            DrawRangeCircle(transform.position, _pawnConfig.TargetDiscoveryRange, TargetDiscoveryRangeGizmoColor);
            DrawRangeCircle(transform.position, _pawnConfig.AttackRange, AttackRangeGizmoColor);
        }

        private void Update()
        {
            if (!_isInitialized)
                return;

            double timeSeconds = Time.timeAsDouble;

            // 每帧先把身体层事实同步给 Brain
            SyncBodyFactsToBlackboard(timeSeconds);

            // 驱动自主 Brain 更新
            _brainController.Tick(Time.deltaTime, timeSeconds);
        }

        /// <summary>
        /// 运行时生成或覆盖 AgentId
        /// 只允许在注册前调用，避免 Registry 中出现悬挂索引
        /// </summary>
        /// <param name="agentId"></param>
        public bool TryAssignAgentId(string agentId)
        {
            return TryAssignAgentId(Gameplay.Agent.Runtime.AgentId.FromString(agentId));
        }

        /// <summary>
        /// 运行时生成或覆盖 AgentId
        /// 只允许在注册前调用，避免 Registry 中出现悬挂索引
        /// </summary>
        /// <param name="agentId"></param>
        public bool TryAssignAgentId(AgentId agentId)
        {
            if (_isRegistered)
            {
                Debug.LogWarning("Agent 已注册到运行时 Registry，不能直接修改 AgentId。", this);
                return false;
            }

            _agentId = agentId.Value;
            _runtimeAgentId = ResolveRuntimeAgentId();

            if (_brainController != null)
                _brainController.SetFact(AgentBlackboardKeys.AgentId, _runtimeAgentId, Time.timeAsDouble);

            return !_runtimeAgentId.IsEmpty;
        }

        private bool EnsureInitialized()
        {
            if (_isInitialized)
                return true;

            CacheComponents();
            _runtimeAgentId = ResolveRuntimeAgentId();

            if (_pawnConfig == null)
            {
                Debug.LogError("AgentPawnRoot 缺少 AgentPawnConfig 配置。", this);
                enabled = false;
                return false;
            }

            _currentHealth = _pawnConfig.MaxHealth;

            _brainController = new AgentBrainController(this);
            _interventionController = new AgentInterventionController(_brainController.Blackboard);

            InitializeBlackboardFacts(Time.timeAsDouble);
            _brainController.Start(Time.timeAsDouble);
            _isInitialized = true;
            return true;
        }

        /// <summary>
        /// 使Agent受到伤害
        /// </summary>
        /// <param name="damageRequest"></param>
        public void ApplyDamage(DamageRequest damageRequest)
        {
            if (IsDead)
                return;

            _currentHealth = Mathf.Max(0, _currentHealth - Mathf.Max(0, damageRequest.DamageAmount));
            SyncBodyFactsToBlackboard(Time.timeAsDouble);
        }

        /// <summary>
        /// 设置Agent是否感知到敌人
        /// </summary>
        /// <param name="hasVisibleEnemy"></param>
        public void SetVisibleEnemy(bool hasVisibleEnemy)
        {
            _brainController.SetFact(
                AgentBlackboardKeys.HasVisibleEnemy,
                hasVisibleEnemy,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置Agent当前是否有可侦查的敌人来源点
        /// </summary>
        /// <param name="hasEnemySourceTarget"></param>
        public void SetHasEnemySourceTarget(bool hasEnemySourceTarget)
        {
            _brainController.SetFact(
                AgentBlackboardKeys.HasEnemySourceTarget,
                hasEnemySourceTarget,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置Agent当前是否有可搜索资源点
        /// </summary>
        /// <param name="hasResourceTarget"></param>
        public void SetHasResourceTarget(bool hasResourceTarget)
        {
            _brainController.SetFact(
                AgentBlackboardKeys.HasResourceTarget,
                hasResourceTarget,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置Agent当前是否有可交互目标
        /// </summary>
        /// <param name="hasInteractableTarget"></param>
        public void SetHasInteractableTarget(bool hasInteractableTarget)
        {
            _brainController.SetFact(
                AgentBlackboardKeys.HasInteractableTarget,
                hasInteractableTarget,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 设置Agent当前是否应该撤离
        /// </summary>
        /// <param name="shouldExtract"></param>
        public void SetShouldExtract(bool shouldExtract)
        {
            _brainController.SetFact(
                AgentBlackboardKeys.ShouldExtract,
                shouldExtract,
                Time.timeAsDouble);
        }

        /// <summary>
        /// 提交一个Agent干预请求
        /// 当前阶段只做缓存，不在 Pawn Root 中解释业务
        /// </summary>
        /// <param name="directiveRequest"></param>
        public void SubmitDirective(AgentDirectiveRequest directiveRequest)
        {
            AgentDirectiveRequest routedRequest = directiveRequest.WithTargetAgentId(AgentId);
            _interventionController.SubmitDirective(routedRequest, Time.timeAsDouble);
        }

        /// <summary>
        /// 清除当前待处理的Agent干预请求
        /// </summary>
        public void ClearDirective()
        {
            _interventionController.ClearDirective(Time.timeAsDouble);
        }

        private AgentId ResolveRuntimeAgentId()
        {
            if (Gameplay.Agent.Runtime.AgentId.TryCreate(_agentId, out AgentId resolvedAgentId))
                return resolvedAgentId;

            return Gameplay.Agent.Runtime.AgentId.FromString($"{gameObject.name}_{GetInstanceID()}");
        }

        private void CacheComponents()
        {
            if (_navMeshAgent == null)
                _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void RegisterWithRuntime()
        {
            if (_isRegistered)
                return;

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();
            _isRegistered = registry.Register(this);
            if (_isRegistered)
                AgentTargetDiscoveryController.GetOrCreate();
        }

        private void UnregisterFromRuntime()
        {
            if (!_isRegistered)
                return;

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry != null)
                registry.Unregister(this);

            _isRegistered = false;
        }

        // 初始化 Brain 启动所需的默认事实，
        // 避免状态机第一帧读取到未初始化的黑板值
        private void InitializeBlackboardFacts(double timeSeconds)
        {
            _brainController.SetFact(AgentBlackboardKeys.AgentId, _runtimeAgentId, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AgentIsDead, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AgentHealthRatio, 1f, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasVisibleEnemy, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasEnemySourceTarget, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasResourceTarget, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasInteractableTarget, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.ShouldExtract, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.NeedRecovery, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.MoveSpeed, _pawnConfig.MoveSpeed, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.MoveStoppingDistance, _pawnConfig.MoveStoppingDistance, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.InteractionDistance, _pawnConfig.InteractionDistance, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackRange, _pawnConfig.AttackRange, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackDamage, _pawnConfig.AttackDamage, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackInterval, _pawnConfig.AttackInterval, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasPendingDirective, false, timeSeconds);
        }

        // 将 Pawn 身体层事实同步给 Brain
        private void SyncBodyFactsToBlackboard(double timeSeconds)
        {
            bool isDead = IsDead;
            bool needRecovery = !isDead && HealthRatio <= _pawnConfig.LowHealthRecoveryThreshold;

            _brainController.SetFact(AgentBlackboardKeys.AgentIsDead, isDead, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AgentHealthRatio, HealthRatio, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.NeedRecovery, needRecovery, timeSeconds);
        }

        private static void DrawRangeCircle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f)
                return;

            Color previousColor = Gizmos.color;
            Gizmos.color = color;

            Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);
            for (int index = 1; index <= RangeGizmoSegmentCount; index++)
            {
                float angle = index / (float)RangeGizmoSegmentCount * Mathf.PI * 2f;
                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }

            Gizmos.color = previousColor;
        }
    }
}
