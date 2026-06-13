using Gameplay.SkillEffect;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 潮汐畸变体白模行为。
/// 近距离使用水母触须电击玩家，远距离喷射高压水柱击退玩家。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyLookController))]
[RequireComponent(typeof(EnemySuspicionSensor))]
[RequireComponent(typeof(EnemyPatrolAwarenessController))]
public class TidalAberrationBehaviorController : MonoBehaviour, IEnemyVisionSource, IEnemyDirectDamageReceiver
{
    private const float DirectDamageForcedChaseDuration = 4f;
    private const float DirectDamageDestinationSampleRadius = 4f;
    private const float WaterJetHitRadius = 0.65f;

    /// <summary>
    /// 潮汐畸变体的主行为状态。
    /// </summary>
    public enum EnemyState
    {
        Patrol,
        Chase,
        MeleeAttack,
        RangedAttack
    }

    /// <summary>
    /// 当前主行为状态。
    /// </summary>
    public EnemyState CurrentState;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private TidalAberrationConfig _config;

    [Header("References")]
    /// <summary>
    /// 当前战斗目标，通常是玩家，也可能是 Agent。
    /// </summary>
    public Transform PlayerTransform;

    /// <summary>
    /// 近战电击触须的起点。
    /// </summary>
    public Transform MeleeOrigin;

    /// <summary>
    /// 远程水柱的起点。
    /// </summary>
    public Transform RangedOrigin;

    /// <summary>
    /// 近战电击触须的线渲染器。
    /// </summary>
    public LineRenderer ElectricTentacleRenderer;

    /// <summary>
    /// 远程高压水柱的线渲染器。
    /// </summary>
    public LineRenderer WaterJetRenderer;

    [HideInInspector]
    public float PatrolRadius = 8f;
    [HideInInspector]
    public float PatrolWaitTime = 1.5f;

    [HideInInspector]
    public float DetectionRange = 14f;
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
    public float LoseRange = 18f;

    [HideInInspector]
    public float MeleeAttackRange = 3f;
    [HideInInspector]
    public float MeleeAttackInterval = 2.2f;
    [HideInInspector]
    public float MeleeLatchDuration = 0.8f;
    [HideInInspector]
    public float MeleeContactDamage = 10f;
    [HideInInspector]
    public float SilenceDuration = 1.5f;
    [HideInInspector]
    public float ElectricTickDamagePerSecond = 4f;
    [HideInInspector]
    public float ElectricTickInterval = 0.25f;

    [HideInInspector]
    public float MinimumRangedDistance = 4.5f;
    [HideInInspector]
    public float RangedAttackRange = 9f;
    [HideInInspector]
    public float RangedAttackInterval = 8f;
    [HideInInspector]
    public float WaterJetDuration = 0.18f;
    [HideInInspector]
    public float WaterJetDamage = 14f;
    [HideInInspector]
    public float WaterJetKnockbackStrength = 5.2f;
    [HideInInspector]
    public float KnockbackMoveSpeedMultiplier = 0.5f;
    [HideInInspector]
    public float KnockbackSlowDuration = 1f;
    [HideInInspector]
    public float WaterJetMaxDistance = 10f;

    private NavMeshAgent _navMeshAgent;
    private EnemyAnimatorDriver _animatorDriver;
    private EnemyPatrolRouteFollower _patrolRouteFollower;
    private EnemyPatrolAwarenessController _patrolAwareness;
    private PlayerMovementController _playerMovementController;
    private IExternalMovementReceiver _externalMovementReceiver;
    private PlayerShootingController _playerShootingController;
    private ICombatDamageReceiver _combatDamageReceiver;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;
    private float _waitTimer;
    private float _meleeAttackTimer;
    private float _rangedAttackTimer;
    private float _nextRangedAttackTime;
    private float _meleeVisualTimer;
    private float _rangedVisualTimer;
    private float _electricTickTimer;
    private float _meleeTotalDamage;
    private float _directDamageForcedChaseEndTime = -1f;
    private Vector3 _directDamageFallbackPosition;
    private bool _isMeleeLatched;
    private bool _isRangedCasting;
    private bool _hasDirectDamageFallbackPosition;
    private bool _hasWarnedMissingFixedRoute;
    private bool _openingWaterJetPending;

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
        float effectiveMeleeRange = GetEffectiveMeleeRange();
        if (MinimumRangedDistance <= effectiveMeleeRange + 0.75f)
        {
            MinimumRangedDistance = effectiveMeleeRange + 1.2f;
        }
        if (EnsureAgentReady())
        {
            _navMeshAgent.stoppingDistance = Mathf.Max(0.2f, effectiveMeleeRange * 0.9f);
        }
        ApplyHealthConfig();
        EnsurePlayerReferences();
        InitializePatrolRoute();

        EnsureLineRenderers();
        SetNextPatrolDestination();
        QueueOpeningWaterJetIfTargetInRange();
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing TidalAberrationConfig.", this);
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
        MeleeLatchDuration = _config.MeleeLatchDuration;
        MeleeContactDamage = _config.MeleeContactDamage;
        SilenceDuration = _config.SilenceDuration;
        ElectricTickDamagePerSecond = _config.ElectricTickDamagePerSecond;
        ElectricTickInterval = _config.ElectricTickInterval;
        MinimumRangedDistance = _config.MinimumRangedDistance;
        RangedAttackRange = _config.RangedAttackRange;
        RangedAttackInterval = _config.RangedAttackInterval;
        WaterJetDuration = _config.WaterJetDuration;
        WaterJetDamage = _config.WaterJetDamage;
        WaterJetKnockbackStrength = _config.WaterJetKnockbackStrength;
        KnockbackMoveSpeedMultiplier = _config.KnockbackMoveSpeedMultiplier;
        KnockbackSlowDuration = _config.KnockbackSlowDuration;
        WaterJetMaxDistance = _config.WaterJetMaxDistance;
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
        if (TryFireOpeningWaterJet(distanceToPlayer))
        {
            UpdateAttackVisuals();
            UpdateAnimatorSpeed();
            return;
        }

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
            case EnemyState.RangedAttack:
                RangedAttackBehavior(distanceToPlayer);
                break;
        }

        UpdateAttackVisuals();
        UpdateAnimatorSpeed();
    }

    private void LateUpdate()
    {
        _animatorDriver?.LateUpdate();
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
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer <= effectiveMeleeRange)
        {
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            SetAgentStopped(true);
            return;
        }

        if (IsInRangedAttackWindow(distanceToPlayer))
        {
            CurrentState = EnemyState.RangedAttack;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    private void MeleeAttackBehavior(float distanceToPlayer)
    {
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            StopMeleeAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer > RangedAttackRange)
        {
            StopMeleeAttack();
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false;
            return;
        }

        if (distanceToPlayer > effectiveMeleeRange)
        {
            StopMeleeAttack();
            CurrentState = EnemyState.RangedAttack;
            return;
        }

        LookAtPlayer();

        if (_isMeleeLatched)
        {
            // 电击吸附期间按 tick 造成持续伤害，帧率波动时用 while 追赶结算。
            _meleeVisualTimer += Time.deltaTime;
            _electricTickTimer += Time.deltaTime;
            float electricTickInterval = Mathf.Max(0.05f, ElectricTickInterval);
            while (_electricTickTimer >= electricTickInterval)
            {
                _electricTickTimer -= electricTickInterval;
                _meleeTotalDamage += CombatDamageUtility.ApplyDamageTo(
                    _combatDamageReceiver,
                    ElectricTickDamagePerSecond * electricTickInterval,
                    PlayerTransform != null ? PlayerTransform.position : transform.position,
                    PlayerTransform != null ? PlayerTransform.position - transform.position : transform.forward,
                    gameObject);
            }

            if (_meleeVisualTimer >= MeleeLatchDuration)
            {
                StopMeleeAttack();
            }
            return;
        }

        _meleeAttackTimer += Time.deltaTime;
        if (_meleeAttackTimer >= MeleeAttackInterval)
        {
            PerformMeleeAttack();
        }
    }

    private void RangedAttackBehavior(float distanceToPlayer)
    {
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange && !IsDirectDamageForcedChaseActive())
        {
            StopRangedAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            ReportPlayerLastSeen(DetectionRange, 0.78f, 3f, 1.6f);
            ResetPatrolDestination();
            return;
        }

        if (distanceToPlayer <= effectiveMeleeRange)
        {
            StopRangedAttack();
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            return;
        }

        if (!IsInRangedAttackWindow(distanceToPlayer))
        {
            StopRangedAttack();
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        LookAtPlayer();

        if (_isRangedCasting)
        {
            // 水柱视觉很短，持续结束后回到追击，由冷却计时决定下一次远程攻击。
            _rangedVisualTimer += Time.deltaTime;
            if (_rangedVisualTimer >= WaterJetDuration)
            {
                StopRangedAttack();
            }
            return;
        }

        _rangedAttackTimer += Time.deltaTime;
        if (Time.time >= _nextRangedAttackTime)
        {
            PerformRangedAttack();
        }
    }

    private void PerformMeleeAttack()
    {
        // 近战电击先造成一次接触伤害，再进入持续电击和沉默阶段。
        _animatorDriver?.TriggerAttack();
        _meleeAttackTimer = 0f;
        _meleeVisualTimer = 0f;
        _electricTickTimer = 0f;
        _meleeTotalDamage = 0f;
        _isMeleeLatched = true;

        _meleeTotalDamage += CombatDamageUtility.ApplyDamageTo(
            _combatDamageReceiver,
            MeleeContactDamage,
            PlayerTransform != null ? PlayerTransform.position : transform.position,
            PlayerTransform != null ? PlayerTransform.position - transform.position : transform.forward,
            gameObject);

        if (_playerShootingController != null)
        {
            _playerShootingController.ApplySilence(SilenceDuration);
        }
    }

    private void StopMeleeAttack()
    {
        bool wasMeleeLatched = _isMeleeLatched;
        float totalDamage = _meleeTotalDamage;

        _isMeleeLatched = false;
        _meleeVisualTimer = 0f;
        _electricTickTimer = 0f;
        _meleeTotalDamage = 0f;

        if (wasMeleeLatched)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Electric Tentacle", totalDamage);
        }
    }

    private void PerformRangedAttack()
    {
        // 远程水柱用射线命中，先裁到第一个有效战斗目标，再施加伤害、击退和短暂减速。
        _animatorDriver?.TriggerAttack();
        _rangedAttackTimer = 0f;
        _rangedVisualTimer = 0f;
        _nextRangedAttackTime = Time.time + Mathf.Max(0.05f, RangedAttackInterval);
        _isRangedCasting = true;

        Vector3 origin = RangedOrigin != null ? RangedOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 direction = (PlayerTransform.position + Vector3.up * 0.8f) - origin;
        direction.Normalize();

        if (TryGetWaterJetHit(origin, direction, out Vector3 hitPoint, out Collider hitCollider, out ICombatDamageReceiver damageReceiver))
        {
            float totalDamage = CombatDamageUtility.ApplyDamageTo(
                damageReceiver,
                WaterJetDamage,
                hitPoint,
                direction,
                gameObject);

            IExternalMovementReceiver hitMovementReceiver = ResolveExternalMovementReceiver(damageReceiver, hitCollider);
            if (hitMovementReceiver != null)
            {
                _externalMovementReceiver = hitMovementReceiver;
                hitMovementReceiver.ApplyExternalImpulse(direction, WaterJetKnockbackStrength);
                hitMovementReceiver.ApplyMoveSpeedDebuff(KnockbackMoveSpeedMultiplier, KnockbackSlowDuration);
            }

            EnemySkillDamageLogger.LogSkillDamage(this, "Water Jet", totalDamage);
            return;
        }

        EnemySkillDamageLogger.LogSkillDamage(this, "Water Jet", 0f);
    }

    private void QueueOpeningWaterJetIfTargetInRange()
    {
        if (PlayerTransform == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        if (!IsInRangedAttackWindow(distanceToPlayer))
        {
            return;
        }

        _openingWaterJetPending = true;
        _nextRangedAttackTime = 0f;
        CurrentState = EnemyState.RangedAttack;
        SetAgentStopped(true);
    }

    private bool TryFireOpeningWaterJet(float distanceToPlayer)
    {
        if (!_openingWaterJetPending)
        {
            return false;
        }

        _openingWaterJetPending = false;
        if (!IsInRangedAttackWindow(distanceToPlayer) || PlayerTransform == null)
        {
            return false;
        }

        StopMeleeAttack();
        CurrentState = EnemyState.RangedAttack;
        SetAgentStopped(true);
        LookAtPlayer();
        PerformRangedAttack();
        return true;
    }

    private bool IsInRangedAttackWindow(float distanceToPlayer)
    {
        return distanceToPlayer >= MinimumRangedDistance && distanceToPlayer <= RangedAttackRange;
    }

    private void StopRangedAttack()
    {
        _isRangedCasting = false;
        _rangedVisualTimer = 0f;
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

        StopMeleeAttack();
        StopRangedAttack();
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
        if (ElectricTentacleRenderer == null)
        {
            ElectricTentacleRenderer = CreateLineRenderer("ElectricTentacle", new Color(1f, 0.9f, 0.32f, 0.95f), new Color(1f, 0.64f, 0.08f, 0.55f), 0.09f, 0.04f);
        }

        if (WaterJetRenderer == null)
        {
            WaterJetRenderer = CreateLineRenderer("WaterJet", new Color(0.7f, 0.96f, 1f, 1f), new Color(0.12f, 0.56f, 1f, 0.85f), 0.34f, 0.18f);
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
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 4;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lineRenderer.material = new Material(lineShader);
        }

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        return lineRenderer;
    }

    private void UpdateAttackVisuals()
    {
        UpdateMeleeVisual();
        UpdateRangedVisual();
    }

    private void UpdateMeleeVisual()
    {
        if (ElectricTentacleRenderer == null)
        {
            return;
        }

        if (!_isMeleeLatched || PlayerTransform == null)
        {
            ElectricTentacleRenderer.enabled = false;
            return;
        }

        ElectricTentacleRenderer.enabled = true;
        float pulse = 0.5f + Mathf.Sin(Time.time * 24f) * 0.5f;
        ElectricTentacleRenderer.startWidth = 0.14f + pulse * 0.05f;
        ElectricTentacleRenderer.endWidth = 0.06f + pulse * 0.03f;
        ElectricTentacleRenderer.startColor = Color.Lerp(new Color(1f, 0.92f, 0.35f, 0.92f), Color.white, pulse * 0.55f);
        ElectricTentacleRenderer.endColor = new Color(1f, 0.62f, 0.08f, 0.65f);
        Vector3 origin = MeleeOrigin != null ? MeleeOrigin.position : transform.position + Vector3.up * 1.15f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.95f;
        ElectricTentacleRenderer.SetPosition(0, origin);
        ElectricTentacleRenderer.SetPosition(1, target);
    }

    private void UpdateRangedVisual()
    {
        if (WaterJetRenderer == null)
        {
            return;
        }

        if (!_isRangedCasting || PlayerTransform == null)
        {
            WaterJetRenderer.enabled = false;
            return;
        }

        WaterJetRenderer.enabled = true;
        float pulse = 0.5f + Mathf.Sin(Time.time * 32f) * 0.5f;
        WaterJetRenderer.startWidth = 0.34f + pulse * 0.12f;
        WaterJetRenderer.endWidth = 0.18f + pulse * 0.08f;
        WaterJetRenderer.startColor = Color.Lerp(new Color(0.7f, 0.96f, 1f, 1f), Color.white, pulse * 0.45f);
        WaterJetRenderer.endColor = new Color(0.12f, 0.56f, 1f, 0.88f);
        Vector3 origin = RangedOrigin != null ? RangedOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.85f;
        WaterJetRenderer.SetPosition(0, origin);
        WaterJetRenderer.SetPosition(1, target);
    }

    private bool TryGetWaterJetHit(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out Collider hitCollider, out ICombatDamageReceiver damageReceiver)
    {
        hitPoint = origin + direction * WaterJetMaxDistance;
        hitCollider = null;
        damageReceiver = null;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            WaterJetHitRadius,
            direction,
            WaterJetMaxDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool foundHit = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider) || hit.distance >= closestDistance)
            {
                continue;
            }

            if (!CombatDamageUtility.TryGetDamageReceiver(hit.collider, out ICombatDamageReceiver hitReceiver) ||
                !IsCurrentCombatTarget(hitReceiver, hit.collider))
            {
                continue;
            }

            hitPoint = hit.point;
            hitCollider = hit.collider;
            damageReceiver = hitReceiver;
            closestDistance = hit.distance;
            foundHit = true;
        }

        if (foundHit)
        {
            return true;
        }

        if (PlayerTransform == null || !CombatDamageUtility.TryGetDamageReceiver(PlayerTransform, out damageReceiver))
        {
            return false;
        }

        Transform damageRoot = damageReceiver.DamageRootTransform != null ? damageReceiver.DamageRootTransform : PlayerTransform;
        hitPoint = damageRoot.position + Vector3.up * 0.8f;
        hitCollider = damageRoot.GetComponentInChildren<Collider>();
        return true;
    }

    private bool IsCurrentCombatTarget(ICombatDamageReceiver damageReceiver, Collider hitCollider)
    {
        if (damageReceiver == null || PlayerTransform == null)
        {
            return false;
        }

        if (ReferenceEquals(damageReceiver, _combatDamageReceiver))
        {
            return true;
        }

        if (damageReceiver.DamageRootTransform != null && IsSameTransformHierarchy(damageReceiver.DamageRootTransform, PlayerTransform))
        {
            return true;
        }

        return hitCollider != null && IsSameTransformHierarchy(hitCollider.transform, PlayerTransform);
    }

    private static bool IsSameTransformHierarchy(Transform first, Transform second)
    {
        return first != null &&
               second != null &&
               (first == second || first.IsChildOf(second) || second.IsChildOf(first));
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        return hitCollider != null && hitCollider.transform.IsChildOf(transform);
    }

    private IExternalMovementReceiver ResolveExternalMovementReceiver(ICombatDamageReceiver damageReceiver, Collider hitCollider)
    {
        if (damageReceiver != null && damageReceiver.DamageRootTransform != null)
        {
            IExternalMovementReceiver rootMovement = damageReceiver.DamageRootTransform.GetComponent<IExternalMovementReceiver>();
            if (rootMovement != null)
            {
                return rootMovement;
            }

            rootMovement = damageReceiver.DamageRootTransform.GetComponentInParent<IExternalMovementReceiver>();
            if (rootMovement != null)
            {
                return rootMovement;
            }
        }

        if (hitCollider != null)
        {
            IExternalMovementReceiver hitMovement = hitCollider.GetComponentInParent<IExternalMovementReceiver>();
            if (hitMovement != null)
            {
                return hitMovement;
            }
        }

        return _externalMovementReceiver ?? _playerMovementController;
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
            PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out PlayerTransform);
        }

        if (PlayerTransform != null && AssignCombatTarget(PlayerTransform))
        {
            return true;
        }

        if (PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out Transform currentPlayer) &&
            AssignCombatTarget(currentPlayer))
        {
            return true;
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

        _playerMovementController = target.GetComponent<PlayerMovementController>();
        if (_playerMovementController == null)
        {
            _playerMovementController = target.GetComponentInParent<PlayerMovementController>();
        }

        _externalMovementReceiver = target.GetComponent<IExternalMovementReceiver>();
        if (_externalMovementReceiver == null)
        {
            _externalMovementReceiver = target.GetComponentInParent<IExternalMovementReceiver>();
        }

        _playerShootingController = target.GetComponent<PlayerShootingController>();
        if (_playerShootingController == null)
        {
            _playerShootingController = target.GetComponentInParent<PlayerShootingController>();
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

        _combatDamageReceiver = null;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        float effectiveMeleeRange = Mathf.Max(MeleeAttackRange, 4f);
        EnemyVisionUtility.DrawVisionGizmos(
            VisionTransform,
            PlayerTransform,
            DetectionRange,
            ViewAngle,
            EyeHeight,
            TargetHeight,
            Color.cyan);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, effectiveMeleeRange);
        Gizmos.color = new Color(0.15f, 0.95f, 0.95f, 1f);
        Gizmos.DrawWireSphere(transform.position, MinimumRangedDistance);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, RangedAttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }

    private float GetEffectiveMeleeRange()
    {
        return Mathf.Max(MeleeAttackRange, 4f);
    }
}
