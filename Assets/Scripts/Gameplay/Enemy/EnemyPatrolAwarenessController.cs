using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人在巡逻阶段的感知子状态。
/// </summary>
public enum EnemyPatrolAwarenessState
{
    Patrol,
    Suspicious,
    Investigate,
    Search
}

[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
/// <summary>
/// 敌人巡逻感知控制器。
/// 将听觉刺激升级为怀疑、调查和搜索行为，并在重新看到目标时交还给主战斗状态机。
/// </summary>
public sealed class EnemyPatrolAwarenessController : MonoBehaviour
{
    [Header("Thresholds")]
    [SerializeField, Range(0f, 1f)]
    private float _lookOnlyThreshold = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float _suspiciousThreshold = 0.45f;

    [SerializeField, Range(0f, 1f)]
    private float _investigateThreshold = 0.72f;

    [Header("Timing")]
    [SerializeField, Min(0.1f)]
    private float _suspiciousLookDuration = 2.4f;

    [SerializeField, Min(0.1f)]
    private float _searchDuration = 4.2f;

    [SerializeField, Min(0.1f)]
    private float _searchLookInterval = 0.85f;

    [SerializeField, Min(0.1f)]
    private float _investigateArriveDistance = 1.2f;

    [Header("Search")]
    [SerializeField, Range(0f, 180f)]
    private float _searchScanArc = 145f;

    [SerializeField, Min(0.1f)]
    private float _searchSampleRadius = 2.8f;

    private EnemyLookController _lookController;
    private EnemySuspicionSensor _suspicionSensor;
    private NavMeshAgent _agent;
    private EnemyPatrolAwarenessState _state = EnemyPatrolAwarenessState.Patrol;
    private EnemySuspicionRecord _activeRecord;
    private Vector3 _investigatePosition;
    private Vector3 _returnDestination;
    private float _stateTimer;
    private float _nextSearchLookTime;
    private bool _hasReturnDestination;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;

    /// <summary>
    /// 当前巡逻感知状态。
    /// </summary>
    public EnemyPatrolAwarenessState State => _state;

    /// <summary>
    /// 敌人是否正在执行怀疑、调查或搜索行为。
    /// </summary>
    public bool IsInAwarenessState => _state != EnemyPatrolAwarenessState.Patrol;

    /// <summary>
    /// 当前调查或搜索的目标位置。
    /// </summary>
    public Vector3 InvestigatePosition => _investigatePosition;

    private void Awake()
    {
        _lookController = GetComponent<EnemyLookController>();
        _suspicionSensor = GetComponent<EnemySuspicionSensor>();
        _agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>
    /// 当前感知系统使用的视野节点。
    /// </summary>
    public Transform VisionTransform => _lookController != null ? _lookController.VisionTransform : transform;

    /// <summary>
    /// 设置巡逻感知预设；禁用巡逻感知时会立刻回到普通巡逻。
    /// </summary>
    /// <param name="awarenessPreset">感知预设。</param>
    public void ConfigurePreset(EnemyAwarenessPreset awarenessPreset)
    {
        _awarenessPreset = awarenessPreset;
        if (!_awarenessPreset.UsesPatrolAwareness())
        {
            ResetAwareness();
        }
    }

    /// <summary>
    /// 普通巡逻帧更新入口，只驱动视野扫描和新刺激消费。
    /// </summary>
    /// <param name="isWaiting">敌人是否正在巡逻点等待。</param>
    public void TickPassivePatrol(bool isWaiting)
    {
        TickPassivePatrol(isWaiting, null, -1f);
    }

    /// <summary>
    /// 普通巡逻帧更新入口，并传入当前路点的等待注视覆盖。
    /// </summary>
    /// <param name="isWaiting">敌人是否正在巡逻点等待。</param>
    /// <param name="waitLookTarget">等待时优先注视的目标。</param>
    /// <param name="waitScanArcOverride">等待扫描角度覆盖值。</param>
    public void TickPassivePatrol(bool isWaiting, Transform waitLookTarget, float waitScanArcOverride)
    {
        EnsureReferences();
        if (!_awarenessPreset.UsesPatrolAwareness())
        {
            _lookController.TickPatrolLook(isWaiting, waitLookTarget, waitScanArcOverride);
            return;
        }

        if (_state == EnemyPatrolAwarenessState.Patrol)
        {
            if (TryConsumeSuspicion())
            {
                return;
            }

            _lookController.TickPatrolLook(isWaiting, waitLookTarget, waitScanArcOverride);
        }
    }

    /// <summary>
    /// 驱动怀疑、调查或搜索状态，并在看见目标或回到巡逻时回调主控制器。
    /// </summary>
    /// <param name="canSeeTarget">主控制器提供的目标可见性判断。</param>
    /// <param name="onTargetSeen">重新看到目标时执行的回调。</param>
    /// <param name="onReturnToPatrol">感知流程结束并回巡逻时执行的回调。</param>
    /// <returns>当前帧由感知状态接管时返回 true。</returns>
    public bool TickAwareness(
        System.Func<bool> canSeeTarget,
        System.Action onTargetSeen,
        System.Action onReturnToPatrol)
    {
        EnsureReferences();
        if (!_awarenessPreset.UsesPatrolAwareness())
        {
            return false;
        }

        if (_state == EnemyPatrolAwarenessState.Patrol)
        {
            return false;
        }

        if (canSeeTarget != null && canSeeTarget())
        {
            ResetAwareness();
            onTargetSeen?.Invoke();
            return true;
        }

        if (TryConsumeSuspicion())
        {
            return true;
        }

        switch (_state)
        {
            case EnemyPatrolAwarenessState.Suspicious:
                TickSuspicious(onReturnToPatrol);
                break;
            case EnemyPatrolAwarenessState.Investigate:
                TickInvestigate(onReturnToPatrol);
                break;
            case EnemyPatrolAwarenessState.Search:
                TickSearch(onReturnToPatrol);
                break;
        }

        return true;
    }

    /// <summary>
    /// 重置巡逻感知状态，清理记录并恢复普通巡逻。
    /// </summary>
    public void ResetAwareness()
    {
        EnsureReferences();
        _state = EnemyPatrolAwarenessState.Patrol;
        _stateTimer = 0f;
        _hasReturnDestination = false;
        _suspicionSensor?.Clear();
        _lookController?.ClearExternalIntent();
    }

    /// <summary>
    /// 获取进入调查前的原始巡逻目的地。
    /// </summary>
    /// <param name="destination">成功时返回原本的巡逻目的地。</param>
    /// <returns>存在可返回目的地时返回 true。</returns>
    public bool TryGetReturnDestination(out Vector3 destination)
    {
        destination = _returnDestination;
        return _hasReturnDestination;
    }

    private bool TryConsumeSuspicion()
    {
        if (_suspicionSensor == null || !_suspicionSensor.TryConsumeRecord(out EnemySuspicionRecord record))
        {
            return false;
        }

        float strength = record.Strength;
        if (!_awarenessPreset.AllowsSuspicionRecord(record))
        {
            return false;
        }

        if (strength < _lookOnlyThreshold)
        {
            return false;
        }

        _activeRecord = record;
        _investigatePosition = record.EstimatedPosition;

        // 根据刺激强度决定只看一眼、原地警觉，还是移动到估算位置调查。
        if (strength >= _investigateThreshold)
        {
            EnterInvestigate(record);
        }
        else if (strength >= _suspiciousThreshold)
        {
            EnterSuspicious(record);
        }
        else
        {
            _lookController.LookAtPosition(record.EstimatedPosition, EnemyLookIntentSource.Suspicion, 1.1f);
        }

        return true;
    }

    private void EnterSuspicious(EnemySuspicionRecord record)
    {
        _state = EnemyPatrolAwarenessState.Suspicious;
        _stateTimer = 0f;
        _lookController.LookAtPosition(record.EstimatedPosition, EnemyLookIntentSource.Suspicion, _suspiciousLookDuration);
        StopAgent();
    }

    private void EnterInvestigate(EnemySuspicionRecord record)
    {
        _state = EnemyPatrolAwarenessState.Investigate;
        _stateTimer = 0f;
        _investigatePosition = record.EstimatedPosition;
        _lookController.LookAtPosition(_investigatePosition, EnemyLookIntentSource.Investigate, 1.3f);
        // 调查完成后尽量回到原巡逻目的地，避免固定路线被怀疑事件打断后丢失节奏。
        _hasReturnDestination = TryCaptureAgentDestination(out _returnDestination);

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_investigatePosition);
        }
    }

    private void EnterSearch()
    {
        _state = EnemyPatrolAwarenessState.Search;
        _stateTimer = 0f;
        _nextSearchLookTime = 0f;
        StopAgent();
    }

    private void TickSuspicious(System.Action onReturnToPatrol)
    {
        _stateTimer += Time.deltaTime;
        _lookController.LookAtPosition(_activeRecord.EstimatedPosition, EnemyLookIntentSource.Suspicion, 0.25f);
        _lookController.SnapBodyTowardsVision(180f);

        if (_stateTimer >= _suspiciousLookDuration)
        {
            ReturnToPatrol(onReturnToPatrol);
        }
    }

    private void TickInvestigate(System.Action onReturnToPatrol)
    {
        _stateTimer += Time.deltaTime;
        _lookController.LookAtPosition(_investigatePosition, EnemyLookIntentSource.Investigate, 0.35f);

        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
        {
            EnterSearch();
            return;
        }

        if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, _investigateArriveDistance))
        {
            EnterSearch();
        }
    }

    private void TickSearch(System.Action onReturnToPatrol)
    {
        _stateTimer += Time.deltaTime;
        if (Time.time >= _nextSearchLookTime)
        {
            // 搜索状态围绕调查点随机看向数个方向，模拟短时间搜寻而不移动。
            Vector3 direction = Quaternion.Euler(0f, Random.Range(-_searchScanArc * 0.5f, _searchScanArc * 0.5f), 0f) * transform.forward;
            Vector3 lookPosition = _investigatePosition + direction.normalized * Random.Range(1.5f, _searchSampleRadius);
            _lookController.LookAtPosition(lookPosition, EnemyLookIntentSource.Investigate, _searchLookInterval + 0.2f);
            _nextSearchLookTime = Time.time + _searchLookInterval;
        }

        _lookController.SnapBodyTowardsVision(150f);

        if (_stateTimer >= _searchDuration)
        {
            ReturnToPatrol(onReturnToPatrol);
        }
    }

    private void ReturnToPatrol(System.Action onReturnToPatrol)
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
        }

        ResetAwareness();
        onReturnToPatrol?.Invoke();
    }

    private void StopAgent()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
        }
    }

    private bool TryCaptureAgentDestination(out Vector3 destination)
    {
        destination = default;
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || !_agent.hasPath)
        {
            return false;
        }

        destination = _agent.destination;
        return true;
    }

    private void EnsureReferences()
    {
        if (_lookController == null)
        {
            _lookController = GetComponent<EnemyLookController>();
        }

        if (_suspicionSensor == null)
        {
            _suspicionSensor = GetComponent<EnemySuspicionSensor>();
        }

        if (_agent == null)
        {
            _agent = GetComponent<NavMeshAgent>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || _state == EnemyPatrolAwarenessState.Patrol)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.62f, 0.08f, 1f);
        Gizmos.DrawWireSphere(_investigatePosition, _investigateArriveDistance);
        Gizmos.DrawLine(transform.position + Vector3.up, _investigatePosition + Vector3.up);
    }
}
