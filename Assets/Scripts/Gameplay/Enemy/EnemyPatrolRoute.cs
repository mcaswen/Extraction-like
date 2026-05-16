using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyPatrolRouteMode
{
    Loop,
    PingPong
}

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

    public Transform Point => _point;
    public float WaitTimeOverride => _waitTimeOverride;
    public Transform LookTarget => _lookTarget;
    public float WaitScanArcOverride => _waitScanArcOverride;

    public EnemyPatrolWaypoint()
    {
    }

    public EnemyPatrolWaypoint(Transform point)
    {
        _point = point;
    }
}

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

    public EnemyPatrolRouteMode RouteMode => _routeMode;
    public int WaypointCount => _waypoints != null ? _waypoints.Count : 0;
    public float DefaultNavMeshSampleRadius => _defaultNavMeshSampleRadius;

    public IReadOnlyList<EnemyPatrolWaypoint> Waypoints => _waypoints;

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

    public void ClearMissingWaypoints()
    {
        if (_waypoints == null)
        {
            return;
        }

        _waypoints.RemoveAll(waypoint => waypoint == null || waypoint.Point == null);
    }

    public void ReverseWaypoints()
    {
        _waypoints?.Reverse();
    }

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

public readonly struct EnemyPatrolRoutePoint
{
    public readonly int RouteIndex;
    public readonly Vector3 Position;
    public readonly float WaitTimeOverride;
    public readonly Transform LookTarget;
    public readonly float WaitScanArcOverride;

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
