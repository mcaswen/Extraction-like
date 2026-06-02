using Gameplay.SkillEffect;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 现代搁浅者白模行为。
/// 使用触手吸附玩家，并施加腐蚀性黏液的持续伤害。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class ModernStranderBehaviorController : MonoBehaviour, IEnemyVisionSource, IEnemyDirectDamageReceiver
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
    private ModernStranderConfig _config;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform TentacleOrigin;
    public LineRenderer TentacleRenderer;

    [HideInInspector]
    public float PatrolRadius = 8f;
    [HideInInspector]
    public float PatrolWaitTime = 1.5f;

    [HideInInspector]
    public float DetectionRange = 12f;
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
    public float LoseRange = 16f;

    [HideInInspector]
    public float AttackRange = 3.2f;
    [HideInInspector]
    public float AttackInterval = 5f;
    [HideInInspector]
    public float TentacleLatchDuration = 1.1f;
    [HideInInspector]
    public float TentacleHitboxWidth = 0.55f;
    [HideInInspector]
    public float TentacleHitboxHeight = 0.55f;
    [HideInInspector]
    public float LatchPullStrength = 3.4f;
    [HideInInspector]
    public float CorrosionDamagePerSecond = 10f;
    [HideInInspector]
    public float CorrosionDuration = 2.5f;
    [HideInInspector]
    public float CorrosionTickInterval = 0.25f;
    [HideInInspector]
    public float InitialContactDamage = 6f;

    [HideInInspector]
    public GameObject CorrosivePuddlePrefab;
    [HideInInspector]
    public float PuddleLifetime = 5f;
    [HideInInspector]
    public float PuddleRadius = 1.1f;
    [HideInInspector]
    public float PuddleDamagePerSecond = 6f;
    [HideInInspector]
    public float PuddleCorrosionDuration = 1.8f;
    [HideInInspector]
    public float PuddleTickInterval = 0.25f;

    private NavMeshAgent _navMeshAgent;
    private EnemyPatrolRouteFollower _patrolRouteFollower;
    private EnemyPatrolAwarenessController _patrolAwareness;
    private PlayerHealthController _playerHealthController;
    private PlayerMovementController _playerMovementController;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;
    private float _waitTimer;
    private float _attackTimer;
    private float _latchTimer;
    private float _tentacleTotalDamage;
    private float _directDamageForcedChaseEndTime = -1f;
    private Vector3 _directDamageFallbackPosition;
    private bool _isTentacleStriking;
    private bool _isTentacleLatched;
    private bool _hasAppliedInitialLatchDamage;
    private bool _hasAddedTentacleCorrosionDamage;
    private bool _hasDirectDamageFallbackPosition;
    private bool _hasWarnedMissingFixedRoute;
    private ModernStranderTentacleHitbox _tentacleHitbox;

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
        EnsurePlayerReferences();
        InitializePatrolRoute();

        EnsureTentacleRenderer();
        EnsureTentacleHitbox();
        SetNextPatrolDestination();
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing ModernStranderConfig.", this);
            enabled = false;
            return false;
        }

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
        TentacleLatchDuration = _config.TentacleLatchDuration;
        TentacleHitboxWidth = _config.TentacleHitboxWidth;
        TentacleHitboxHeight = _config.TentacleHitboxHeight;
        LatchPullStrength = _config.LatchPullStrength;
        CorrosionDamagePerSecond = _config.CorrosionDamagePerSecond;
        CorrosionDuration = _config.CorrosionDuration;
        CorrosionTickInterval = _config.CorrosionTickInterval;
        InitialContactDamage = _config.InitialContactDamage;
        CorrosivePuddlePrefab = _config.CorrosivePuddlePrefab;
        PuddleLifetime = _config.PuddleLifetime;
        PuddleRadius = _config.PuddleRadius;
        PuddleDamagePerSecond = _config.PuddleDamagePerSecond;
        PuddleCorrosionDuration = _config.PuddleCorrosionDuration;
        PuddleTickInterval = _config.PuddleTickInterval;
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
        if (!EnsurePlayerReferences())
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

        UpdateTentacleVisual();
        UpdateTentacleHitbox();
    }

    private void OnDisable()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;

        // Space-hourglass magic seal can disable this behaviour mid-attack.
        // Ensure any active tentacle latch/hitbox stops immediately.
        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(false);

        if (TentacleRenderer != null)
        {
            TentacleRenderer.enabled = false;
        }

        if (hadActiveTentacle)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Corrosive Tentacle", totalDamage);
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
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            CurrentState = EnemyState.Attack;
            _attackTimer = AttackInterval;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer > AttackRange)
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        transform.LookAt(lookPosition);

        if (_isTentacleLatched)
        {
            _latchTimer += Time.deltaTime;

            if (_playerHealthController != null)
            {
                _playerHealthController.ApplyCorrosion(
                    CorrosionDamagePerSecond,
                    CorrosionDuration,
                    CorrosionTickInterval);
            }

            if (_playerMovementController != null)
            {
                Vector3 pullTarget = TentacleOrigin != null ? TentacleOrigin.position : transform.position;
                _playerMovementController.ApplyExternalPull(pullTarget, LatchPullStrength);
            }

            if (_latchTimer >= TentacleLatchDuration)
            {
                StopTentacleAttack();
            }

            return;
        }

        if (_isTentacleStriking)
        {
            _latchTimer += Time.deltaTime;
            if (_latchTimer >= TentacleLatchDuration)
            {
                StopTentacleAttack();
            }
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackInterval)
        {
            BeginTentacleStrike();
        }
    }

    private void BeginTentacleStrike()
    {
        _attackTimer = 0f;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _isTentacleStriking = true;
        _isTentacleLatched = false;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(true);
    }

    private void StopTentacleAttack()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        bool hadLatchedPlayer = _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;
        Vector3 puddlePosition = PlayerTransform != null ? PlayerTransform.position : transform.position;

        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(false);

        if (hadLatchedPlayer)
        {
            SpawnCorrosivePuddle(puddlePosition);
        }

        if (hadActiveTentacle)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Corrosive Tentacle", totalDamage);
        }
    }

    public void NotifyDirectPlayerDamage(EnemyDamageContext context)
    {
        if (!context.IsDirectPlayerDamage || context.Attacker == null)
        {
            return;
        }

        PlayerTransform = context.Attacker;
        BeginDirectDamageForcedChase(context);
        _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
        if (_playerHealthController == null)
        {
            _playerHealthController = PlayerTransform.GetComponentInParent<PlayerHealthController>();
        }
        _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
        if (_playerMovementController == null)
        {
            _playerMovementController = PlayerTransform.GetComponentInParent<PlayerMovementController>();
        }

        StopTentacleAttack();
        _waitTimer = 0f;
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        FacePlayerImmediately();

        CurrentState = EnemyState.Chase;
        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    public void NotifyTentacleHit(PlayerHealthController playerHealthController, PlayerMovementController playerMovementController)
    {
        if (!_isTentacleStriking || playerHealthController == null)
        {
            return;
        }

        _playerHealthController = playerHealthController;
        _playerMovementController = playerMovementController;
        _isTentacleLatched = true;

        if (_hasAppliedInitialLatchDamage)
        {
            return;
        }

        _hasAppliedInitialLatchDamage = true;
        _tentacleTotalDamage += _playerHealthController.TakeDamage(InitialContactDamage);
        _playerHealthController.ApplyCorrosion(
            CorrosionDamagePerSecond,
            CorrosionDuration,
            CorrosionTickInterval);

        if (!_hasAddedTentacleCorrosionDamage)
        {
            _hasAddedTentacleCorrosionDamage = true;
            _tentacleTotalDamage += CorrosionDamagePerSecond * CorrosionDuration;
        }
    }

    private void EnsureTentacleRenderer()
    {
        if (TentacleRenderer == null)
        {
            TentacleRenderer = GetComponent<LineRenderer>();
        }

        if (TentacleRenderer == null)
        {
            TentacleRenderer = gameObject.AddComponent<LineRenderer>();
        }

        TentacleRenderer.positionCount = 2;
        TentacleRenderer.enabled = false;
        TentacleRenderer.startWidth = 0.12f;
        TentacleRenderer.endWidth = 0.04f;
        TentacleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        TentacleRenderer.receiveShadows = false;
        TentacleRenderer.useWorldSpace = true;
        TentacleRenderer.numCapVertices = 4;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null && TentacleRenderer.material == null)
        {
            TentacleRenderer.material = new Material(lineShader);
        }

        TentacleRenderer.startColor = new Color(0.43f, 0.92f, 0.56f, 1f);
        TentacleRenderer.endColor = new Color(0.08f, 0.38f, 0.12f, 0.55f);
    }

    private void EnsureTentacleHitbox()
    {
        if (_tentacleHitbox != null)
        {
            return;
        }

        GameObject hitboxObject = new GameObject("TentacleHitbox");
        hitboxObject.transform.SetParent(transform, false);
        SkillEffectLayerUtility.ApplyToRoot(hitboxObject);
        _tentacleHitbox = hitboxObject.AddComponent<ModernStranderTentacleHitbox>();
        _tentacleHitbox.Initialize(this, TentacleHitboxWidth, TentacleHitboxHeight);
        SetTentacleHitboxEnabled(false);
    }

    private void UpdateTentacleVisual()
    {
        if (TentacleRenderer == null)
        {
            return;
        }

        if ((!_isTentacleLatched && !_isTentacleStriking) || PlayerTransform == null)
        {
            TentacleRenderer.enabled = false;
            return;
        }

        TentacleRenderer.enabled = true;
        Vector3 origin = TentacleOrigin != null ? TentacleOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        TentacleRenderer.SetPosition(0, origin);
        TentacleRenderer.SetPosition(1, target);
    }

    private void UpdateTentacleHitbox()
    {
        if (_tentacleHitbox == null || PlayerTransform == null)
        {
            return;
        }

        if (!_isTentacleStriking && !_isTentacleLatched)
        {
            _tentacleHitbox.gameObject.SetActive(false);
            return;
        }

        _tentacleHitbox.gameObject.SetActive(true);
        Vector3 origin = TentacleOrigin != null ? TentacleOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        _tentacleHitbox.UpdateHitboxTransform(origin, target);
    }

    private void SetTentacleHitboxEnabled(bool isEnabled)
    {
        if (_tentacleHitbox == null)
        {
            return;
        }

        _tentacleHitbox.gameObject.SetActive(isEnabled);
    }

    private void SpawnCorrosivePuddle(Vector3 worldPosition)
    {
        Vector3 puddlePosition = new Vector3(worldPosition.x, 0.02f, worldPosition.z);

        if (CorrosivePuddlePrefab != null)
        {
            GameObject puddleObject = Instantiate(CorrosivePuddlePrefab, puddlePosition, Quaternion.identity);
            SkillEffectLayerUtility.ApplyToRoot(puddleObject);
            CorrosiveSlimePuddle puddle = puddleObject.GetComponent<CorrosiveSlimePuddle>();
            if (puddle != null)
            {
                puddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
            }
            return;
        }

        GameObject defaultPuddleObject = new GameObject("CorrosiveSlimePuddle");
        defaultPuddleObject.transform.position = puddlePosition;
        SkillEffectLayerUtility.ApplyToRoot(defaultPuddleObject);
        CorrosiveSlimePuddle defaultPuddle = defaultPuddleObject.AddComponent<CorrosiveSlimePuddle>();
        defaultPuddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
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

    private bool EnsurePlayerReferences()
    {
        if (PlayerTransform == null)
        {
            if (PlayerHealthController.Instance != null)
            {
                PlayerTransform = PlayerHealthController.Instance.transform;
            }
            else
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    PlayerTransform = playerObject.transform;
                }
            }
        }

        if (PlayerTransform != null)
        {
            if (_playerHealthController == null)
            {
                _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
                if (_playerHealthController == null)
                {
                    _playerHealthController = PlayerTransform.gameObject.AddComponent<PlayerHealthController>();
                }
            }

            if (_playerMovementController == null)
            {
                _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
            }
        }

        if (_playerHealthController == null && PlayerHealthController.Instance != null)
        {
            _playerHealthController = PlayerHealthController.Instance;
            PlayerTransform = _playerHealthController.transform;
        }

        if (_playerMovementController == null && PlayerTransform != null)
        {
            _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
        }

        return PlayerTransform != null && _playerHealthController != null;
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
            Color.cyan);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }
}
