using UnityEngine;

public enum EnemyPatrolRouteStartMode
{
    Closest,
    First,
    Random,
    SequenceOffset
}

public sealed class EnemyPatrolRouteFollower : MonoBehaviour
{
    [SerializeField]
    private EnemyPatrolRoute _route;

    [SerializeField]
    private EnemyPatrolRouteStartMode _startMode = EnemyPatrolRouteStartMode.Closest;

    [SerializeField, Min(0)]
    private int _sequenceOffset;

    [SerializeField, Min(0.1f)]
    private float _navMeshSampleRadius = 2f;

    private int _currentRouteIndex = -1;
    private int _direction = 1;
    private float _currentWaitTimeOverride = -1f;
    private Transform _currentLookTarget;
    private float _currentWaitScanArcOverride = -1f;
    private bool _hasInitialized;
    private bool _hasWarnedInvalidRoute;

    public EnemyPatrolRoute Route => _route;
    public bool HasRoute => _route != null;
    public float CurrentWaitTimeOverride => _currentWaitTimeOverride;
    public Transform CurrentLookTarget => _currentLookTarget;
    public float CurrentWaitScanArcOverride => _currentWaitScanArcOverride;

    public void AssignRoute(
        EnemyPatrolRoute route,
        EnemyPatrolRouteStartMode startMode,
        int sequenceOffset,
        float navMeshSampleRadius)
    {
        _route = route;
        _startMode = startMode;
        _sequenceOffset = Mathf.Max(0, sequenceOffset);
        _navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        _currentRouteIndex = -1;
        _direction = 1;
        _currentWaitTimeOverride = -1f;
        _currentLookTarget = null;
        _currentWaitScanArcOverride = -1f;
        _hasInitialized = false;
        _hasWarnedInvalidRoute = false;
    }

    public bool HasUsableRoute()
    {
        return _route != null && _route.CountSampledWaypoints(GetSampleRadius()) >= 2;
    }

    public bool TrySetInitialDestination(Vector3 currentPosition, out Vector3 destination)
    {
        destination = default;
        if (!TryChooseStartPoint(currentPosition, out EnemyPatrolRoutePoint routePoint))
        {
            WarnInvalidRouteOnce();
            return false;
        }

        ApplyRoutePoint(routePoint);
        destination = routePoint.Position;
        _hasInitialized = true;
        return true;
    }

    public bool TrySetNearestDestination(Vector3 currentPosition, out Vector3 destination)
    {
        destination = default;
        if (_route == null || !_route.TryFindNearestSampledWaypoint(currentPosition, GetSampleRadius(), out EnemyPatrolRoutePoint routePoint))
        {
            WarnInvalidRouteOnce();
            return false;
        }

        if (!HasUsableRoute())
        {
            WarnInvalidRouteOnce();
            return false;
        }

        ApplyRoutePoint(routePoint);
        destination = routePoint.Position;
        _hasInitialized = true;
        return true;
    }

    public bool TryAdvanceToNextDestination(Vector3 currentPosition, out Vector3 destination)
    {
        destination = default;
        if (!_hasInitialized || _currentRouteIndex < 0)
        {
            return TrySetInitialDestination(currentPosition, out destination);
        }

        if (_route == null || !HasUsableRoute())
        {
            WarnInvalidRouteOnce();
            return false;
        }

        int waypointCount = _route.WaypointCount;
        if (waypointCount <= 0)
        {
            WarnInvalidRouteOnce();
            return false;
        }

        int candidateIndex = _currentRouteIndex;
        for (int attempts = 0; attempts < waypointCount; attempts++)
        {
            candidateIndex = GetNextIndex(candidateIndex, waypointCount);
            if (_route.TryGetSampledWaypoint(candidateIndex, GetSampleRadius(), out EnemyPatrolRoutePoint routePoint))
            {
                ApplyRoutePoint(routePoint);
                destination = routePoint.Position;
                return true;
            }
        }

        WarnInvalidRouteOnce();
        return false;
    }

    public float GetCurrentWaitTime(float defaultWaitTime)
    {
        return _currentWaitTimeOverride >= 0f ? _currentWaitTimeOverride : defaultWaitTime;
    }

    public bool TryGetCurrentLookTarget(out Transform lookTarget)
    {
        lookTarget = _currentLookTarget;
        return lookTarget != null;
    }

    public float GetCurrentWaitScanArc(float defaultScanArc)
    {
        return _currentWaitScanArcOverride >= 0f ? _currentWaitScanArcOverride : defaultScanArc;
    }

    private bool TryChooseStartPoint(Vector3 currentPosition, out EnemyPatrolRoutePoint routePoint)
    {
        routePoint = default;
        if (_route == null || !HasUsableRoute())
        {
            return false;
        }

        switch (_startMode)
        {
            case EnemyPatrolRouteStartMode.First:
                return TryFindFromIndex(0, out routePoint);
            case EnemyPatrolRouteStartMode.Random:
                return TryFindFromIndex(Random.Range(0, Mathf.Max(1, _route.WaypointCount)), out routePoint);
            case EnemyPatrolRouteStartMode.SequenceOffset:
                return TryFindFromIndex(_sequenceOffset, out routePoint);
            default:
                return _route.TryFindNearestSampledWaypoint(currentPosition, GetSampleRadius(), out routePoint);
        }
    }

    private bool TryFindFromIndex(int startIndex, out EnemyPatrolRoutePoint routePoint)
    {
        routePoint = default;
        if (_route == null || _route.WaypointCount <= 0)
        {
            return false;
        }

        int waypointCount = _route.WaypointCount;
        int index = Mod(startIndex, waypointCount);
        for (int attempts = 0; attempts < waypointCount; attempts++)
        {
            int candidateIndex = Mod(index + attempts, waypointCount);
            if (_route.TryGetSampledWaypoint(candidateIndex, GetSampleRadius(), out routePoint))
            {
                return true;
            }
        }

        return false;
    }

    private int GetNextIndex(int currentIndex, int waypointCount)
    {
        if (_route != null && _route.RouteMode == EnemyPatrolRouteMode.PingPong)
        {
            if (waypointCount <= 1)
            {
                return currentIndex;
            }

            int nextIndex = currentIndex + _direction;
            if (nextIndex >= waypointCount)
            {
                _direction = -1;
                nextIndex = waypointCount - 2;
            }
            else if (nextIndex < 0)
            {
                _direction = 1;
                nextIndex = 1;
            }

            return Mathf.Clamp(nextIndex, 0, waypointCount - 1);
        }

        return Mod(currentIndex + 1, waypointCount);
    }

    private void ApplyRoutePoint(EnemyPatrolRoutePoint routePoint)
    {
        _currentRouteIndex = routePoint.RouteIndex;
        _currentWaitTimeOverride = routePoint.WaitTimeOverride;
        _currentLookTarget = routePoint.LookTarget;
        _currentWaitScanArcOverride = routePoint.WaitScanArcOverride;
    }

    private float GetSampleRadius()
    {
        if (_navMeshSampleRadius > 0f)
        {
            return _navMeshSampleRadius;
        }

        return _route != null ? _route.DefaultNavMeshSampleRadius : 2f;
    }

    private void WarnInvalidRouteOnce()
    {
        if (_hasWarnedInvalidRoute)
        {
            return;
        }

        _hasWarnedInvalidRoute = true;
        string routeName = _route != null ? _route.name : "None";
        Debug.LogWarning($"[{name}] Fixed patrol route '{routeName}' is invalid. Falling back to random-radius patrol.", this);
    }

    private static int Mod(int value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
