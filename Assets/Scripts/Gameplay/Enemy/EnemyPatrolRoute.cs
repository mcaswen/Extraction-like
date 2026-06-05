using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 固定巡逻路线的循环方式。
/// </summary>
public enum EnemyPatrolRouteMode
{
    Loop,
    PingPong
}

/// <summary>
/// 固定巡逻路线上的单个路点配置。
/// </summary>
[Serializable]
public sealed class EnemyPatrolWaypoint
{
    [SerializeField]
    private Transform _point;

    [SerializeField, Tooltip("Use a negative value to fall back to the enemy config wait time.")]
    private float _waitTimeOverride = -1f;

    [SerializeField, Tooltip("Optional point this enemy should favor looking at while waiting here.")]
    private Transform _lookTarget;

    [SerializeField, Tooltip("Use a negative value to fall back to the enemy's default wait scan arc.")]
    private float _waitScanArcOverride = -1f;

    /// <summary>
    /// 路点对应的场景 Transform。
    /// </summary>
    public Transform Point => _point;

    /// <summary>
    /// 到达该路点后的等待时间覆盖值，小于 0 时使用敌人默认值。
    /// </summary>
    public float WaitTimeOverride => _waitTimeOverride;

    /// <summary>
    /// 敌人在该路点等待时优先注视的目标。
    /// </summary>
    public Transform LookTarget => _lookTarget;

    /// <summary>
    /// 敌人在该路点等待时的扫描角度覆盖值，小于 0 时使用默认值。
    /// </summary>
    public float WaitScanArcOverride => _waitScanArcOverride;

    /// <summary>
    /// 创建一个空路点配置，供 Unity 序列化使用。
    /// </summary>
    public EnemyPatrolWaypoint()
    {
    }

    /// <summary>
    /// 使用指定 Transform 创建一个路点配置。
    /// </summary>
    /// <param name="point">路点 Transform。</param>
    public EnemyPatrolWaypoint(Transform point)
    {
        _point = point;
    }
}

/// <summary>
/// 敌人的固定巡逻路线组件。
/// 管理路点列表、NavMesh 采样和 Scene 视图路线绘制。
/// </summary>
public sealed class EnemyPatrolRoute : MonoBehaviour
{
    [SerializeField]
    private EnemyPatrolRouteMode _routeMode = EnemyPatrolRouteMode.Loop;

    [SerializeField]
    private List<EnemyPatrolWaypoint> _waypoints = new List<EnemyPatrolWaypoint>();

    [Header("NavMesh")]
    [SerializeField, Min(0.1f)]
    private float _defaultNavMeshSampleRadius = 2f;

    [Header("Gizmos")]
    [SerializeField]
    private bool _drawGizmos = true;

    [SerializeField]
    private Color _gizmoColor = new Color(0.25f, 0.75f, 1f, 1f);

    /// <summary>
    /// 路线的循环方式。
    /// </summary>
    public EnemyPatrolRouteMode RouteMode => _routeMode;

    /// <summary>
    /// 路线中序列化的路点数量。
    /// </summary>
    public int WaypointCount => _waypoints != null ? _waypoints.Count : 0;

    /// <summary>
    /// 默认 NavMesh 采样半径。
    /// </summary>
    public float DefaultNavMeshSampleRadius => _defaultNavMeshSampleRadius;

    /// <summary>
    /// 只读访问当前路点列表。
    /// </summary>
    public IReadOnlyList<EnemyPatrolWaypoint> Waypoints => _waypoints;

    /// <summary>
    /// 将当前对象的直接子节点收集为巡逻路点。
    /// </summary>
    public void CollectChildWaypoints()
    {
        _waypoints ??= new List<EnemyPatrolWaypoint>();
        _waypoints.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                _waypoints.Add(new EnemyPatrolWaypoint(child));
            }
        }
    }

    /// <summary>
    /// 移除缺失 Transform 的路点引用。
    /// </summary>
    public void ClearMissingWaypoints()
    {
        if (_waypoints == null)
        {
            return;
        }

        _waypoints.RemoveAll(waypoint => waypoint == null || waypoint.Point == null);
    }

    /// <summary>
    /// 反转当前巡逻路点顺序。
    /// </summary>
    public void ReverseWaypoints()
    {
        _waypoints?.Reverse();
    }

    /// <summary>
    /// 统计已分配有效 Transform 的路点数量。
    /// </summary>
    /// <returns>有效路点数量。</returns>
    public int CountAssignedWaypoints()
    {
        if (_waypoints == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            if (_waypoints[i] != null && _waypoints[i].Point != null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 获取指定路线索引对应的 NavMesh 采样路点。
    /// </summary>
    /// <param name="routeIndex">路线索引。</param>
    /// <param name="sampleRadius">NavMesh 采样半径。</param>
    /// <param name="routePoint">成功时返回可导航路点。</param>
    /// <returns>成功采样到 NavMesh 位置时返回 true。</returns>
    public bool TryGetSampledWaypoint(
        int routeIndex,
        float sampleRadius,
        out EnemyPatrolRoutePoint routePoint)
    {
        routePoint = default;
        if (_waypoints == null || routeIndex < 0 || routeIndex >= _waypoints.Count)
        {
            return false;
        }

        EnemyPatrolWaypoint waypoint = _waypoints[routeIndex];
        if (waypoint == null || waypoint.Point == null)
        {
            return false;
        }

        float effectiveRadius = Mathf.Max(0.1f, sampleRadius);
        if (!NavMesh.SamplePosition(waypoint.Point.position, out NavMeshHit hit, effectiveRadius, NavMesh.AllAreas))
        {
            return false;
        }

        routePoint = new EnemyPatrolRoutePoint(
            routeIndex,
            hit.position,
            waypoint.WaitTimeOverride,
            waypoint.LookTarget,
            waypoint.WaitScanArcOverride);
        return true;
    }

    /// <summary>
    /// 从路线中查找距离指定位置最近的可导航路点。
    /// </summary>
    /// <param name="position">用于比较距离的位置。</param>
    /// <param name="sampleRadius">NavMesh 采样半径。</param>
    /// <param name="routePoint">成功时返回最近路点。</param>
    /// <returns>找到可导航路点时返回 true。</returns>
    public bool TryFindNearestSampledWaypoint(
        Vector3 position,
        float sampleRadius,
        out EnemyPatrolRoutePoint routePoint)
    {
        routePoint = default;
        if (_waypoints == null || _waypoints.Count == 0)
        {
            return false;
        }

        bool found = false;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            if (!TryGetSampledWaypoint(i, sampleRadius, out EnemyPatrolRoutePoint candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.Position - position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                routePoint = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 统计可以成功采样到 NavMesh 的路点数量。
    /// </summary>
    /// <param name="sampleRadius">NavMesh 采样半径。</param>
    /// <returns>可导航路点数量。</returns>
    public int CountSampledWaypoints(float sampleRadius)
    {
        if (_waypoints == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            if (TryGetSampledWaypoint(i, sampleRadius, out _))
            {
                count++;
            }
        }

        return count;
    }

    private void OnValidate()
    {
        _waypoints ??= new List<EnemyPatrolWaypoint>();
        _defaultNavMeshSampleRadius = Mathf.Max(0.1f, _defaultNavMeshSampleRadius);
    }

    private void OnDrawGizmos()
    {
        if (_drawGizmos)
        {
            DrawRouteGizmos(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawRouteGizmos(true);
    }

    private void DrawRouteGizmos(bool selected)
    {
        if (_waypoints == null || _waypoints.Count == 0)
        {
            return;
        }

        Color color = _gizmoColor;
        color.a = selected ? 1f : Mathf.Min(color.a, 0.45f);
        Gizmos.color = color;

        // 绘制路点连线时跳过缺失路点，让设计器能在 Scene 中直接看出有效路线。
        Vector3? previous = null;
        Vector3? first = null;
        Vector3? last = null;

        for (int i = 0; i < _waypoints.Count; i++)
        {
            EnemyPatrolWaypoint waypoint = _waypoints[i];
            if (waypoint == null || waypoint.Point == null)
            {
                continue;
            }

            Vector3 position = waypoint.Point.position;
            Gizmos.DrawSphere(position, selected ? 0.24f : 0.16f);

            if (previous.HasValue)
            {
                Gizmos.DrawLine(previous.Value, position);
                DrawDirectionMarker(previous.Value, position);
            }

            first ??= position;
            previous = position;
            last = position;
        }

        if (_routeMode == EnemyPatrolRouteMode.Loop && first.HasValue && last.HasValue && first.Value != last.Value)
        {
            Gizmos.DrawLine(last.Value, first.Value);
            DrawDirectionMarker(last.Value, first.Value);
        }
    }

    private static void DrawDirectionMarker(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 center = Vector3.Lerp(from, to, 0.6f);
        Vector3 direction = delta.normalized;
        Vector3 right = Quaternion.Euler(0f, 35f, 0f) * -direction;
        Vector3 left = Quaternion.Euler(0f, -35f, 0f) * -direction;
        float size = Mathf.Min(0.45f, delta.magnitude * 0.18f);

        Gizmos.DrawLine(center, center + right * size);
        Gizmos.DrawLine(center, center + left * size);
    }
}

/// <summary>
/// 固定巡逻路线在运行时解析出的可导航路点数据。
/// </summary>
public readonly struct EnemyPatrolRoutePoint
{
    /// <summary>
    /// 路点在原始路线数组中的索引。
    /// </summary>
    public readonly int RouteIndex;

    /// <summary>
    /// NavMesh 采样后的巡逻目标位置。
    /// </summary>
    public readonly Vector3 Position;

    /// <summary>
    /// 等待时间覆盖值。
    /// </summary>
    public readonly float WaitTimeOverride;

    /// <summary>
    /// 等待时优先注视的目标。
    /// </summary>
    public readonly Transform LookTarget;

    /// <summary>
    /// 等待扫描角度覆盖值。
    /// </summary>
    public readonly float WaitScanArcOverride;

    /// <summary>
    /// 创建一个运行时巡逻路点。
    /// </summary>
    /// <param name="routeIndex">原始路线索引。</param>
    /// <param name="position">可导航目标位置。</param>
    /// <param name="waitTimeOverride">等待时间覆盖值。</param>
    /// <param name="lookTarget">等待时注视目标。</param>
    /// <param name="waitScanArcOverride">等待扫描角度覆盖值。</param>
    public EnemyPatrolRoutePoint(
        int routeIndex,
        Vector3 position,
        float waitTimeOverride,
        Transform lookTarget,
        float waitScanArcOverride)
    {
        RouteIndex = routeIndex;
        Position = position;
        WaitTimeOverride = waitTimeOverride;
        LookTarget = lookTarget;
        WaitScanArcOverride = waitScanArcOverride;
    }
}
