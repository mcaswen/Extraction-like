using System;
using System.Collections.Generic;
using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField]
    private bool _spawnOnStart = true;

    [SerializeField]
    private Transform _spawnParent;

    [SerializeField, Min(0f)]
    private float _spawnRadius = 0f;

    [SerializeField, Min(0.1f)]
    private float _spawnNavMeshSampleRadius = 3f;

    [SerializeField]
    private bool _randomizeYaw = true;

    [SerializeField]
    private List<EnemySpawnEntry> _entries = new List<EnemySpawnEntry>();

    [Header("Gizmos")]
    [SerializeField]
    private bool _drawGizmos = true;

    [SerializeField]
    private Color _gizmoColor = new Color(1f, 0.55f, 0.2f, 1f);

    public IReadOnlyList<EnemySpawnEntry> Entries => _entries;

    private void Start()
    {
        if (_spawnOnStart)
        {
            SpawnAll();
        }
    }

    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{name}] Spawn All only runs in Play Mode.", this);
            return;
        }

        if (_entries == null)
        {
            return;
        }

        int sequence = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            EnemySpawnEntry entry = _entries[i];
            if (entry == null || entry.EnemyPrefab == null)
            {
                continue;
            }

            int count = Mathf.Max(0, entry.SpawnCount);
            for (int j = 0; j < count; j++)
            {
                Spawn(entry, sequence);
                sequence++;
            }
        }
    }

    public GameObject Spawn(EnemySpawnEntry entry, int sequenceIndex)
    {
        if (entry == null || entry.EnemyPrefab == null)
        {
            return null;
        }

        Vector3 position = ResolveSpawnPosition(entry, sequenceIndex);
        Quaternion rotation = ResolveSpawnRotation(entry);
        Transform parent = _spawnParent != null ? _spawnParent : null;
        GameObject enemy = Instantiate(entry.EnemyPrefab, position, rotation, parent);
        enemy.name = entry.BuildSpawnedName(sequenceIndex);

        EnemyPatrolRouteFollower follower = enemy.GetComponent<EnemyPatrolRouteFollower>();
        if (follower == null)
        {
            follower = enemy.AddComponent<EnemyPatrolRouteFollower>();
        }

        follower.AssignRoute(
            entry.PatrolRoute,
            entry.RouteStartMode,
            sequenceIndex,
            entry.NavMeshSampleRadius > 0f ? entry.NavMeshSampleRadius : _spawnNavMeshSampleRadius);

        RegisterSpawnedEnemyTarget(enemy);
        return enemy;
    }

    public void CollectChildRoutes()
    {
        EnemyPatrolRoute[] routes = GetComponentsInChildren<EnemyPatrolRoute>(true);
        if (routes == null || routes.Length == 0)
        {
            return;
        }

        _entries ??= new List<EnemySpawnEntry>();
        for (int i = 0; i < _entries.Count; i++)
        {
            EnemySpawnEntry entry = _entries[i];
            if (entry != null && entry.PatrolRoute == null)
            {
                entry.SetPatrolRoute(routes[Mathf.Min(i, routes.Length - 1)]);
            }
        }
    }

    private Vector3 ResolveSpawnPosition(EnemySpawnEntry entry, int sequenceIndex)
    {
        Vector3 localOffset = entry.PositionOffset;
        if (_spawnRadius > 0f || entry.SpawnRadius > 0f)
        {
            float radius = entry.SpawnRadius > 0f ? entry.SpawnRadius : _spawnRadius;
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
            localOffset += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        Vector3 desired = transform.TransformPoint(localOffset);
        float sampleRadius = entry.NavMeshSampleRadius > 0f ? entry.NavMeshSampleRadius : _spawnNavMeshSampleRadius;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, Mathf.Max(0.1f, sampleRadius), NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning($"[{name}] Could not sample spawn position for '{entry.Label}' on NavMesh. Using raw spawn position.", this);
        return desired;
    }

    private Quaternion ResolveSpawnRotation(EnemySpawnEntry entry)
    {
        Quaternion baseRotation = transform.rotation * Quaternion.Euler(entry.RotationOffset);
        bool shouldRandomizeYaw = entry.OverrideRandomizeYaw ? entry.RandomizeYaw : _randomizeYaw;
        if (!shouldRandomizeYaw)
        {
            return baseRotation;
        }

        return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * baseRotation;
    }

    // 出生点只负责声明来源，生成后的活跃敌人归属由目标注册表转交给对应活跃群
    private void RegisterSpawnedEnemyTarget(GameObject enemy)
    {
        if (enemy == null)
            return;

        EnemyHealthController enemyHealth = enemy.GetComponent<EnemyHealthController>();
        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInChildren<EnemyHealthController>(true);

        if (enemyHealth != null)
            GameplayTargetRegistry.GetOrCreate().TryRegisterSpawnedEnemy(transform, enemyHealth);
    }

    private void OnValidate()
    {
        _entries ??= new List<EnemySpawnEntry>();
        _spawnRadius = Mathf.Max(0f, _spawnRadius);
        _spawnNavMeshSampleRadius = Mathf.Max(0.1f, _spawnNavMeshSampleRadius);
    }

    private void OnDrawGizmos()
    {
        if (_drawGizmos)
        {
            DrawGizmos(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos(true);
    }

    private void DrawGizmos(bool selected)
    {
        Color color = _gizmoColor;
        color.a = selected ? 1f : 0.45f;
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, selected ? 0.32f : 0.22f);

        if (_spawnRadius > 0f)
        {
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }

        Vector3 forward = transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + forward * 1.25f);
    }
}

[Serializable]
public sealed class EnemySpawnEntry
{
    [SerializeField]
    private string _label = "Enemy";

    [SerializeField]
    private GameObject _enemyPrefab;

    [SerializeField, Min(0)]
    private int _spawnCount = 1;

    [SerializeField]
    private EnemyPatrolRoute _patrolRoute;

    [SerializeField]
    private EnemyPatrolRouteStartMode _routeStartMode = EnemyPatrolRouteStartMode.Closest;

    [SerializeField]
    private Vector3 _positionOffset;

    [SerializeField]
    private Vector3 _rotationOffset;

    [SerializeField, Min(0f)]
    private float _spawnRadius;

    [SerializeField, Min(0f)]
    private float _navMeshSampleRadius = 2f;

    [SerializeField]
    private bool _overrideRandomizeYaw;

    [SerializeField]
    private bool _randomizeYaw = true;

    public string Label => _label;
    public GameObject EnemyPrefab => _enemyPrefab;
    public int SpawnCount => _spawnCount;
    public EnemyPatrolRoute PatrolRoute => _patrolRoute;
    public EnemyPatrolRouteStartMode RouteStartMode => _routeStartMode;
    public Vector3 PositionOffset => _positionOffset;
    public Vector3 RotationOffset => _rotationOffset;
    public float SpawnRadius => _spawnRadius;
    public float NavMeshSampleRadius => _navMeshSampleRadius;
    public bool OverrideRandomizeYaw => _overrideRandomizeYaw;
    public bool RandomizeYaw => _randomizeYaw;

    public void SetPatrolRoute(EnemyPatrolRoute route)
    {
        _patrolRoute = route;
    }

    public string BuildSpawnedName(int sequenceIndex)
    {
        if (!string.IsNullOrWhiteSpace(_label))
        {
            return $"{_label}_{sequenceIndex:00}";
        }

        return _enemyPrefab != null ? $"{_enemyPrefab.name}_{sequenceIndex:00}" : $"Enemy_{sequenceIndex:00}";
    }
}
