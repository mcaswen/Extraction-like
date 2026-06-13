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

    /// <summary>
    /// 现代搁浅者的主行为状态。
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
    private ModernStranderConfig _config;

    [Header("References")]
    /// <summary>
    /// 当前战斗目标，通常是玩家，也可能是 Agent。
    /// </summary>
    public Transform PlayerTransform;

    /// <summary>
    /// 触手攻击的起点。
    /// </summary>
    public Transform TentacleOrigin;

    /// <summary>
    /// 触手攻击的线渲染器。
    /// </summary>
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
    public float DirectDamageCounterAttackRange = 10.5f;
    [HideInInspector]
    public float AttackInterval = 5f;
    [HideInInspector]
    public float TentacleLatchDuration = 1.1f;
    [HideInInspector]
    public float TentacleHitboxWidth = 0.55f;
    [HideInInspector]
    public float TentacleHitboxHeight = 0.55f;
    [HideInInspector]
    public float LatchPullStrength = 0.20f;
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
    private EnemyAnimatorDriver _animatorDriver;
    private EnemyHealthController _healthController;
    private Collider _bodyCollider;
    private Collider _ignoredPlayerBodyCollider;
    private EnemyPatrolRouteFollower _patrolRouteFollower;
    private EnemyPatrolAwarenessController _patrolAwareness;
    private Gameplay.Agent.Core.AgentHealthController _agentHealthController;
    private PlayerMovementController _playerMovementController;
    private IExternalMovementReceiver _externalMovementReceiver;
    private ICombatDamageReceiver _combatDamageReceiver;
    private Vector3 _startingPosition;
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;
    private float _waitTimer;
    private float _attackTimer;
    private float _latchTimer;
    private float _tentacleTotalDamage;
    private float _directDamageForcedChaseEndTime = -1f;
    private float _directDamageCounterCooldownEndTime = -1f;
    private float _activeTentacleLatchRange;
    private Vector3 _directDamageFallbackPosition;
    private bool _isTentacleStriking;
    private bool _isTentacleLatched;
    private bool _isDirectDamageCounterStrike;
    private bool _hasAppliedInitialLatchDamage;
    private bool _hasAddedTentacleCorrosionDamage;
    private bool _hasDirectDamageFallbackPosition;
    private bool _hasWarnedMissingFixedRoute;
    private ModernStranderTentacleHitbox _tentacleHitbox;

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
        _healthController = GetComponent<EnemyHealthController>();
        _bodyCollider = GetComponent<Collider>();
        EnemyAwarenessRuntimeInstaller.EnsureAwarenessComponents(gameObject);
        _patrolAwareness = GetComponent<EnemyPatrolAwarenessController>();
        _patrolAwareness?.ConfigurePreset(_awarenessPreset);
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        ApplyHealthConfig();
        EnsureAgentReady();
        _animatorDriver = new EnemyAnimatorDriver(this);
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
        DirectDamageCounterAttackRange = _config.DirectDamageCounterAttackRange;
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
        UpdateAnimatorSpeed();
    }

    private void LateUpdate()
    {
        _animatorDriver?.LateUpdate();
    }

    private void OnDisable()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;

        // 魔法封印可能在触手攻击中途禁用技能，因此这里立即停止命中盒和吸附状态。
        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _isDirectDamageCounterStrike = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _activeTentacleLatchRange = 0f;
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

        if (distanceToPlayer > GetCurrentAttackHoldRange())
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
            // 吸附阶段持续造成腐蚀伤害，并把玩家向触手起点方向拉拽。
            _latchTimer += Time.deltaTime;

            if (_combatDamageReceiver != null && _agentHealthController == null)
            {
                _tentacleTotalDamage += CombatDamageUtility.ApplyDamageTo(
                    _combatDamageReceiver,
                    CorrosionDamagePerSecond * Time.deltaTime,
                    PlayerTransform != null ? PlayerTransform.position : transform.position,
                    PlayerTransform != null ? PlayerTransform.position - transform.position : transform.forward,
                    gameObject);
            }

            if (_agentHealthController != null)
            {
                _agentHealthController.ApplyCorrosion(
                    CorrosionDamagePerSecond,
                    CorrosionDuration,
                    CorrosionTickInterval);
            }

            if (_externalMovementReceiver != null)
            {
                Vector3 pullTarget = TentacleOrigin != null ? TentacleOrigin.position : transform.position;
                _externalMovementReceiver.ApplyExternalPull(pullTarget, LatchPullStrength);
            }

            if (_latchTimer >= TentacleLatchDuration)
            {
                StopTentacleAttack();
            }

            return;
        }

        if (_isTentacleStriking)
        {
            // 触手已伸出但尚未命中时，只维持短暂命中窗口。
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
        BeginTentacleStrike(AttackRange, false);
    }

    private void BeginTentacleStrike(float latchRange, bool isDirectDamageCounterStrike)
    {
        // 每次触手攻击从“伸出命中盒”开始，首次命中后才切换为吸附。
        _animatorDriver?.TriggerAttack();
        _attackTimer = 0f;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _activeTentacleLatchRange = Mathf.Max(AttackRange, latchRange);
        _isTentacleStriking = true;
        _isTentacleLatched = false;
        _isDirectDamageCounterStrike = isDirectDamageCounterStrike;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(true);
        TryLatchCurrentCombatTarget();
    }

    private void StopTentacleAttack()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        bool hadLatchedPlayer = _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;
        Vector3 puddlePosition = PlayerTransform != null ? PlayerTransform.position : transform.position;

        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _isDirectDamageCounterStrike = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _activeTentacleLatchRange = 0f;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(false);

        if (hadLatchedPlayer)
        {
            // 只有成功吸附过目标才在目标脚下留下腐蚀黏液池。
            SpawnCorrosivePuddle(puddlePosition);
        }

        if (hadActiveTentacle)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Corrosive Tentacle", totalDamage);
        }
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

        if (!IsAliveAfterDamageReaction())
        {
            return;
        }

        AssignCombatTarget(context.Attacker);
        BeginDirectDamageForcedChase(context);

        _waitTimer = 0f;
        _patrolAwareness?.ResetAwareness();
        GetComponent<EnemyLookController>()?.LookAtPlayer(PlayerTransform);
        FacePlayerImmediately();

        if (TryStartDirectDamageCounterAttack(context))
        {
            return;
        }

        if (_isTentacleStriking || _isTentacleLatched)
        {
            CurrentState = EnemyState.Attack;
            SetAgentStopped(true);
            return;
        }

        CurrentState = EnemyState.Chase;
        SetAgentStopped(false);
        TrySetChaseDestination();
    }

    /// <summary>
    /// 触手命中盒命中可受击目标时回调现代搁浅者。
    /// </summary>
    /// <param name="damageReceiver">命中的战斗伤害接收者。</param>
    /// <param name="playerMovementController">命中玩家时的移动控制器。</param>
    public void NotifyTentacleHit(ICombatDamageReceiver damageReceiver, PlayerMovementController playerMovementController)
    {
        if (!_isTentacleStriking || damageReceiver == null)
        {
            return;
        }

        _combatDamageReceiver = damageReceiver;
        _agentHealthController = damageReceiver as Gameplay.Agent.Core.AgentHealthController;
        if (_agentHealthController == null &&
            damageReceiver.DamageRootTransform != null)
        {
            PlayerTargetResolver.TryGetAgentHealth(damageReceiver.DamageRootTransform, out _agentHealthController);
        }
        PlayerTransform = damageReceiver.DamageRootTransform != null ? damageReceiver.DamageRootTransform : PlayerTransform;
        _playerMovementController = playerMovementController;
        _externalMovementReceiver = ResolveExternalMovementReceiver(damageReceiver, playerMovementController);
        _isTentacleLatched = true;

        if (_hasAppliedInitialLatchDamage)
        {
            return;
        }

        _hasAppliedInitialLatchDamage = true;
        // 首次命中只结算一次即时伤害，后续持续伤害由吸附阶段处理。
        _tentacleTotalDamage += CombatDamageUtility.ApplyDamageTo(
            _combatDamageReceiver,
            InitialContactDamage,
            PlayerTransform != null ? PlayerTransform.position : transform.position,
            PlayerTransform != null ? PlayerTransform.position - transform.position : transform.forward,
            gameObject);

        if (_agentHealthController != null)
        {
            _agentHealthController.ApplyCorrosion(
                CorrosionDamagePerSecond,
                CorrosionDuration,
                CorrosionTickInterval);
        }

        if (!_hasAddedTentacleCorrosionDamage)
        {
            _hasAddedTentacleCorrosionDamage = true;
            _tentacleTotalDamage += CorrosionDamagePerSecond * CorrosionDuration;
        }
    }

    private void TryLatchCurrentCombatTarget()
    {
        if (!_isTentacleStriking ||
            _combatDamageReceiver == null ||
            PlayerTransform == null ||
            Vector3.Distance(transform.position, PlayerTransform.position) > GetCurrentTentacleLatchRange() + 0.75f)
        {
            return;
        }

        NotifyTentacleHit(_combatDamageReceiver, _playerMovementController);
    }

    private bool TryStartDirectDamageCounterAttack(EnemyDamageContext context)
    {
        if (PlayerTransform == null ||
            _combatDamageReceiver == null ||
            !context.IsDirectPlayerDamage ||
            _isTentacleStriking ||
            _isTentacleLatched ||
            Time.time < _directDamageCounterCooldownEndTime)
        {
            return false;
        }

        float counterRange = Mathf.Max(AttackRange, DirectDamageCounterAttackRange);
        if (Vector3.Distance(transform.position, PlayerTransform.position) > counterRange ||
            !EnemyVisionUtility.HasLineOfSight(transform, PlayerTransform, LineOfSightBlockMask, EyeHeight, TargetHeight))
        {
            return false;
        }

        CurrentState = EnemyState.Attack;
        SetAgentStopped(true);
        FacePlayerImmediately();
        _directDamageCounterCooldownEndTime = Time.time + Mathf.Max(0.1f, AttackInterval);
        BeginTentacleStrike(counterRange, true);
        return true;
    }

    private bool IsAliveAfterDamageReaction()
    {
        if (_healthController == null)
        {
            _healthController = GetComponent<EnemyHealthController>();
        }

        return _healthController == null || _healthController.IsAlive;
    }

    private float GetCurrentAttackHoldRange()
    {
        return _isDirectDamageCounterStrike
            ? Mathf.Max(AttackRange, _activeTentacleLatchRange)
            : AttackRange;
    }

    private float GetCurrentTentacleLatchRange()
    {
        return _activeTentacleLatchRange > 0f
            ? _activeTentacleLatchRange
            : AttackRange;
    }

    private IExternalMovementReceiver ResolveExternalMovementReceiver(
        ICombatDamageReceiver damageReceiver,
        PlayerMovementController playerMovementController)
    {
        if (damageReceiver != null && damageReceiver.DamageRootTransform != null)
        {
            IExternalMovementReceiver receiver = damageReceiver.DamageRootTransform.GetComponent<IExternalMovementReceiver>();
            if (receiver != null)
            {
                return receiver;
            }

            receiver = damageReceiver.DamageRootTransform.GetComponentInParent<IExternalMovementReceiver>();
            if (receiver != null)
            {
                return receiver;
            }
        }

        return _externalMovementReceiver ?? playerMovementController;
    }

    private void UpdateAnimatorSpeed()
    {
        _animatorDriver?.SetSpeedFromAgent(_navMeshAgent);
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

        Vector3 desiredChasePosition = ResolveChaseStandOffPosition();
        if (TrySetSampledDestination(desiredChasePosition, DirectDamageDestinationSampleRadius))
        {
            return true;
        }

        if (_hasDirectDamageFallbackPosition &&
            TrySetSampledDestination(_directDamageFallbackPosition, DirectDamageDestinationSampleRadius))
        {
            return true;
        }

        return TrySetDestination(desiredChasePosition);
    }

    private Vector3 ResolveChaseStandOffPosition()
    {
        if (PlayerTransform == null)
        {
            return transform.position;
        }

        Vector3 fromPlayer = transform.position - PlayerTransform.position;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude <= 0.0001f)
        {
            fromPlayer = -PlayerTransform.forward;
            fromPlayer.y = 0f;
        }

        if (fromPlayer.sqrMagnitude <= 0.0001f)
        {
            fromPlayer = -transform.forward;
            fromPlayer.y = 0f;
        }

        float standOffDistance = Mathf.Max(0.4f, AttackRange * 0.8f);
        return PlayerTransform.position + fromPlayer.normalized * standOffDistance;
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
        PlayerTargetResolver.TryGetAgentHealth(target, out _agentHealthController);

        _playerMovementController = target.GetComponent<PlayerMovementController>();
        if (_playerMovementController == null)
        {
            _playerMovementController = target.GetComponentInParent<PlayerMovementController>();
        }

        IgnorePlayerBodyCollision(target);

        _externalMovementReceiver = target.GetComponent<IExternalMovementReceiver>();
        if (_externalMovementReceiver == null)
        {
            _externalMovementReceiver = target.GetComponentInParent<IExternalMovementReceiver>();
        }

        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver))
        {
            _combatDamageReceiver = receiver;
            if (receiver.DamageRootTransform != null)
            {
                PlayerTransform = receiver.DamageRootTransform;
                _externalMovementReceiver = ResolveExternalMovementReceiver(receiver, _playerMovementController);
            }

            return true;
        }

        _combatDamageReceiver = null;
        return false;
    }

    private void IgnorePlayerBodyCollision(Transform target)
    {
        if (_bodyCollider == null)
        {
            _bodyCollider = GetComponent<Collider>();
        }

        if (_bodyCollider == null || target == null)
        {
            return;
        }

        Collider playerBodyCollider = target.GetComponent<Collider>();
        if (playerBodyCollider == null)
        {
            playerBodyCollider = target.GetComponentInParent<Collider>();
        }

        if (playerBodyCollider == null || playerBodyCollider == _ignoredPlayerBodyCollider)
        {
            return;
        }

        if (_ignoredPlayerBodyCollider != null)
        {
            Physics.IgnoreCollision(_bodyCollider, _ignoredPlayerBodyCollider, false);
        }

        Physics.IgnoreCollision(_bodyCollider, playerBodyCollider, true);
        _ignoredPlayerBodyCollider = playerBodyCollider;
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
