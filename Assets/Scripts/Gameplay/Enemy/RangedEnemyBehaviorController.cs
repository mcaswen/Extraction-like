using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 基础远程敌人行为控制器。
/// 负责巡逻、发现目标、追击、远程射击和直接受击反击。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class RangedEnemyBehaviorController : MonoBehaviour, IEnemyVisionSource, IEnemyDirectDamageReceiver
{
    private const float DirectDamageForcedChaseDuration = 4f;
    private const float DirectDamageDestinationSampleRadius = 4f;

    /// <summary>
    /// 基础远程敌人的主行为状态。
    /// </summary>
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    /// <summary>
    /// 当前主行为状态。
    /// </summary>
    public EnemyState CurrentState;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private RangedEnemyConfig _config;

    [Header("References")]
    /// <summary>
    /// 当前战斗目标，通常是玩家，也可能是 Agent。
    /// </summary>
    public Transform PlayerTransform;

    /// <summary>
    /// 子弹生成位置和朝向。
    /// </summary>
    public Transform FirePoint;

    /// <summary>
    /// 远程敌人发射的子弹预制体。
    /// </summary>
    [HideInInspector] public GameObject EnemyBulletPrefab;

    /// <summary>
    /// 子弹飞行速度。
    /// </summary>
    [HideInInspector] public float BulletMoveSpeed = 15f;

    /// <summary>
    /// 子弹命中伤害。
    /// </summary>
    [HideInInspector] public float BulletDamage = 15f;

    /// <summary>
    /// 子弹自动销毁前的存活时间。
    /// </summary>
    [HideInInspector] public float BulletLifeTime = 3f;

    /// <summary>
    /// 随机巡逻半径。
    /// </summary>
    [HideInInspector]
    public float PatrolRadius = 10f;

    /// <summary>
    /// 到达巡逻点后的等待时间。
    /// </summary>
    [HideInInspector]
    public float PatrolWaitTime = 2f;

    /// <summary>
    /// 发现目标的视野距离。
    /// </summary>
    [HideInInspector]
    public float DetectionRange = 15f;

    /// <summary>
    /// 发现目标的水平视野角度。
    /// </summary>
    [HideInInspector]
    public float ViewAngle = 360f;

    /// <summary>
    /// 视线检测阻挡层。
    /// </summary>
    [HideInInspector]
    public LayerMask LineOfSightBlockMask = 1;

    /// <summary>
    /// 视野可视化贴地层。
    /// </summary>
    [HideInInspector]
    public LayerMask GroundMask = 1;

    /// <summary>
    /// 敌人眼睛高度。
    /// </summary>
    [HideInInspector]
    public float EyeHeight = 1.2f;

    /// <summary>
    /// 目标检测高度。
    /// </summary>
    [HideInInspector]
    public float TargetHeight = 1f;

    /// <summary>
    /// 目标超过该距离后脱战。
    /// </summary>
    [HideInInspector]
    public float LoseRange = 20f;

    /// <summary>
    /// 远程射击距离。
    /// </summary>
    [HideInInspector]
    public float AttackRange = 10f;

    /// <summary>
    /// 射击间隔。
    /// </summary>
    [HideInInspector]
    public float AttackInterval = 2f;

    private NavMeshAgent _navMeshAgent;
    private EnemyAnimatorDriver _animatorDriver;
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

    /// <summary>
    /// 视野检测使用的节点。
    /// </summary>
    public Transform VisionTransform => _patrolAwareness != null ? _patrolAwareness.VisionTransform : transform;
    Transform IEnemyVisionSource.PlayerTransform => PlayerTransform;
    float IEnemyVisionSource.DetectionRange => DetectionRange;
    float IEnemyVisionSource.ViewAngle => ViewAngle;
    LayerMask IEnemyVisionSource.LineOfSightBlockMask => LineOfSightBlockMask;
    LayerMask IEnemyVisionSource.GroundMask => GroundMask;
    float IEnemyVisionSource.EyeHeight => EyeHeight;
    float IEnemyVisionSource.TargetHeight => TargetHeight;
    /// <summary>
    /// 仅在巡逻或巡逻感知状态下显示视野扇形。
    /// </summary>
    public bool ShouldShowVision => CurrentState == EnemyState.Patrol || (_patrolAwareness != null && _patrolAwareness.IsInAwarenessState);

    /// <summary>
    /// 当前视野系统是否判定能看到目标。
    /// </summary>
    public bool CanSeePlayerForVision => CanSeePlayer();

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animatorDriver = new EnemyAnimatorDriver(this);
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
        // 主状态机只负责大阶段切换，巡逻中的怀疑/调查/搜索交给 EnemyPatrolAwarenessController。
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

        UpdateAnimatorSpeed();
    }

    private void LateUpdate()
    {
        _animatorDriver?.LateUpdate();
    }

    private void PatrolBehavior(float distanceToPlayer)
    {
        // 如果巡逻感知正在处理怀疑事件，本帧不再推进普通巡逻。
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
        // 直接受击后的短时间强制追击可以避免远距离命中后立刻被 LoseRange 拉回巡逻。
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

        _animatorDriver?.TriggerAttack();
        // 子弹携带 SourceEnemy，命中后能在日志和伤害来源中追溯到发射者。
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

    private void UpdateAnimatorSpeed()
    {
        _animatorDriver?.SetSpeedFromAgent(_navMeshAgent);
    }

    /// <summary>
    /// 直接受击时锁定攻击者，清理巡逻感知并进入追击。
    /// </summary>
    /// <param name="context">本次受击上下文。</param>
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
