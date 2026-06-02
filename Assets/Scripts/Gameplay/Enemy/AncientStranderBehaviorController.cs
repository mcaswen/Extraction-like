using Gameplay.SkillEffect;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 古代搁浅者白模行为。
/// 近战挥舞鱼骨造成小范围伤害，中距离延伸鱼骨并撕咬玩家。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class AncientStranderBehaviorController : MonoBehaviour, IEnemyVisionSource, IEnemyDirectDamageReceiver
{
    private const float DirectDamageForcedChaseDuration = 4f;
    private const float DirectDamageDestinationSampleRadius = 4f;

    public enum EnemyState
    {
        Patrol,
        Chase,
        MeleeAttack,
        RangedBiteAttack
    }

    public EnemyState CurrentState;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private AncientStranderConfig _config;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform MeleeOrigin;
    public Transform BiteOrigin;
    public LineRenderer MeleeSwingRenderer;
    public LineRenderer FishboneBiteRenderer;

    [HideInInspector]
    public float PatrolRadius = 8f;
    [HideInInspector]
    public float PatrolWaitTime = 1.4f;

    [HideInInspector]
    public float DetectionRange = 13f;
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
    public float LoseRange = 17f;

    [HideInInspector]
    public float MeleeAttackRange = 2.8f;
    [HideInInspector]
    public float MeleeAttackInterval = 1.8f;
    [HideInInspector]
    public float MeleeAttackRadius = 1.9f;
    [HideInInspector]
    public float MeleeDamage = 12f;
    [HideInInspector]
    public float MeleeVisualDuration = 0.2f;

    [HideInInspector]
    public float MinimumRangedDistance = 3.4f;
    [HideInInspector]
    public float RangedAttackRange = 7.6f;
    [HideInInspector]
    public float RangedAttackInterval = 2.4f;
    [HideInInspector]
    public float BiteStrikeDuration = 0.42f;
    [HideInInspector]
    public float BiteHitboxWidth = 0.42f;
    [HideInInspector]
    public float BiteHitboxHeight = 0.42f;
    [HideInInspector]
    public float BiteDamage = 15f;

    private NavMeshAgent _navMeshAgent;
    private EnemyPatrolRouteFollower _patrolRouteFollower;
    private EnemyPatrolAwarenessController _patrolAwareness;
    private PlayerHealthController _playerHealthController;
    private ICombatDamageReceiver _combatDamageReceiver;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;
    private float _waitTimer;
    private float _meleeAttackTimer;
    private float _rangedAttackTimer;
    private float _meleeVisualTimer;
    private float _biteStrikeTimer;
    private float _biteTotalDamage;
    private float _directDamageForcedChaseEndTime = -1f;
    private Vector3 _directDamageFallbackPosition;
    private bool _isBiteStriking;
    private bool _hasAppliedBiteDamage;
    private bool _hasDirectDamageFallbackPosition;
    private bool _hasWarnedMissingFixedRoute;
    private AncientStranderBiteHitbox _biteHitbox;

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
        if (EnsureAgentReady())
        {
            _navMeshAgent.stoppingDistance = Mathf.Max(0.2f, MeleeAttackRange * 0.85f);
        }
        ApplyHealthConfig();
        EnsurePlayerReferences();
        InitializePatrolRoute();

        if (MinimumRangedDistance <= MeleeAttackRange + 0.5f)
        {
            MinimumRangedDistance = MeleeAttackRange + 1f;
        }

        EnsureLineRenderers();
        EnsureBiteHitbox();
        SetNextPatrolDestination();
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing AncientStranderConfig.", this);
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
        MeleeAttackRange = _config.MeleeAttackRange;
        MeleeAttackInterval = _config.MeleeAttackInterval;
        MeleeAttackRadius = _config.MeleeAttackRadius;
        MeleeDamage = _config.MeleeDamage;
        MeleeVisualDuration = _config.MeleeVisualDuration;
        MinimumRangedDistance = _config.MinimumRangedDistance;
        RangedAttackRange = _config.RangedAttackRange;
        RangedAttackInterval = _config.RangedAttackInterval;
        BiteStrikeDuration = _config.BiteStrikeDuration;
        BiteHitboxWidth = _config.BiteHitboxWidth;
        BiteHitboxHeight = _config.BiteHitboxHeight;
        BiteDamage = _config.BiteDamage;
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
            case EnemyState.MeleeAttack:
                MeleeAttackBehavior(distanceToPlayer);
                break;
            case EnemyState.RangedBiteAttack:
                RangedBiteAttackBehavior(distanceToPlayer);
                break;
        }

        UpdateMeleeVisual();
        UpdateBiteVisual();
        UpdateBiteHitbox();
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

        if (distanceToPlayer <= MeleeAttackRange)
        {
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            SetAgentStopped(true);
            return;
        }

        if (distanceToPlayer >= MinimumRangedDistance && distanceToPlayer <= RangedAttackRange)
        {
            CurrentState = EnemyState.RangedBiteAttack;
            _rangedAttackTimer = RangedAttackInterval;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    private void MeleeAttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer > MeleeAttackRange + 1f)
        {
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        LookAtPlayer();
        _meleeAttackTimer += Time.deltaTime;
        if (_meleeAttackTimer >= MeleeAttackInterval)
        {
            _meleeAttackTimer = 0f;
            PerformMeleeAttack();
        }
    }

    private void RangedBiteAttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            StopBiteStrike();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer <= MeleeAttackRange)
        {
            StopBiteStrike();
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            return;
        }

        if (distanceToPlayer < MinimumRangedDistance || distanceToPlayer > RangedAttackRange)
        {
            StopBiteStrike();
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        LookAtPlayer();

        if (_isBiteStriking)
        {
            _biteStrikeTimer += Time.deltaTime;
            if (_biteStrikeTimer >= BiteStrikeDuration)
            {
                StopBiteStrike();
            }
            return;
        }

        _rangedAttackTimer += Time.deltaTime;
        if (_rangedAttackTimer >= RangedAttackInterval)
        {
            BeginBiteStrike();
        }
    }

    private void PerformMeleeAttack()
    {
        _meleeVisualTimer = MeleeVisualDuration;
        float totalDamage = 0f;
        Vector3 center = MeleeOrigin != null ? MeleeOrigin.position : transform.position + transform.forward * 1.2f;
        Collider[] hits = Physics.OverlapSphere(center, MeleeAttackRadius);
        foreach (Collider hit in hits)
        {
            if (!CombatDamageUtility.TryGetDamageReceiver(hit, out ICombatDamageReceiver damageReceiver))
            {
                continue;
            }

            totalDamage += CombatDamageUtility.ApplyDamageTo(
                damageReceiver,
                MeleeDamage,
                hit.ClosestPoint(center),
                hit.transform.position - transform.position,
                gameObject);
        }

        EnemySkillDamageLogger.LogSkillDamage(this, "Fishbone Sweep", totalDamage);
    }

    private void BeginBiteStrike()
    {
        _rangedAttackTimer = 0f;
        _biteStrikeTimer = 0f;
        _biteTotalDamage = 0f;
        _isBiteStriking = true;
        _hasAppliedBiteDamage = false;
        SetBiteHitboxEnabled(true);
    }

    public void NotifyBiteHit(ICombatDamageReceiver damageReceiver)
    {
        if (!_isBiteStriking || _hasAppliedBiteDamage || damageReceiver == null)
        {
            return;
        }

        _hasAppliedBiteDamage = true;
        _biteTotalDamage += CombatDamageUtility.ApplyDamageTo(
            damageReceiver,
            BiteDamage,
            damageReceiver.DamageRootTransform != null ? damageReceiver.DamageRootTransform.position : transform.position,
            damageReceiver.DamageRootTransform != null ? damageReceiver.DamageRootTransform.position - transform.position : transform.forward,
            gameObject);
    }

    private void StopBiteStrike()
    {
        bool wasBiteStriking = _isBiteStriking;
        float totalDamage = _biteTotalDamage;

        _isBiteStriking = false;
        _biteStrikeTimer = 0f;
        _biteTotalDamage = 0f;
        _hasAppliedBiteDamage = false;
        SetBiteHitboxEnabled(false);

        if (wasBiteStriking)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Fishbone Bite", totalDamage);
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

        StopBiteStrike();
        _waitTimer = 0f;
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        FacePlayerImmediately();

        CurrentState = EnemyState.Chase;
        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    private void EnsureLineRenderers()
    {
        if (MeleeSwingRenderer == null)
        {
            MeleeSwingRenderer = CreateLineRenderer("FishboneMeleeSwing", new Color(0.95f, 0.92f, 0.78f, 0.92f), new Color(0.78f, 0.76f, 0.6f, 0.38f), 0.14f, 0.04f);
        }

        if (FishboneBiteRenderer == null)
        {
            FishboneBiteRenderer = CreateLineRenderer("FishboneBite", new Color(0.92f, 0.96f, 1f, 0.95f), new Color(0.75f, 0.82f, 0.92f, 0.42f), 0.1f, 0.035f);
        }
    }

    private LineRenderer CreateLineRenderer(string objectName, Color startColor, Color endColor, float startWidth, float endWidth)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        SkillEffectLayerUtility.ApplyToRoot(lineObject);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lineRenderer.material = new Material(lineShader);
        }

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        return lineRenderer;
    }

    private void EnsureBiteHitbox()
    {
        if (_biteHitbox != null)
        {
            return;
        }

        GameObject hitboxObject = new GameObject("FishboneBiteHitbox");
        hitboxObject.transform.SetParent(transform, false);
        SkillEffectLayerUtility.ApplyToRoot(hitboxObject);
        _biteHitbox = hitboxObject.AddComponent<AncientStranderBiteHitbox>();
        _biteHitbox.Initialize(this, BiteHitboxWidth, BiteHitboxHeight);
        SetBiteHitboxEnabled(false);
    }

    private void SetBiteHitboxEnabled(bool isEnabled)
    {
        if (_biteHitbox == null)
        {
            return;
        }

        _biteHitbox.gameObject.SetActive(isEnabled);
    }

    private void UpdateBiteHitbox()
    {
        if (_biteHitbox == null || PlayerTransform == null)
        {
            return;
        }

        if (!_isBiteStriking)
        {
            _biteHitbox.gameObject.SetActive(false);
            return;
        }

        _biteHitbox.gameObject.SetActive(true);
        Vector3 origin = BiteOrigin != null ? BiteOrigin.position : transform.position + Vector3.up * 1.1f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.9f;
        _biteHitbox.UpdateHitboxTransform(origin, target);
    }

    private void UpdateMeleeVisual()
    {
        if (MeleeSwingRenderer == null)
        {
            return;
        }

        if (_meleeVisualTimer <= 0f)
        {
            MeleeSwingRenderer.enabled = false;
            return;
        }

        _meleeVisualTimer -= Time.deltaTime;
        MeleeSwingRenderer.enabled = true;
        float normalizedTime = 1f - (_meleeVisualTimer / Mathf.Max(0.01f, MeleeVisualDuration));
        float sweepAngle = Mathf.Lerp(-55f, 55f, normalizedTime);
        Vector3 origin = MeleeOrigin != null ? MeleeOrigin.position : transform.position + Vector3.up * 1.05f;
        Vector3 direction = Quaternion.Euler(0f, sweepAngle, 0f) * transform.forward;
        Vector3 endPoint = origin + direction * MeleeAttackRadius;

        MeleeSwingRenderer.SetPosition(0, origin);
        MeleeSwingRenderer.SetPosition(1, endPoint);
    }

    private void UpdateBiteVisual()
    {
        if (FishboneBiteRenderer == null)
        {
            return;
        }

        if (!_isBiteStriking || PlayerTransform == null)
        {
            FishboneBiteRenderer.enabled = false;
            return;
        }

        FishboneBiteRenderer.enabled = true;
        Vector3 origin = BiteOrigin != null ? BiteOrigin.position : transform.position + Vector3.up * 1.1f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.9f;
        FishboneBiteRenderer.SetPosition(0, origin);
        FishboneBiteRenderer.SetPosition(1, target);
        float pulse = 0.5f + Mathf.Sin(Time.time * 20f) * 0.5f;
        FishboneBiteRenderer.startWidth = 0.09f + pulse * 0.04f;
        FishboneBiteRenderer.endWidth = 0.035f + pulse * 0.015f;
    }

    private void LookAtPlayer()
    {
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPosition);
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

        if (PlayerTransform != null && AssignCombatTarget(PlayerTransform))
        {
            return true;
        }

        if (PlayerTransform != null &&
            PlayerTransform.CompareTag("Player") &&
            _playerHealthController == null)
        {
            _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
            if (_playerHealthController == null)
            {
                _playerHealthController = PlayerTransform.gameObject.AddComponent<PlayerHealthController>();
            }

            _combatDamageReceiver = _playerHealthController;
            return _combatDamageReceiver != null;
        }

        if (PlayerHealthController.Instance != null)
        {
            return AssignCombatTarget(PlayerHealthController.Instance.transform);
        }

        return false;
    }

    private bool AssignCombatTarget(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        PlayerTransform = target;
        _playerHealthController = target.GetComponent<PlayerHealthController>();
        if (_playerHealthController == null)
        {
            _playerHealthController = target.GetComponentInParent<PlayerHealthController>();
        }

        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver))
        {
            _combatDamageReceiver = receiver;
            if (receiver.DamageRootTransform != null)
            {
                PlayerTransform = receiver.DamageRootTransform;
            }

            return true;
        }

        _combatDamageReceiver = _playerHealthController;
        return _combatDamageReceiver != null;
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
        Gizmos.color = new Color(0.85f, 0.82f, 0.65f, 1f);
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRange);
        Gizmos.color = new Color(0.62f, 0.75f, 0.88f, 1f);
        Gizmos.DrawWireSphere(transform.position, MinimumRangedDistance);
        Gizmos.color = new Color(0.32f, 0.5f, 0.7f, 1f);
        Gizmos.DrawWireSphere(transform.position, RangedAttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }
}

/// <summary>
/// 古代搁浅者远程鱼骨撕咬命中盒。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AncientStranderBiteHitbox : MonoBehaviour
{
    private AncientStranderBehaviorController _owner;
    private BoxCollider _boxCollider;
    private float _width;
    private float _height;

    public void Initialize(AncientStranderBehaviorController owner, float width, float height)
    {
        _owner = owner;
        _width = Mathf.Max(0.1f, width);
        _height = Mathf.Max(0.1f, height);

        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
    }

    public void UpdateHitboxTransform(Vector3 origin, Vector3 target)
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
        }

        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        transform.position = origin + delta * 0.5f;
        transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        _boxCollider.size = new Vector3(_width, _height, distance);
        _boxCollider.center = Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyOwner(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyOwner(other);
    }

    private void NotifyOwner(Collider other)
    {
        if (_owner == null)
        {
            return;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(other, out ICombatDamageReceiver damageReceiver))
        {
            _owner.NotifyBiteHit(damageReceiver);
        }
    }
}
