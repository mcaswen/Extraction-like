using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic ranged enemy behaviour with patrol, chase and ranged attack.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class RangedEnemyBehaviorController : MonoBehaviour, IEnemyVisionSource, IEnemyDirectDamageReceiver
{
    private const float DirectDamageForcedChaseDuration = 4f;
    private const float DirectDamageDestinationSampleRadius = 4f;

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
    private ICombatDamageReceiver _combatDamageReceiver;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;
    private float _waitTimer;
    private float _attackTimer;
    private float _directDamageForcedChaseEndTime = -1f;
    private Vector3 _directDamageFallbackPosition;
    private bool _hasDirectDamageFallbackPosition;
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
        _patrolAwareness?.ConfigurePreset(_awarenessPreset);
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
        _awarenessPreset = _config.Detection.AwarenessPreset;
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
            ReportPlayerLastSeen(Mathf.Max(DetectionRange, 12f), 0.8f, 2.5f, 0.8f);
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
        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            CurrentState = EnemyState.Patrol;
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
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
        TrySetChaseDestination();
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

    public void NotifyDirectDamage(EnemyDamageContext context)
    {
        if (!context.IsDirectDamage || context.Attacker == null)
        {
            return;
        }

        AssignCombatTarget(context.Attacker);
        BeginDirectDamageForcedChase(context);
        _waitTimer = 0f;
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        FacePlayerImmediately();

        CurrentState = EnemyState.Chase;
        SetAgentStopped(false);
        TrySetChaseDestination();
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

    private void ReportPlayerLastSeen(float radius, float strength, float duration, float uncertaintyRadius)
    {
        if (!_awarenessPreset.ShouldReportPlayerLastSeen() || PlayerTransform == null)
        {
            return;
        }

        EnemySuspicionStimulusBus.Raise(
            EnemySuspicionStimulusType.PlayerLastSeen,
            PlayerTransform.position,
            radius,
            strength,
            duration,
            uncertaintyRadius,
            PlayerTransform);
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

    private bool TrySetChaseDestination()
    {
        if (PlayerTransform == null)
        {
            return false;
        }

        if (TrySetSampledDestination(PlayerTransform.position, DirectDamageDestinationSampleRadius))
        {
            return true;
        }

        if (_hasDirectDamageFallbackPosition &&
            TrySetSampledDestination(_directDamageFallbackPosition, DirectDamageDestinationSampleRadius))
        {
            return true;
        }

        return TrySetDestination(PlayerTransform.position);
    }

    private bool TrySetSampledDestination(Vector3 position, float radius)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, Mathf.Max(0.1f, radius), NavMesh.AllAreas) &&
               TrySetDestination(hit.position);
    }

    private void BeginDirectDamageForcedChase(EnemyDamageContext context)
    {
        _directDamageForcedChaseEndTime = Time.time + DirectDamageForcedChaseDuration;
        _directDamageFallbackPosition = ResolveDirectDamageFallbackPosition(context);
        _hasDirectDamageFallbackPosition = true;
    }

    private bool IsDirectDamageForcedChaseActive()
    {
        return Time.time < _directDamageForcedChaseEndTime;
    }

    private Vector3 ResolveDirectDamageFallbackPosition(EnemyDamageContext context)
    {
        if (context.SourcePosition.sqrMagnitude > 0.0001f)
        {
            return context.SourcePosition;
        }

        if (context.Attacker != null)
        {
            return context.Attacker.position;
        }

        if (context.HitPosition.sqrMagnitude > 0.0001f)
        {
            return context.HitPosition;
        }

        return transform.position;
    }

    private void FacePlayerImmediately()
    {
        if (PlayerTransform == null)
        {
            return;
        }

        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        Vector3 direction = lookPosition - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
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
        if (PlayerTransform != null && AssignCombatTarget(PlayerTransform))
        {
            return true;
        }

        if (PlayerHealthController.Instance != null)
        {
            PlayerTransform = PlayerHealthController.Instance.transform;
            return AssignCombatTarget(PlayerTransform);
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return false;
        }

        PlayerTransform = playerObject.transform;
        return AssignCombatTarget(PlayerTransform);
    }

    private bool AssignCombatTarget(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        PlayerTransform = target;
        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver))
        {
            _combatDamageReceiver = receiver;
            if (receiver.DamageRootTransform != null)
            {
                PlayerTransform = receiver.DamageRootTransform;
            }

            return true;
        }

        _combatDamageReceiver = null;
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
