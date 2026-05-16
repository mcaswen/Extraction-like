using UnityEngine;
using UnityEngine.AI;

public enum EnemyLookIntentSource
{
    PathForward,
    MovingScan,
    WaitScan,
    Suspicion,
    Investigate,
    Combat
}

public sealed class EnemyLookController : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField]
    private Transform _visionPivot;

    [SerializeField, Min(0f)]
    private float _pivotHeight = 1.15f;

    [Header("Movement Scan")]
    [SerializeField, Range(0f, 120f)]
    private float _movingScanArc = 52f;

    [SerializeField, Min(0.1f)]
    private float _movingScanIntervalMin = 0.75f;

    [SerializeField, Min(0.1f)]
    private float _movingScanIntervalMax = 1.7f;

    [SerializeField, Range(0f, 1f)]
    private float _movingForwardBias = 0.62f;

    [Header("Wait Scan")]
    [SerializeField, Range(0f, 180f)]
    private float _waitScanArc = 115f;

    [SerializeField, Min(0.1f)]
    private float _waitScanIntervalMin = 0.45f;

    [SerializeField, Min(0.1f)]
    private float _waitScanIntervalMax = 1.15f;

    [Header("Turning")]
    [SerializeField, Min(1f)]
    private float _patrolTurnSpeed = 185f;

    [SerializeField, Min(1f)]
    private float _alertTurnSpeed = 310f;

    [SerializeField, Min(1f)]
    private float _combatTurnSpeed = 540f;

    [SerializeField, Min(0.05f)]
    private float _externalIntentDuration = 0.8f;

    private NavMeshAgent _agent;
    private Vector3 _currentLookDirection;
    private Vector3 _scanDirection;
    private Vector3 _externalLookPosition;
    private EnemyLookIntentSource _externalIntentSource;
    private float _nextScanTime;
    private float _externalIntentExpireTime;
    private float _externalTurnSpeed;
    private bool _hasExternalIntent;

    public Transform VisionTransform => _visionPivot != null ? _visionPivot : transform;
    public Vector3 CurrentPlanarDirection => _currentLookDirection.sqrMagnitude > 0.0001f ? _currentLookDirection.normalized : transform.forward;
    public Vector3 CurrentExternalLookPosition => _externalLookPosition;
    public bool HasExternalIntent => _hasExternalIntent && Time.time <= _externalIntentExpireTime;

    private void Awake()
    {
        EnsureVisionPivot();
        _agent = GetComponent<NavMeshAgent>();
        _currentLookDirection = GetPlanarDirection(transform.forward, Vector3.forward);
        _scanDirection = _currentLookDirection;
        _nextScanTime = Time.time;
    }

    private void LateUpdate()
    {
        EnsureVisionPivot();
    }

    public void TickPatrolLook(bool isWaiting)
    {
        TickPatrolLook(isWaiting, null, -1f);
    }

    public void TickPatrolLook(bool isWaiting, Transform waitLookTarget, float waitScanArcOverride)
    {
        EnsureVisionPivot();

        if (TryTickExternalIntent())
        {
            return;
        }

        if (isWaiting && waitLookTarget != null)
        {
            TurnVisionTowardsPosition(waitLookTarget.position, _patrolTurnSpeed);
            return;
        }

        Vector3 baseDirection = ResolvePathDirection();
        float scanArc = isWaiting
            ? (waitScanArcOverride >= 0f ? waitScanArcOverride : _waitScanArc)
            : _movingScanArc;
        float intervalMin = isWaiting ? _waitScanIntervalMin : _movingScanIntervalMin;
        float intervalMax = isWaiting ? _waitScanIntervalMax : _movingScanIntervalMax;

        if (Time.time >= _nextScanTime || _scanDirection.sqrMagnitude <= 0.0001f)
        {
            _scanDirection = ChooseScanDirection(baseDirection, scanArc, isWaiting);
            _nextScanTime = Time.time + Random.Range(intervalMin, Mathf.Max(intervalMin, intervalMax));
        }

        float weight = isWaiting ? 1f : 1f - _movingForwardBias;
        Vector3 desiredDirection = Vector3.Slerp(baseDirection, _scanDirection, weight);
        TurnVisionTowardsDirection(desiredDirection, _patrolTurnSpeed);
    }

    public void LookAtPosition(
        Vector3 worldPosition,
        EnemyLookIntentSource source = EnemyLookIntentSource.Suspicion,
        float duration = -1f,
        float turnSpeed = -1f)
    {
        _externalLookPosition = worldPosition;
        _externalIntentSource = source;
        _externalIntentExpireTime = Time.time + (duration > 0f ? duration : _externalIntentDuration);
        _externalTurnSpeed = turnSpeed > 0f ? turnSpeed : ResolveTurnSpeed(source);
        _hasExternalIntent = true;
    }

    public void LookAtPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return;
        }

        LookAtPosition(playerTransform.position, EnemyLookIntentSource.Combat, 0.25f, _combatTurnSpeed);
    }

    public void ClearExternalIntent()
    {
        _hasExternalIntent = false;
    }

    public void SnapBodyTowardsVision(float turnSpeed)
    {
        Vector3 direction = CurrentPlanarDirection;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private bool TryTickExternalIntent()
    {
        if (!_hasExternalIntent)
        {
            return false;
        }

        if (Time.time > _externalIntentExpireTime)
        {
            _hasExternalIntent = false;
            return false;
        }

        TurnVisionTowardsPosition(_externalLookPosition, _externalTurnSpeed);
        return true;
    }

    private Vector3 ResolvePathDirection()
    {
        if (_agent == null)
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        if (_agent != null && _agent.enabled)
        {
            Vector3 steeringDelta = _agent.steeringTarget - transform.position;
            steeringDelta.y = 0f;
            if (steeringDelta.sqrMagnitude > 0.09f)
            {
                return steeringDelta.normalized;
            }

            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.04f)
            {
                return velocity.normalized;
            }
        }

        return GetPlanarDirection(transform.forward, Vector3.forward);
    }

    private Vector3 ChooseScanDirection(Vector3 baseDirection, float scanArc, bool isWaiting)
    {
        float halfArc = Mathf.Max(0f, scanArc) * 0.5f;
        float yaw;
        if (isWaiting)
        {
            int slot = Random.Range(0, 5);
            switch (slot)
            {
                case 0:
                    yaw = -halfArc;
                    break;
                case 1:
                    yaw = -halfArc * 0.5f;
                    break;
                case 2:
                    yaw = 0f;
                    break;
                case 3:
                    yaw = halfArc * 0.5f;
                    break;
                default:
                    yaw = halfArc;
                    break;
            }
            yaw += Random.Range(-8f, 8f);
        }
        else
        {
            yaw = Random.Range(-halfArc, halfArc);
        }

        return Quaternion.Euler(0f, yaw, 0f) * baseDirection;
    }

    private void TurnVisionTowardsPosition(Vector3 worldPosition, float turnSpeed)
    {
        Vector3 direction = worldPosition - VisionTransform.position;
        direction.y = 0f;
        TurnVisionTowardsDirection(direction, turnSpeed);
    }

    private void TurnVisionTowardsDirection(Vector3 direction, float turnSpeed)
    {
        direction = GetPlanarDirection(direction, _currentLookDirection);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        VisionTransform.rotation = Quaternion.RotateTowards(
            VisionTransform.rotation,
            targetRotation,
            Mathf.Max(1f, turnSpeed) * Time.deltaTime);
        _currentLookDirection = GetPlanarDirection(VisionTransform.forward, direction);
    }

    private float ResolveTurnSpeed(EnemyLookIntentSource source)
    {
        switch (source)
        {
            case EnemyLookIntentSource.Combat:
                return _combatTurnSpeed;
            case EnemyLookIntentSource.Suspicion:
            case EnemyLookIntentSource.Investigate:
                return _alertTurnSpeed;
            default:
                return _patrolTurnSpeed;
        }
    }

    private void EnsureVisionPivot()
    {
        if (_visionPivot != null)
        {
            return;
        }

        Transform existingPivot = transform.Find("VisionPivot");
        if (existingPivot != null)
        {
            _visionPivot = existingPivot;
            return;
        }

        GameObject pivotObject = new GameObject("VisionPivot");
        pivotObject.transform.SetParent(transform, false);
        pivotObject.transform.localPosition = Vector3.up * _pivotHeight;
        pivotObject.transform.localRotation = Quaternion.identity;
        _visionPivot = pivotObject.transform;
    }

    private static Vector3 GetPlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = _visionPivot != null ? _visionPivot : transform;
        Gizmos.color = new Color(0.22f, 0.84f, 1f, 1f);
        Gizmos.DrawLine(pivot.position, pivot.position + pivot.forward * 2.2f);

        if (HasExternalIntent)
        {
            Gizmos.color = new Color(1f, 0.72f, 0.08f, 1f);
            Gizmos.DrawLine(pivot.position, _externalLookPosition + Vector3.up * _pivotHeight);
            Gizmos.DrawWireSphere(_externalLookPosition, 0.35f);
        }
    }
}
