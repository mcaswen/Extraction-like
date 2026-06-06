using System;
using System.Collections.Generic;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人刷新点。
/// 运行时生成一个配置好的敌人，并可把固定巡逻路线绑定给生成物。
/// </summary>
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

    /// <summary>
    /// 当前刷新点生成的敌人预制体。
    /// </summary>
    public GameObject EnemyPrefab => _enemyPrefab;

    /// <summary>
    /// 当前刷新点绑定的固定巡逻路线。
    /// </summary>
    public EnemyPatrolRoute PatrolRoute => _patrolRoute;

    /// <summary>
    /// 当前刷新点本轮是否已经生成过敌人。
    /// </summary>
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
    /// <summary>
    /// 在 Play Mode 中生成该刷新点配置的敌人。
    /// </summary>
    /// <returns>生成出的敌人对象，失败时返回 null。</returns>
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

        GameObject enemyPrefab = ResolveEnemyPrefab();
        if (enemyPrefab == null)
        {
            return null;
        }

        Vector3 position = ResolveSpawnPosition();
        Quaternion rotation = ResolveSpawnRotation();
        GameObject enemy = Instantiate(enemyPrefab, position, rotation);
        enemy.name = BuildSpawnedName(enemyPrefab);
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
    /// <summary>
    /// 兼容旧编辑器入口的批量刷新方法；当前实现等同于 Spawn。
    /// </summary>
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
    /// <summary>
    /// 兼容旧多条目刷新接口；当前刷新点只生成自身配置的单个敌人。
    /// </summary>
    /// <param name="entry">旧刷新条目，已不再使用。</param>
    /// <param name="sequenceIndex">旧序号，已不再使用。</param>
    /// <returns>生成出的敌人对象。</returns>
    public GameObject Spawn(EnemySpawnEntry entry, int sequenceIndex)
    {
        return Spawn();
    }

    /// <summary>
    /// 从子节点中收集第一条固定巡逻路线。
    /// </summary>
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

    private GameObject ResolveEnemyPrefab()
    {
        EnemySourceClusterAuthoring sourceCluster = GetComponentInParent<EnemySourceClusterAuthoring>();
        if (sourceCluster != null &&
            sourceCluster.TryResolveEnemyPrefabForSpawnPoint(transform, out GameObject clusterEnemyPrefab))
        {
            return clusterEnemyPrefab;
        }

        return _enemyPrefab;
    }

    private string BuildSpawnedName(GameObject enemyPrefab)
    {
        return enemyPrefab != null ? $"{enemyPrefab.name}_00" : "Enemy_00";
    }

    // 刷新点声明敌人来源；生成出的活动敌人会交给目标注册表追踪。
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

        // 旧数据曾允许一个刷新点带多个条目，现在迁移首个有效条目到单敌人配置。
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

/// <summary>
/// 旧版敌人刷新条目数据。
/// 当前主要用于从历史序列化数据迁移到单敌人刷新点。
/// </summary>
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

    /// <summary>
    /// 旧条目的显示标签。
    /// </summary>
    public string Label => _label;

    /// <summary>
    /// 旧条目中的敌人预制体。
    /// </summary>
    public GameObject EnemyPrefab => _enemyPrefab;

    /// <summary>
    /// 旧条目中的生成数量。
    /// </summary>
    public int SpawnCount => _spawnCount;

    /// <summary>
    /// 旧条目中的固定巡逻路线。
    /// </summary>
    public EnemyPatrolRoute PatrolRoute => _patrolRoute;

    /// <summary>
    /// 旧条目中的路线起始模式。
    /// </summary>
    public EnemyPatrolRouteStartMode RouteStartMode => _routeStartMode;

    /// <summary>
    /// 旧条目中的位置偏移。
    /// </summary>
    public Vector3 PositionOffset => _positionOffset;

    /// <summary>
    /// 旧条目中的旋转偏移。
    /// </summary>
    public Vector3 RotationOffset => _rotationOffset;

    /// <summary>
    /// 旧条目中的随机生成半径。
    /// </summary>
    public float SpawnRadius => _spawnRadius;

    /// <summary>
    /// 旧条目中的 NavMesh 采样半径。
    /// </summary>
    public float NavMeshSampleRadius => _navMeshSampleRadius;

    /// <summary>
    /// 旧条目是否覆盖随机朝向设置。
    /// </summary>
    public bool OverrideRandomizeYaw => _overrideRandomizeYaw;

    /// <summary>
    /// 旧条目是否随机 Y 轴朝向。
    /// </summary>
    public bool RandomizeYaw => _randomizeYaw;

    /// <summary>
    /// 设置旧条目的巡逻路线引用。
    /// </summary>
    /// <param name="route">巡逻路线。</param>
    public void SetPatrolRoute(EnemyPatrolRoute route)
    {
        _patrolRoute = route;
    }

    /// <summary>
    /// 根据旧标签和序号生成敌人实例名称。
    /// </summary>
    /// <param name="sequenceIndex">生成序号。</param>
    /// <returns>生成对象名称。</returns>
    public string BuildSpawnedName(int sequenceIndex)
    {
        if (!string.IsNullOrWhiteSpace(_label))
        {
            return $"{_label}_{sequenceIndex:00}";
        }

        return _enemyPrefab != null ? $"{_enemyPrefab.name}_{sequenceIndex:00}" : $"Enemy_{sequenceIndex:00}";
    }
}
