using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 运行时调试视图
    /// 只负责把 AgentPawnRoot 和 Blackboard 中的运行时状态同步到 Inspector，便于 Play Mode 下观察决策状态和目标
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AgentPawnRoot))]
    public sealed class AgentRuntimeDebugView : MonoBehaviour
    {
        [Header("数据源")]
        [SerializeField] private AgentPawnRoot _agent;
        [SerializeField, Min(0.02f)] private float _refreshIntervalSeconds = 0.1f;

        [Header("身份")]
        [SerializeField] private string _agentId;
        [SerializeField] private AgentMacroStateId _macroStateId;
        [SerializeField] private string _macroStateName;

        [Header("生命状态")]
        [SerializeField] private int _currentHealth;
        [SerializeField] private int _maxHealth;
        [SerializeField] private float _healthRatio;
        [SerializeField] private bool _isDead;
        [SerializeField] private bool _needRecovery;

        [Header("黑板事实")]
        [SerializeField] private bool _hasVisibleEnemy;
        [SerializeField] private bool _hasEnemySourceTarget;
        [SerializeField] private bool _hasResourceTarget;
        [SerializeField] private bool _hasInteractableTarget;
        [SerializeField] private bool _shouldExtract;
        [SerializeField] private bool _hasPendingDirective;

        [Header("决策模块")]
        [SerializeField] private bool _decisionModuleEnabled;
        [SerializeField] private string _decisionSummary;
        [SerializeField] private AgentDecisionTargetKind _decisionTargetKind;
        [SerializeField] private string _decisionTargetId;
        [SerializeField] private float _decisionScore;
        [SerializeField] private float _decisionRisk;
        [SerializeField] private int _decisionCandidateCount;
        [SerializeField] private int _decisionRiskEnemyCount;
        [SerializeField] private float _decisionAttack;
        [SerializeField] private float _decisionDefense;
        [SerializeField] private string _decisionReason;

        [Header("当前指令")]
        [SerializeField] private AgentDirectiveType _directiveType;
        [SerializeField] private AgentTargetKind _targetKind;
        [SerializeField] private AgentTargetBindingType _targetBindingType;
        [SerializeField] private string _targetId;
        [SerializeField] private GameObject _targetObject;
        [SerializeField] private Vector3 _targetPosition;
        [SerializeField] private bool _hasTargetPosition;
        [SerializeField] private string _payloadId;
        [SerializeField] private string _commandId;
        [SerializeField] private int _priority;

        [Header("当前具体实例目标")]
        [SerializeField] private AgentTargetKind _concreteTargetKind;
        [SerializeField] private GameObject _concreteTargetObject;
        [SerializeField] private string _concreteTargetName;
        [SerializeField] private int _concreteTargetInstanceId;
        [SerializeField] private Vector3 _concreteTargetPosition;
        [SerializeField] private bool _hasConcreteNavigationPosition;
        [SerializeField] private Vector3 _concreteNavigationPosition;
        [SerializeField] private string _concreteTargetSummary;

        [Header("导航状态")]
        [SerializeField] private bool _hasNavMeshAgent;
        [SerializeField] private bool _isOnNavMesh;
        [SerializeField] private bool _pathPending;
        [SerializeField] private bool _hasPath;
        [SerializeField] private NavMeshPathStatus _pathStatus;
        [SerializeField] private bool _isStopped;
        [SerializeField] private Vector3 _destination;
        [SerializeField] private float _remainingDistance;
        [SerializeField] private bool _hasReliableRemainingDistance;
        [SerializeField] private float _stoppingDistance;
        [SerializeField] private float _speed;
        [SerializeField] private Vector3 _velocity;
        [SerializeField] private float _velocityMagnitude;

        [Header("移动控制判定")]
        [SerializeField] private float _setDestinationIntervalSeconds;
        [SerializeField] private bool _navMeshControlsMovement;
        [SerializeField] private string _movementControlSummary;
        [SerializeField] private bool _hasRigidbody;
        [SerializeField] private bool _rigidbodyUseGravity;
        [SerializeField] private bool _rigidbodyIsKinematic;
        [SerializeField] private bool _movementControllerNavMeshDrivingRigidbody;

        [Header("调试")]
        [SerializeField] private string _status;
        [SerializeField] private float _lastRefreshTime;

        private float _refreshTimer;

        private void Reset()
        {
            CacheAgent();
            RefreshDebugView();
        }

        private void Awake()
        {
            CacheAgent();
            RefreshDebugView();
        }

        private void OnValidate()
        {
            CacheAgent();
        }

        private void OnEnable()
        {
            CacheAgent();
            _refreshTimer = 0f;
            RefreshDebugView();
        }

        private void Update()
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < _refreshIntervalSeconds)
            {
                return;
            }

            _refreshTimer = 0f;
            RefreshDebugView();
        }

        /// <summary>
        /// 手动刷新当前 Inspector 调试快照
        /// </summary>
        [ContextMenu("刷新调试视图")]
        public void RefreshDebugView()
        {
            CacheAgent();

            if (_agent == null)
            {
                ClearSnapshot("缺少 AgentPawnRoot");
                return;
            }

            _agentId = _agent.AgentIdValue;
            _macroStateId = _agent.CurrentMacroStateId;
            _macroStateName = ResolveMacroStateName();
            _currentHealth = _agent.CurrentHealth;
            _maxHealth = _agent.MaxHealth;
            _healthRatio = _agent.HealthRatio;
            _isDead = _agent.IsDead;

            RefreshBlackboardSnapshot();
            RefreshNavMeshSnapshot(_agent.NavMeshAgent);
            RefreshMovementControlSnapshot(_agent.NavMeshAgent);

            _status = _agent.Blackboard != null ? "运行中" : "等待运行时 Blackboard 初始化";
            _lastRefreshTime = Application.isPlaying ? Time.time : 0f;
        }

        // 缓存 AgentPawnRoot 引用，避免 Update 中重复查找其他对象
        private void CacheAgent()
        {
            if (_agent == null)
            {
                _agent = GetComponent<AgentPawnRoot>();
            }
        }

        // 从黑板中读取决策事实和当前指令，Inspector 中的字段只作为观察快照
        private void RefreshBlackboardSnapshot()
        {
            if (_agent.Blackboard == null)
            {
                ClearBlackboardSnapshot();
                return;
            }

            // 黑板事实区只反映当前状态机输入，不主动推导目标
            _needRecovery = GetBlackboardValue(AgentBlackboardKeys.NeedRecovery, false);
            _hasVisibleEnemy = GetBlackboardValue(AgentBlackboardKeys.HasVisibleEnemy, false);
            _hasEnemySourceTarget = GetBlackboardValue(AgentBlackboardKeys.HasEnemySourceTarget, false);
            _hasResourceTarget = GetBlackboardValue(AgentBlackboardKeys.HasResourceTarget, false);
            _hasInteractableTarget = GetBlackboardValue(AgentBlackboardKeys.HasInteractableTarget, false);
            _shouldExtract = GetBlackboardValue(AgentBlackboardKeys.ShouldExtract, false);
            _hasPendingDirective = GetBlackboardValue(AgentBlackboardKeys.HasPendingDirective, false);
            _decisionModuleEnabled = GetBlackboardValue(AgentBlackboardKeys.DecisionModuleEnabled, false);
            _decisionTargetKind = GetBlackboardValue(
                AgentBlackboardKeys.DecisionTargetKind,
                AgentDecisionTargetKind.None);
            _decisionTargetId = GetBlackboardValue(AgentBlackboardKeys.DecisionTargetId, string.Empty);
            _decisionScore = GetBlackboardValue(AgentBlackboardKeys.DecisionScore, 0f);
            _decisionRisk = GetBlackboardValue(AgentBlackboardKeys.DecisionRisk, 0f);
            _decisionCandidateCount = GetBlackboardValue(AgentBlackboardKeys.DecisionCandidateCount, 0);
            _decisionRiskEnemyCount = GetBlackboardValue(AgentBlackboardKeys.DecisionRiskEnemyCount, 0);
            _decisionAttack = GetBlackboardValue(AgentBlackboardKeys.DecisionAttack, 0f);
            _decisionDefense = GetBlackboardValue(AgentBlackboardKeys.DecisionDefense, 0f);
            _decisionReason = GetBlackboardValue(AgentBlackboardKeys.DecisionReason, string.Empty);
            _decisionSummary = BuildDecisionSummary();

            // 指令区保留原始目标引用，同时额外解析行为节点实际使用的具体目标
            if (_agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest))
            {
                ApplyDirectiveSnapshot(directiveRequest);
                return;
            }

            ClearDirectiveSnapshot();
        }

        private string ResolveMacroStateName()
        {
            if (_agent.Blackboard != null &&
                _agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.CurrentMacroStateName,
                    out string blackboardStateName) &&
                !string.IsNullOrEmpty(blackboardStateName))
            {
                return blackboardStateName;
            }

            return _agent.CurrentMacroStateName;
        }

        // 汇总决策模块的关键结果，Inspector 扫一行就能看出本轮是否选中目标
        private string BuildDecisionSummary()
        {
            if (!_decisionModuleEnabled)
                return string.IsNullOrWhiteSpace(_decisionReason)
                    ? "未启用"
                    : $"未启用 - {_decisionReason}";

            if (_decisionTargetKind == AgentDecisionTargetKind.None ||
                string.IsNullOrWhiteSpace(_decisionTargetId))
            {
                return string.IsNullOrWhiteSpace(_decisionReason)
                    ? $"启用，无目标 candidates={_decisionCandidateCount}"
                    : $"启用，无目标 candidates={_decisionCandidateCount} - {_decisionReason}";
            }

            return $"{_decisionTargetKind} [{_decisionTargetId}] " +
                   $"score={_decisionScore:0.##} risk={_decisionRisk:0.##} " +
                   $"candidates={_decisionCandidateCount}";
        }

        private T GetBlackboardValue<T>(BlackboardKey key, T defaultValue)
        {
            return _agent.Blackboard.TryGetValue(key, out T value) ? value : defaultValue;
        }

        // 将当前指令拆成 Inspector 可读字段，方便定位 Agent 正在处理的目标
        private void ApplyDirectiveSnapshot(AgentDirectiveRequest directiveRequest)
        {
            AgentTargetRef targetRef = directiveRequest.TargetRef;

            _directiveType = directiveRequest.DirectiveType;
            _targetKind = targetRef.Kind;
            _targetBindingType = targetRef.BindingType;
            _targetId = targetRef.TargetId;
            _targetObject = targetRef.TargetObject;
            _targetPosition = targetRef.HasTargetPosition ? targetRef.TargetPosition : default;
            _hasTargetPosition = targetRef.HasTargetPosition;
            _payloadId = directiveRequest.PayloadId;
            _commandId = directiveRequest.CommandId;
            _priority = directiveRequest.Priority;
            ApplyConcreteTargetSnapshot(directiveRequest);
        }

        private void ApplyConcreteTargetSnapshot(AgentDirectiveRequest directiveRequest)
        {
            ClearConcreteTargetSnapshot();

            // 具体目标可能来自群目标展开，解析失败时保持空值，避免误导 Inspector
            if (TryResolveConcreteTarget(
                    directiveRequest,
                    out AgentTargetKind concreteTargetKind,
                    out GameObject concreteTargetObject,
                    out Vector3 navigationPosition,
                    out bool hasNavigationPosition))
            {
                _concreteTargetKind = concreteTargetKind;
                _concreteTargetObject = concreteTargetObject;
                _concreteTargetName = concreteTargetObject != null ? concreteTargetObject.name : string.Empty;
                _concreteTargetInstanceId = concreteTargetObject != null ? concreteTargetObject.GetInstanceID() : 0;
                _concreteTargetPosition = concreteTargetObject != null ? concreteTargetObject.transform.position : default;
                _hasConcreteNavigationPosition = hasNavigationPosition;
                _concreteNavigationPosition = hasNavigationPosition ? navigationPosition : default;
                _concreteTargetSummary = BuildConcreteTargetSummary();
            }
        }

        private bool TryResolveConcreteTarget(
            AgentDirectiveRequest directiveRequest,
            out AgentTargetKind concreteTargetKind,
            out GameObject concreteTargetObject,
            out Vector3 navigationPosition,
            out bool hasNavigationPosition)
        {
            concreteTargetKind = AgentTargetKind.None;
            concreteTargetObject = null;
            navigationPosition = default;
            hasNavigationPosition = false;

            AgentTargetRef targetRef = directiveRequest.TargetRef;
            if (directiveRequest.DirectiveType == AgentDirectiveType.Engage)
            {
                // 接战指令如果指向敌人群，Inspector 展示当前真正会攻击的存活敌人实例
                if (TryGetTargetComponent(targetRef, out ActiveEnemyClusterAuthoring enemyCluster))
                {
                    if (!enemyCluster.TryGetNearestAliveEnemy(_agent.Position, out global::EnemyHealthController enemy))
                        return false;

                    concreteTargetKind = AgentTargetKind.Enemy;
                    concreteTargetObject = enemy.gameObject;
                    return true;
                }

                // 直接敌人目标无需展开，保持原实例引用
                if (TryGetTargetComponent(targetRef, out global::EnemyHealthController directEnemy))
                {
                    concreteTargetKind = AgentTargetKind.Enemy;
                    concreteTargetObject = directEnemy.gameObject;
                    return true;
                }
            }

            if (directiveRequest.DirectiveType == AgentDirectiveType.Search &&
                targetRef.Kind == AgentTargetKind.Resource)
            {
                // 搜索资源群时展示当前可达的具体箱子/掉落物，以及实际 NavMesh 停靠点
                if (TryGetTargetComponent(targetRef, out ResourceClusterAuthoring resourceCluster))
                {
                    if (!resourceCluster.TryGetNearestReachableIncompleteResource(
                            _agent.Position,
                            _agent.NavMeshAgent,
                            out concreteTargetObject,
                            out navigationPosition))
                    {
                        return false;
                    }

                    concreteTargetKind = AgentTargetKind.Resource;
                    hasNavigationPosition = true;
                    return concreteTargetObject != null;
                }

                // 直接资源目标通常来自手动点击具体资源对象
                if (targetRef.TargetObject != null)
                {
                    concreteTargetKind = AgentTargetKind.Resource;
                    concreteTargetObject = targetRef.TargetObject;
                    return true;
                }
            }

            return false;
        }

        private string BuildConcreteTargetSummary()
        {
            if (_concreteTargetObject == null)
                return string.Empty;

            string navigationText = _hasConcreteNavigationPosition
                ? $" nav={FormatVector3(_concreteNavigationPosition)}"
                : string.Empty;
            return $"{_concreteTargetKind} {_concreteTargetName} " +
                   $"#{_concreteTargetInstanceId} pos={FormatVector3(_concreteTargetPosition)}{navigationText}";
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }

        private static bool TryGetTargetComponent<TComponent>(
            AgentTargetRef targetRef,
            out TComponent component)
            where TComponent : Component
        {
            GameObject targetObject = targetRef.TargetObject;
            if (targetObject == null)
            {
                component = null;
                return false;
            }

            component = targetObject.GetComponent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInParent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInChildren<TComponent>();
            return component != null;
        }

        // NavMeshAgent 的部分属性需要先确认 isOnNavMesh，避免编辑态或离开导航面时抛异常
        private void RefreshNavMeshSnapshot(NavMeshAgent navMeshAgent)
        {
            _hasNavMeshAgent = navMeshAgent != null;
            if (navMeshAgent == null)
            {
                ClearNavMeshSnapshot();
                return;
            }

            _speed = navMeshAgent.speed;
            _stoppingDistance = navMeshAgent.stoppingDistance;
            _isOnNavMesh = navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

            // Unity 的 NavMeshAgent 属性在未上 NavMesh 时部分访问会抛异常，先统一清空
            if (!_isOnNavMesh)
            {
                _pathPending = false;
                _hasPath = false;
                _pathStatus = NavMeshPathStatus.PathInvalid;
                _isStopped = false;
                _destination = default;
                _remainingDistance = 0f;
                _hasReliableRemainingDistance = false;
                _velocity = default;
                _velocityMagnitude = 0f;
                return;
            }

            _isStopped = navMeshAgent.isStopped;
            _pathPending = navMeshAgent.pathPending;
            _hasPath = navMeshAgent.hasPath;
            _pathStatus = navMeshAgent.pathStatus;
            _destination = navMeshAgent.destination;
            _remainingDistance = navMeshAgent.remainingDistance;
            _hasReliableRemainingDistance =
                !_pathPending &&
                _hasPath &&
                _pathStatus == NavMeshPathStatus.PathComplete &&
                !float.IsNaN(_remainingDistance) &&
                !float.IsInfinity(_remainingDistance);
            _velocity = navMeshAgent.velocity;
            _velocityMagnitude = _velocity.magnitude;
        }

        private void RefreshMovementControlSnapshot(NavMeshAgent navMeshAgent)
        {
            _setDestinationIntervalSeconds = AgentActionNodeBase.NavMeshDestinationRefreshInterval;

            // 通过 NavMeshAgent、Rigidbody 和移动控制器三方状态判断当前是谁在驱动位移
            bool hasAgent = navMeshAgent != null;
            bool agentEnabled = hasAgent && navMeshAgent.enabled;
            bool isOnNavMesh = agentEnabled && navMeshAgent.isOnNavMesh;
            bool pathPending = isOnNavMesh && navMeshAgent.pathPending;
            bool hasPath = isOnNavMesh && navMeshAgent.hasPath;
            bool agentNotStopped = isOnNavMesh && !navMeshAgent.isStopped;
            _navMeshControlsMovement =
                hasAgent &&
                agentEnabled &&
                isOnNavMesh &&
                (pathPending || hasPath || agentNotStopped);

            Rigidbody attachedRigidbody = GetComponent<Rigidbody>();
            _hasRigidbody = attachedRigidbody != null;
            _rigidbodyUseGravity = attachedRigidbody != null && attachedRigidbody.useGravity;
            _rigidbodyIsKinematic = attachedRigidbody != null && attachedRigidbody.isKinematic;

            global::PlayerMovementController movementController =
                GetComponent<global::PlayerMovementController>();
            _movementControllerNavMeshDrivingRigidbody =
                movementController != null && movementController.IsNavMeshDrivingRigidbody;

            _movementControlSummary =
                $"setDestInterval={_setDestinationIntervalSeconds:0.##}s " +
                $"agent(enabled={agentEnabled}, onMesh={isOnNavMesh}, pending={pathPending}, path={hasPath}, notStopped={agentNotStopped}) " +
                $"rb(kinematic={_rigidbodyIsKinematic}, gravity={_rigidbodyUseGravity}) controllerDriving={_movementControllerNavMeshDrivingRigidbody}";
        }

        private void ClearSnapshot(string status)
        {
            _agentId = string.Empty;
            _macroStateId = AgentMacroStateId.None;
            _macroStateName = string.Empty;
            _currentHealth = 0;
            _maxHealth = 0;
            _healthRatio = 0f;
            _isDead = false;
            ClearBlackboardSnapshot();
            ClearNavMeshSnapshot();
            ClearMovementControlSnapshot();
            _status = status;
            _lastRefreshTime = Application.isPlaying ? Time.time : 0f;
        }

        private void ClearBlackboardSnapshot()
        {
            _needRecovery = false;
            _hasVisibleEnemy = false;
            _hasEnemySourceTarget = false;
            _hasResourceTarget = false;
            _hasInteractableTarget = false;
            _shouldExtract = false;
            _hasPendingDirective = false;
            _decisionModuleEnabled = false;
            _decisionSummary = string.Empty;
            _decisionTargetKind = AgentDecisionTargetKind.None;
            _decisionTargetId = string.Empty;
            _decisionScore = 0f;
            _decisionRisk = 0f;
            _decisionCandidateCount = 0;
            _decisionRiskEnemyCount = 0;
            _decisionAttack = 0f;
            _decisionDefense = 0f;
            _decisionReason = string.Empty;
            ClearDirectiveSnapshot();
        }

        private void ClearDirectiveSnapshot()
        {
            _directiveType = AgentDirectiveType.None;
            _targetKind = AgentTargetKind.None;
            _targetBindingType = AgentTargetBindingType.None;
            _targetId = string.Empty;
            _targetObject = null;
            _targetPosition = default;
            _hasTargetPosition = false;
            _payloadId = string.Empty;
            _commandId = string.Empty;
            _priority = 0;
            ClearConcreteTargetSnapshot();
        }

        private void ClearConcreteTargetSnapshot()
        {
            _concreteTargetKind = AgentTargetKind.None;
            _concreteTargetObject = null;
            _concreteTargetName = string.Empty;
            _concreteTargetInstanceId = 0;
            _concreteTargetPosition = default;
            _hasConcreteNavigationPosition = false;
            _concreteNavigationPosition = default;
            _concreteTargetSummary = string.Empty;
        }

        private void ClearNavMeshSnapshot()
        {
            _hasNavMeshAgent = false;
            _isOnNavMesh = false;
            _pathPending = false;
            _hasPath = false;
            _pathStatus = NavMeshPathStatus.PathInvalid;
            _isStopped = false;
            _destination = default;
            _remainingDistance = 0f;
            _hasReliableRemainingDistance = false;
            _stoppingDistance = 0f;
            _speed = 0f;
            _velocity = default;
            _velocityMagnitude = 0f;
        }

        private void ClearMovementControlSnapshot()
        {
            _setDestinationIntervalSeconds = AgentActionNodeBase.NavMeshDestinationRefreshInterval;
            _navMeshControlsMovement = false;
            _movementControlSummary = string.Empty;
            _hasRigidbody = false;
            _rigidbodyUseGravity = false;
            _rigidbodyIsKinematic = false;
            _movementControllerNavMeshDrivingRigidbody = false;
        }
    }
}
