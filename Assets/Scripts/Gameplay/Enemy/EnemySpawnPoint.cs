using System;
using System.Collections.Generic;
using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemySpawnPoint : MonoBehaviour
{
    private static readonly float SpawnRadius = 0f;
    private const float NavMeshSampleRadius = 3f;
    private static readonly bool RandomizeYaw = true;
    private static readonly Vector3 PositionOffset = Vector3.zero;
    private static readonly Vector3 RotationOffset = Vector3.zero;
    private const EnemyPatrolRouteStartMode RouteStartMode = EnemyPatrolRouteStartMode.Closest;

    [SerializeField]
    private GameObject _enemyPrefab;

    [SerializeField]
    private EnemyPatrolRoute _patrolRoute;

    [SerializeField, HideInInspector]
    private List<EnemySpawnEntry> _entries = new List<EnemySpawnEntry>();

    private GameObject _spawnedEnemy;
    private bool _hasSpawned;

    public GameObject EnemyPrefab => _enemyPrefab;
    public EnemyPatrolRoute PatrolRoute => _patrolRoute;
    public bool HasSpawned => _hasSpawned;

    private void Awake()
    {
        MigrateLegacyEntryIfNeeded();
    }

    private void Start()
    {
        Spawn();
    }

    [ContextMenu("Spawn")]
    public GameObject Spawn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{name}] Spawn only runs in Play Mode.", this);
            return null;
        }

        if (_hasSpawned)
        {
            return _spawnedEnemy;
        }

        _hasSpawned = true;

        if (_enemyPrefab == null)
        {
            return null;
        }

        Vector3 position = ResolveSpawnPosition();
        Quaternion rotation = ResolveSpawnRotation();
        GameObject enemy = Instantiate(_enemyPrefab, position, rotation);
        enemy.name = BuildSpawnedName();
        _spawnedEnemy = enemy;

        EnemyPatrolRouteFollower follower = enemy.GetComponent<EnemyPatrolRouteFollower>();
        if (follower == null)
        {
            follower = enemy.AddComponent<EnemyPatrolRouteFollower>();
        }

        follower.AssignRoute(
            _patrolRoute,
            RouteStartMode,
            0,
            NavMeshSampleRadius);

        RegisterSpawnedEnemyTarget(enemy);
        return enemy;
    }

    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{name}] Spawn All only runs in Play Mode.", this);
            return;
        }

        Spawn();
    }

    [Obsolete("EnemySpawnPoint now spawns one configured enemy per round. Use Spawn() instead.")]
    public GameObject Spawn(EnemySpawnEntry entry, int sequenceIndex)
    {
        return Spawn();
    }

    public void CollectChildRoutes()
    {
        if (_patrolRoute != null)
        {
            return;
        }

        EnemyPatrolRoute[] routes = GetComponentsInChildren<EnemyPatrolRoute>(true);
        if (routes != null && routes.Length > 0)
        {
            _patrolRoute = routes[0];
        }
    }

    private Vector3 ResolveSpawnPosition()
    {
        Vector3 localOffset = PositionOffset;
        if (SpawnRadius > 0f)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * SpawnRadius;
            localOffset += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        Vector3 desired = transform.TransformPoint(localOffset);
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning($"[{name}] Could not sample spawn position on NavMesh. Using raw spawn position.", this);
        return desired;
    }

    private Quaternion ResolveSpawnRotation()
    {
        Quaternion baseRotation = transform.rotation * Quaternion.Euler(RotationOffset);
        if (!RandomizeYaw)
        {
            return baseRotation;
        }

        return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * baseRotation;
    }

    private string BuildSpawnedName()
    {
        return _enemyPrefab != null ? $"{_enemyPrefab.name}_00" : "Enemy_00";
    }

    // Spawn points declare source ownership; spawned active enemies are handed to the target registry.
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
        MigrateLegacyEntryIfNeeded();
    }

    private void MigrateLegacyEntryIfNeeded()
    {
        if (_enemyPrefab != null || _entries == null)
        {
            return;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            EnemySpawnEntry entry = _entries[i];
            if (entry == null || entry.EnemyPrefab == null)
            {
                continue;
            }

            _enemyPrefab = entry.EnemyPrefab;
            _patrolRoute = entry.PatrolRoute;
            break;
        }
    }

    private void OnDrawGizmos()
    {
        DrawGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmos(true);
    }

    private void DrawGizmos(bool selected)
    {
        Color color = new Color(1f, 0.55f, 0.2f, selected ? 1f : 0.45f);
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, selected ? 0.32f : 0.22f);

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
