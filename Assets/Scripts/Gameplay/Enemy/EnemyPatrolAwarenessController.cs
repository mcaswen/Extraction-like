using UnityEngine;
using UnityEngine.AI;

public enum EnemyPatrolAwarenessState
{
    Patrol,
    Suspicious,
    Investigate,
    Search
}

[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
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

    public EnemyPatrolAwarenessState State => _state;
    public bool IsInAwarenessState => _state != EnemyPatrolAwarenessState.Patrol;
    public Vector3 InvestigatePosition => _investigatePosition;

    private void Awake()
    {
        _lookController = GetComponent<EnemyLookController>();
        _suspicionSensor = GetComponent<EnemySuspicionSensor>();
        _agent = GetComponent<NavMeshAgent>();
    }

    public Transform VisionTransform => _lookController != null ? _lookController.VisionTransform : transform;

    public void ConfigurePreset(EnemyAwarenessPreset awarenessPreset)
    {
        _awarenessPreset = awarenessPreset;
        if (!_awarenessPreset.UsesPatrolAwareness())
        {
            ResetAwareness();
        }
    }

    public void TickPassivePatrol(bool isWaiting)
    {
        TickPassivePatrol(isWaiting, null, -1f);
    }

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

    public void ResetAwareness()
    {
        EnsureReferences();
        _state = EnemyPatrolAwarenessState.Patrol;
        _stateTimer = 0f;
        _hasReturnDestination = false;
        _suspicionSensor?.Clear();
        _lookController?.ClearExternalIntent();
    }

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
