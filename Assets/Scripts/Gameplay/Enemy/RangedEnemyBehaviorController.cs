using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic ranged enemy behaviour with patrol, chase and ranged attack.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class RangedEnemyBehaviorController : MonoBehaviour, IEnemyVisionSource
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    public EnemyState CurrentState;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private RangedEnemyConfig _config;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform FirePoint;

    [HideInInspector] public GameObject EnemyBulletPrefab;
    [HideInInspector] public float BulletMoveSpeed = 15f;
    [HideInInspector] public float BulletDamage = 15f;
    [HideInInspector] public float BulletLifeTime = 3f;

    [HideInInspector]
    public float PatrolRadius = 10f;
    [HideInInspector]
    public float PatrolWaitTime = 2f;

    [HideInInspector]
    public float DetectionRange = 15f;
    [HideInInspector]
    public float ViewAngle = 360f;
    [HideInInspector]
    public LayerMask LineOfSightBlockMask = 1;
    [HideInInspector]
    public LayerMask GroundMask = 1;
    [HideInInspector]
    public float EyeHeight = 1.2f;
    [HideInInspector]
    public float TargetHeight = 1f;
    [HideInInspector]
    public float LoseRange = 20f;

    [HideInInspector]
    public float AttackRange = 10f;
    [HideInInspector]
    public float AttackInterval = 2f;

    private NavMeshAgent _navMeshAgent;
    private EnemyPatrolRouteFollower _patrolRouteFollower;
    private EnemyPatrolAwarenessController _patrolAwareness;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private float _waitTimer;
    private float _attackTimer;
    private bool _hasWarnedMissingFixedRoute;

    public Transform VisionTransform => _patrolAwareness != null ? _patrolAwareness.VisionTransform : transform;
    Transform IEnemyVisionSource.PlayerTransform => PlayerTransform;
    float IEnemyVisionSource.DetectionRange => DetectionRange;
    float IEnemyVisionSource.ViewAngle => ViewAngle;
    LayerMask IEnemyVisionSource.LineOfSightBlockMask => LineOfSightBlockMask;
    LayerMask IEnemyVisionSource.GroundMask => GroundMask;
    float IEnemyVisionSource.EyeHeight => EyeHeight;
    float IEnemyVisionSource.TargetHeight => TargetHeight;
    public bool ShouldShowVision => CurrentState == EnemyState.Patrol || (_patrolAwareness != null && _patrolAwareness.IsInAwarenessState);
    public bool CanSeePlayerForVision => CanSeePlayer();

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        _navMeshAgent = GetComponent<NavMeshAgent>();
        EnemyAwarenessRuntimeInstaller.EnsureAwarenessComponents(gameObject);
        _patrolAwareness = GetComponent<EnemyPatrolAwarenessController>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        ApplyHealthConfig();
        EnsureAgentReady();
        EnsurePlayerTransform();
        InitializePatrolRoute();

        SetNextPatrolDestination();
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing RangedEnemyConfig.", this);
            enabled = false;
            return false;
        }

        EnemyBulletPrefab = _config.EnemyBulletPrefab;
        BulletMoveSpeed = _config.BulletMoveSpeed;
        BulletDamage = _config.BulletDamage;
        BulletLifeTime = _config.BulletLifeTime;
        PatrolRadius = _config.Patrol.PatrolRadius;
        PatrolWaitTime = _config.Patrol.PatrolWaitTime;
        _patrolMode = _config.Patrol.PatrolMode;
        DetectionRange = _config.Detection.DetectionRange;
        ViewAngle = _config.Detection.ViewAngle;
        LineOfSightBlockMask = _config.Detection.LineOfSightBlockMask;
        GroundMask = _config.Detection.GroundMask;
        EyeHeight = _config.Detection.EyeHeight;
        TargetHeight = _config.Detection.TargetHeight;
        LoseRange = _config.Detection.LoseRange;
        AttackRange = _config.AttackRange;
        AttackInterval = _config.AttackInterval;
        return true;
    }

    private void ApplyHealthConfig()
    {
        EnemyHealthController healthController = GetComponent<EnemyHealthController>();
        if (healthController != null)
        {
            healthController.ApplyConfig(_config);
        }
    }

    private void Update()
    {
        if (!EnsurePlayerTransform())
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        switch (CurrentState)
        {
            case EnemyState.Patrol:
                PatrolBehavior(distanceToPlayer);
                break;
            case EnemyState.Chase:
                ChaseBehavior(distanceToPlayer);
                break;
            case EnemyState.Attack:
                AttackBehavior(distanceToPlayer);
                break;
        }
    }

    private void PatrolBehavior(float distanceToPlayer)
    {
        if (_patrolAwareness != null &&
            _patrolAwareness.TickAwareness(
                CanSeePlayer,
                () => CurrentState = EnemyState.Chase,
                ResetPatrolDestination))
        {
            return;
        }

        if (CanSeePlayer())
        {
            EnemySuspicionStimulusBus.Raise(
                EnemySuspicionStimulusType.PlayerLastSeen,
                PlayerTransform.position,
                Mathf.Max(DetectionRange, 12f),
                0.8f,
                2.5f,
                0.8f,
                PlayerTransform);
            CurrentState = EnemyState.Chase;
            return;
        }

        bool isWaiting = HasReachedCurrentDestination();
        _patrolAwareness?.TickPassivePatrol(
            isWaiting,
            GetCurrentPatrolLookTarget(),
            GetCurrentPatrolWaitScanArc());
        if (_patrolAwareness != null && _patrolAwareness.IsInAwarenessState)
        {
            return;
        }

        if (isWaiting)
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= GetCurrentPatrolWaitTime())
            {
                SetNextPatrolDestination();
                _waitTimer = 0f;
            }
        }
    }

    private void ChaseBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            CurrentState = EnemyState.Patrol;
            EnemySuspicionStimulusBus.Raise(
                EnemySuspicionStimulusType.PlayerLastSeen,
                PlayerTransform.position,
                DetectionRange,
                0.78f,
                3f,
                1.6f,
                PlayerTransform);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            CurrentState = EnemyState.Attack;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetDestination(PlayerTransform.position);
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > AttackRange)
        {
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        transform.LookAt(lookPosition);

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackInterval)
        {
            Shoot();
            _attackTimer = 0f;
        }
    }

    private void Shoot()
    {
        if (FirePoint == null || EnemyBulletPrefab == null)
        {
            return;
        }

        GameObject bulletObject = Instantiate(EnemyBulletPrefab, FirePoint.position, FirePoint.rotation);
        EnemyBulletController bullet = bulletObject.GetComponent<EnemyBulletController>();
        if (bullet != null)
        {
            bullet.MoveSpeed = BulletMoveSpeed;
            bullet.Damage = BulletDamage;
            bullet.LifeTime = BulletLifeTime;
            bullet.SourceEnemy = gameObject;
            bullet.SkillName = "Ranged Shot";
        }
    }

    private void InitializePatrolRoute()
    {
        _patrolRouteFollower = GetComponent<EnemyPatrolRouteFollower>();
    }

    private void SetNextPatrolDestination()
    {
        if (ShouldUseFixedPatrol())
        {
            if (_patrolRouteFollower != null &&
                _patrolRouteFollower.TryAdvanceToNextDestination(transform.position, out Vector3 routeDestination))
            {
                TrySetDestination(routeDestination);
                return;
            }

            WarnMissingFixedRouteIfNeeded();
        }

        SetRandomPatrolDestination();
    }

    private void ResetPatrolDestination()
    {
        if (ShouldUseFixedPatrol())
        {
            if (_patrolRouteFollower != null &&
                _patrolRouteFollower.TrySetNearestDestination(transform.position, out Vector3 routeDestination))
            {
                TrySetDestination(routeDestination);
                return;
            }

            WarnMissingFixedRouteIfNeeded();
        }

        SetRandomPatrolDestination();
    }

    private void SetRandomPatrolDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;
        randomDirection += _startingPosition;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, PatrolRadius, NavMesh.AllAreas))
        {
            TrySetDestination(hit.position);
        }
    }

    private float GetCurrentPatrolWaitTime()
    {
        return ShouldUseFixedPatrol() && _patrolRouteFollower != null
            ? _patrolRouteFollower.GetCurrentWaitTime(PatrolWaitTime)
            : PatrolWaitTime;
    }

    private Transform GetCurrentPatrolLookTarget()
    {
        return ShouldUseFixedPatrol() &&
               _patrolRouteFollower != null &&
               _patrolRouteFollower.TryGetCurrentLookTarget(out Transform lookTarget)
            ? lookTarget
            : null;
    }

    private float GetCurrentPatrolWaitScanArc()
    {
        return ShouldUseFixedPatrol() && _patrolRouteFollower != null
            ? _patrolRouteFollower.GetCurrentWaitScanArc(-1f)
            : -1f;
    }

    private bool ShouldUseFixedPatrol()
    {
        return _patrolMode == EnemyPatrolMode.FixedRoute;
    }

    private void WarnMissingFixedRouteIfNeeded()
    {
        if (!ShouldUseFixedPatrol() || _hasWarnedMissingFixedRoute || _patrolRouteFollower != null)
        {
            return;
        }

        _hasWarnedMissingFixedRoute = true;
        Debug.LogWarning($"[{name}] Patrol mode is FixedRoute, but no EnemyPatrolRouteFollower was assigned. Falling back to random-radius patrol.", this);
    }

    private bool EnsureAgentReady()
    {
        if (_navMeshAgent == null)
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            return false;
        }

        if (_navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        float searchRadius = Mathf.Max(2f, PatrolRadius);
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            _navMeshAgent.Warp(hit.position);
        }
        else
        {
            RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
        }

        return _navMeshAgent.isOnNavMesh;
    }

    private bool HasReachedCurrentDestination()
    {
        return EnsureAgentReady() &&
               !_navMeshAgent.pathPending &&
               _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance;
    }

    private void SetAgentStopped(bool isStopped)
    {
        if (EnsureAgentReady())
        {
            _navMeshAgent.isStopped = isStopped;
        }
    }

    private bool TrySetDestination(Vector3 destination)
    {
        return EnsureAgentReady() && _navMeshAgent.SetDestination(destination);
    }

    private bool CanSeePlayer()
    {
        return EnemyVisionUtility.CanSeeTarget(
            VisionTransform,
            PlayerTransform,
            DetectionRange,
            ViewAngle,
            LineOfSightBlockMask,
            EyeHeight,
            TargetHeight);
    }

    private bool EnsurePlayerTransform()
    {
        if (PlayerTransform != null)
        {
            if (PlayerTransform.GetComponent<PlayerHealthController>() != null)
            {
                return true;
            }

            if (PlayerHealthController.Instance != null)
            {
                PlayerTransform = PlayerHealthController.Instance.transform;
                return true;
            }

            return true;
        }

        if (PlayerHealthController.Instance != null)
        {
            PlayerTransform = PlayerHealthController.Instance.transform;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return false;
        }

        PlayerTransform = playerObject.transform;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        EnemyVisionUtility.DrawVisionGizmos(
            VisionTransform,
            PlayerTransform,
            DetectionRange,
            ViewAngle,
            EyeHeight,
            TargetHeight,
            Color.red);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
