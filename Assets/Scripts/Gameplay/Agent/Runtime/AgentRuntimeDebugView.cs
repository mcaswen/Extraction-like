using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
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

        [Header("导航状态")]
        [SerializeField] private bool _hasNavMeshAgent;
        [SerializeField] private bool _isOnNavMesh;
        [SerializeField] private bool _pathPending;
        [SerializeField] private bool _hasPath;
        [SerializeField] private bool _isStopped;
        [SerializeField] private Vector3 _destination;
        [SerializeField] private float _remainingDistance;
        [SerializeField] private float _stoppingDistance;
        [SerializeField] private float _speed;
        [SerializeField] private Vector3 _velocity;
        [SerializeField] private float _velocityMagnitude;

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

            _needRecovery = GetBlackboardValue(AgentBlackboardKeys.NeedRecovery, false);
            _hasVisibleEnemy = GetBlackboardValue(AgentBlackboardKeys.HasVisibleEnemy, false);
            _hasEnemySourceTarget = GetBlackboardValue(AgentBlackboardKeys.HasEnemySourceTarget, false);
            _hasResourceTarget = GetBlackboardValue(AgentBlackboardKeys.HasResourceTarget, false);
            _hasInteractableTarget = GetBlackboardValue(AgentBlackboardKeys.HasInteractableTarget, false);
            _shouldExtract = GetBlackboardValue(AgentBlackboardKeys.ShouldExtract, false);
            _hasPendingDirective = GetBlackboardValue(AgentBlackboardKeys.HasPendingDirective, false);

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

            if (!_isOnNavMesh)
            {
                _pathPending = false;
                _hasPath = false;
                _isStopped = false;
                _destination = default;
                _remainingDistance = 0f;
                _velocity = default;
                _velocityMagnitude = 0f;
                return;
            }

            _isStopped = navMeshAgent.isStopped;
            _pathPending = navMeshAgent.pathPending;
            _hasPath = navMeshAgent.hasPath;
            _destination = navMeshAgent.destination;
            _remainingDistance = navMeshAgent.remainingDistance;
            _velocity = navMeshAgent.velocity;
            _velocityMagnitude = _velocity.magnitude;
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
        }

        private void ClearNavMeshSnapshot()
        {
            _hasNavMeshAgent = false;
            _isOnNavMesh = false;
            _pathPending = false;
            _hasPath = false;
            _isStopped = false;
            _destination = default;
            _remainingDistance = 0f;
            _stoppingDistance = 0f;
            _speed = 0f;
            _velocity = default;
            _velocityMagnitude = 0f;
        }
    }
}
