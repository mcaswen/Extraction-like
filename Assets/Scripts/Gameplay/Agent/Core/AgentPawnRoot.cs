using UnityEngine;
using UnityEngine.AI;
using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.SO;

namespace Gameplay.Agent.Core
{
    /// <summary>
    /// Agent实体总入口
    /// 当前阶段负责承载最小身体事实，并桥接 Brain 与干预层
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(AgentCombatShooter), typeof(AgentCombatController))]
    public sealed class AgentPawnRoot : MonoBehaviour, IAgentReadOnly, IAgentCommandReceiver, ICombatDamageReceiver, global::IExternalMovementReceiver
    {
        private const float ExternalImpulseMovementOverrideDuration = 0.45f;
        private const float ExternalImpulseDamping = 10f;
        private const float StopFromMaxSpeedDuration = 0.5f;
        private const int RangeGizmoSegmentCount = 64;
        private static readonly Color TargetDiscoveryRangeGizmoColor = new Color(0.1f, 0.65f, 1f, 0.85f);
        private static readonly Color AttackRangeGizmoColor = new Color(1f, 0.28f, 0.18f, 0.85f);

        [Header("Data")]
        [SerializeField] private string _agentId;
        [SerializeField] private AgentPawnConfig _pawnConfig;

        [Header("Components")]
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private AgentCombatController _combatController;

        [Header("Editor Gizmos")]
        [SerializeField] private bool _showRangeGizmos = true;

        private int _currentHealth;
        private bool _isInitialized;
        private bool _isRegistered;
        private AgentId _runtimeAgentId;

        private AgentBrainController _brainController;
        private AgentInterventionController _interventionController;
        private Vector3 _externalImpulseVelocity;
        private float _externalImpulseMovementOverrideRemaining;
        private float _speedDebuffDurationRemaining;
        private float _speedDebuffMultiplier = 1f;

        /// <summary>
        /// Agent 的强类型运行时 ID
        /// </summary>
        public AgentId AgentId => _runtimeAgentId;

        /// <summary>
        /// Agent 的字符串运行时 ID
        /// </summary>
        public string AgentIdValue => _runtimeAgentId.Value;

        /// <summary>
        /// 当前 Pawn 的缓存 Transform
        /// </summary>
        public Transform CachedTransform => transform;

        /// <summary>
        /// 当前 Pawn 使用的 NavMeshAgent
        /// </summary>
        public NavMeshAgent NavMeshAgent => _navMeshAgent;

        /// <summary>
        /// 当前世界坐标
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// 当前世界朝向
        /// </summary>
        public Vector3 Forward => transform.forward;

        /// <summary>
        /// 当前 Brain 宏状态 ID
        /// </summary>
        public AgentMacroStateId CurrentMacroStateId =>
            _brainController != null ? _brainController.CurrentMacroStateId : AgentMacroStateId.None;

        /// <summary>
        /// 当前 Brain 宏状态名称
        /// </summary>
        public string CurrentMacroStateName => CurrentMacroStateId.ToString();

        /// <summary>
        /// 当前生命值
        /// </summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHealth => _pawnConfig != null ? _pawnConfig.MaxHealth : 0;

        /// <summary>
        /// 当前生命比例
        /// </summary>
        public float HealthRatio => MaxHealth <= 0 ? 0f : (float)_currentHealth / MaxHealth;

        public float Defense => _pawnConfig != null ? _pawnConfig.Defense : 0f;

        /// <summary>
        /// 当前 Pawn 是否死亡
        /// </summary>
        public bool IsDead => _currentHealth <= 0;
        public Transform DamageRootTransform => transform;
        public bool IsCombatDamageReceiverAlive => !IsDead;

        /// <summary>
        /// 是否启用目标发现
        /// </summary>
        public bool EnableTargetDiscovery => _pawnConfig != null && _pawnConfig.EnableTargetDiscovery;

        /// <summary>
        /// 目标发现半径
        /// </summary>
        public float TargetDiscoveryRange => _pawnConfig != null ? _pawnConfig.TargetDiscoveryRange : 0f;

        /// <summary>
        /// 目标发现扫描间隔
        /// </summary>
        public float TargetDiscoveryInterval => _pawnConfig != null ? _pawnConfig.TargetDiscoveryInterval : 0.5f;

        /// <summary>
        /// Brain 使用的运行时黑板
        /// </summary>
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

            float deltaTime = Time.deltaTime;
            TickExternalMovementStatus(deltaTime);

            double timeSeconds = Time.timeAsDouble;

            // 每帧先把身体层事实同步给 Brain
            SyncBodyFactsToBlackboard(timeSeconds);

            // 驱动自主 Brain 更新
            _brainController.Tick(deltaTime, timeSeconds);
            TickExternalImpulseMovement(deltaTime);
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

            if (_combatController != null)
            {
                _combatController.ApplyConfig(
                    _pawnConfig.CombatStyleConfig,
                    _pawnConfig.CreateCombatRuntimeStats());
            }

            _currentHealth = MaxHealth;

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

            ApplyMitigatedDamage(Mathf.Max(0, damageRequest.DamageAmount));
        }

        public float TakeCombatDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, GameObject source)
        {
            if (IsDead || damage <= 0f)
                return 0f;

            int previousHealth = _currentHealth;
            ApplyMitigatedDamage(damage);
            return Mathf.Max(0, previousHealth - _currentHealth);
        }

        private void ApplyMitigatedDamage(float rawDamage)
        {
            if (rawDamage <= 0f)
                return;

            float mitigatedDamage = rawDamage * global::CombatDamageUtility.CalculateDefenseDamageMultiplier(Defense);
            _currentHealth = Mathf.Max(0, _currentHealth - Mathf.RoundToInt(mitigatedDamage));
            SyncBodyFactsToBlackboard(Time.timeAsDouble);
        }

        public void ApplyExternalPull(Vector3 targetPosition, float pullStrength)
        {
            if (IsDead || pullStrength <= 0f)
                return;

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            _externalImpulseVelocity += direction.normalized * pullStrength;
            _externalImpulseMovementOverrideRemaining = Mathf.Max(
                _externalImpulseMovementOverrideRemaining,
                ExternalImpulseMovementOverrideDuration);
            StopNavMeshForExternalMovement();
        }

        public void ApplyExternalImpulse(Vector3 direction, float strength)
        {
            if (IsDead || strength <= 0f)
                return;

            Vector3 planarDirection = direction;
            planarDirection.y = 0f;
            if (planarDirection.sqrMagnitude <= 0.0001f)
                return;

            _externalImpulseVelocity += planarDirection.normalized * strength;
            _externalImpulseMovementOverrideRemaining = Mathf.Max(
                _externalImpulseMovementOverrideRemaining,
                ExternalImpulseMovementOverrideDuration);
            StopNavMeshForExternalMovement();
        }

        public void ApplyMoveSpeedDebuff(float multiplier, float duration)
        {
            if (duration <= 0f || multiplier <= 0f)
                return;

            _speedDebuffDurationRemaining = Mathf.Max(_speedDebuffDurationRemaining, duration);
            _speedDebuffMultiplier = Mathf.Min(_speedDebuffMultiplier, Mathf.Clamp(multiplier, 0.1f, 1f));
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

            if (_combatController == null)
                _combatController = GetComponent<AgentCombatController>();
        }

        private void RegisterWithRuntime()
        {
            if (_isRegistered)
                return;

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();
            _isRegistered = registry.Register(this);
            if (_isRegistered)
            {
                AgentTargetDiscoveryController.GetOrCreate();
            }
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
            _brainController.SetFact(AgentBlackboardKeys.MoveSpeed, GetEffectiveMoveSpeed(), timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.MoveStoppingDistance, _pawnConfig.MoveStoppingDistance, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.InteractionDistance, _pawnConfig.InteractionDistance, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackRange, _pawnConfig.AttackRange, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackDamage, _pawnConfig.AttackDamage, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AttackInterval, _pawnConfig.AttackInterval, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.HasPendingDirective, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionModuleEnabled, false, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionTargetId, string.Empty, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionTargetKind, AgentDecisionTargetKind.None, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionScore, 0f, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionRisk, 0f, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionCandidateCount, 0, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionRiskEnemyCount, 0, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionAttack, _pawnConfig.AttackDamage, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionDefense, _pawnConfig.Defense, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.DecisionReason, string.Empty, timeSeconds);
        }

        // 将 Pawn 身体层事实同步给 Brain
        private void SyncBodyFactsToBlackboard(double timeSeconds)
        {
            bool isDead = IsDead;
            bool needRecovery = !isDead && HealthRatio <= _pawnConfig.LowHealthRecoveryThreshold;

            _brainController.SetFact(AgentBlackboardKeys.AgentIsDead, isDead, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.AgentHealthRatio, HealthRatio, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.NeedRecovery, needRecovery, timeSeconds);
            _brainController.SetFact(AgentBlackboardKeys.MoveSpeed, GetEffectiveMoveSpeed(), timeSeconds);
        }

        private void TickExternalMovementStatus(float deltaTime)
        {
            _speedDebuffDurationRemaining = Mathf.Max(0f, _speedDebuffDurationRemaining - deltaTime);
            if (_speedDebuffDurationRemaining <= 0f)
            {
                _speedDebuffMultiplier = 1f;
            }
        }

        private void TickExternalImpulseMovement(float deltaTime)
        {
            if (deltaTime <= 0f || IsDead)
                return;

            bool hasActiveImpulse = _externalImpulseVelocity.sqrMagnitude > 0.0001f;
            if (!hasActiveImpulse && _externalImpulseMovementOverrideRemaining <= 0f)
                return;

            StopNavMeshForExternalMovement();

            Vector3 displacement = _externalImpulseVelocity * deltaTime;
            displacement.y = 0f;
            if (displacement.sqrMagnitude > 0.000001f)
            {
                if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
                {
                    _navMeshAgent.Move(displacement);
                }
                else
                {
                    transform.position += displacement;
                }
            }

            float stopDeceleration = GetEffectiveMoveSpeed() / StopFromMaxSpeedDuration;
            _externalImpulseVelocity = DecayVelocity(
                _externalImpulseVelocity,
                Mathf.Max(ExternalImpulseDamping, stopDeceleration),
                deltaTime);
            _externalImpulseMovementOverrideRemaining = Mathf.Max(
                0f,
                _externalImpulseMovementOverrideRemaining - deltaTime);
        }

        private float GetEffectiveMoveSpeed()
        {
            float baseMoveSpeed = _pawnConfig != null ? _pawnConfig.MoveSpeed : 0f;
            return baseMoveSpeed * (_speedDebuffDurationRemaining > 0f ? _speedDebuffMultiplier : 1f);
        }

        private static Vector3 DecayVelocity(Vector3 velocity, float deceleration, float deltaTime)
        {
            return Vector3.MoveTowards(
                velocity,
                Vector3.zero,
                Mathf.Max(0f, deceleration) * Mathf.Max(0f, deltaTime));
        }

        private void StopNavMeshForExternalMovement()
        {
            if (_navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
                return;

            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            if (_navMeshAgent.hasPath)
                _navMeshAgent.ResetPath();
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
