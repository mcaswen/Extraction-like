using UnityEngine;

/// <summary>
/// 固定巡逻路线的起始路点选择方式。
/// </summary>
public enum EnemyPatrolRouteStartMode
{
    Closest,
    First,
    Random,
    SequenceOffset
}

/// <summary>
/// 固定巡逻路线跟随器。
/// 保存当前路点索引，并为敌人控制器提供下一个可导航目标点。
/// </summary>
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

    /// <summary>
    /// 当前使用的固定巡逻路线。
    /// </summary>
    public EnemyPatrolRoute Route => _route;

    /// <summary>
    /// 是否已绑定巡逻路线。
    /// </summary>
    public bool HasRoute => _route != null;

    /// <summary>
    /// 当前路点的等待时间覆盖值。
    /// </summary>
    public float CurrentWaitTimeOverride => _currentWaitTimeOverride;

    /// <summary>
    /// 当前路点等待时的注视目标。
    /// </summary>
    public Transform CurrentLookTarget => _currentLookTarget;

    /// <summary>
    /// 当前路点等待时的扫描角度覆盖值。
    /// </summary>
    public float CurrentWaitScanArcOverride => _currentWaitScanArcOverride;

    /// <summary>
    /// 运行时绑定新的固定巡逻路线并重置路线状态。
    /// </summary>
    /// <param name="route">巡逻路线。</param>
    /// <param name="startMode">起始路点选择方式。</param>
    /// <param name="sequenceOffset">顺序偏移起点。</param>
    /// <param name="navMeshSampleRadius">NavMesh 采样半径。</param>
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

    /// <summary>
    /// 判断当前路线是否至少有两个可导航路点。
    /// </summary>
    /// <returns>路线可用于固定巡逻时返回 true。</returns>
    public bool HasUsableRoute()
    {
        return _route != null && _route.CountSampledWaypoints(GetSampleRadius()) >= 2;
    }

    /// <summary>
    /// 根据起始模式选择初始巡逻目标。
    /// </summary>
    /// <param name="currentPosition">敌人当前位置。</param>
    /// <param name="destination">成功时返回初始目标位置。</param>
    /// <returns>成功选择初始目标时返回 true。</returns>
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

    /// <summary>
    /// 将当前目标设置为距离敌人最近的路线点。
    /// </summary>
    /// <param name="currentPosition">敌人当前位置。</param>
    /// <param name="destination">成功时返回最近目标位置。</param>
    /// <returns>成功找到最近路点时返回 true。</returns>
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

    /// <summary>
    /// 沿路线推进到下一个可导航目标点。
    /// </summary>
    /// <param name="currentPosition">敌人当前位置。</param>
    /// <param name="destination">成功时返回下一个目标位置。</param>
    /// <returns>成功推进到下一个路点时返回 true。</returns>
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

    /// <summary>
    /// 获取当前路点等待时间，没有覆盖值时返回默认等待时间。
    /// </summary>
    /// <param name="defaultWaitTime">敌人配置中的默认等待时间。</param>
    /// <returns>当前路点实际等待时间。</returns>
    public float GetCurrentWaitTime(float defaultWaitTime)
    {
        return _currentWaitTimeOverride >= 0f ? _currentWaitTimeOverride : defaultWaitTime;
    }

    /// <summary>
    /// 尝试获取当前路点等待时的注视目标。
    /// </summary>
    /// <param name="lookTarget">成功时返回注视目标。</param>
    /// <returns>当前路点配置了注视目标时返回 true。</returns>
    public bool TryGetCurrentLookTarget(out Transform lookTarget)
    {
        lookTarget = _currentLookTarget;
        return lookTarget != null;
    }

    /// <summary>
    /// 获取当前路点等待扫描角度，没有覆盖值时返回默认扫描角度。
    /// </summary>
    /// <param name="defaultScanArc">敌人默认等待扫描角度。</param>
    /// <returns>当前路点实际等待扫描角度。</returns>
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
            // PingPong 在两端反向，避免从末尾直接跳回起点造成敌人瞬移式转向。
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
