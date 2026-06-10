using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 锚点守卫白模行为。
/// 玩家需按顺序摧毁符文弱点，否则守卫会激活并发射能量光束。
/// </summary>
public class AnchorSentinelBehaviorController : MonoBehaviour, IEnemyDeathLootRuleReceiver
{
    /// <summary>
    /// 锚点守卫的主行为状态。
    /// </summary>
    public enum SentinelState
    {
        Dormant,
        Locking,
        Firing,
        Cooldown,
        Disabled
    }

    /// <summary>
    /// 当前守卫状态。
    /// </summary>
    public SentinelState CurrentState = SentinelState.Dormant;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private AnchorSentinelConfig _config;

    [Header("References")]
    /// <summary>
    /// 当前玩家目标。
    /// </summary>
    public Transform PlayerTransform;

    /// <summary>
    /// 光束发射起点。
    /// </summary>
    public Transform EyeOrigin;

    /// <summary>
    /// 锁定阶段的预警光束。
    /// </summary>
    public LineRenderer LockBeamRenderer;

    /// <summary>
    /// 开火阶段的伤害光束。
    /// </summary>
    public LineRenderer FiringBeamRenderer;

    /// <summary>
    /// 符文弱点数组，顺序可作为谜题顺序。
    /// </summary>
    public AnchorSentinelRuneWeakpoint[] RuneWeakpoints;

    /// <summary>
    /// 启动时是否初始化符文弱点。
    /// </summary>
    [HideInInspector]
    public bool AutoInitializeRunes = true;

    /// <summary>
    /// 未配置符文时是否自动生成默认符文。
    /// </summary>
    [HideInInspector]
    public bool AutoSpawnDefaultRunes = true;

    /// <summary>
    /// 自动生成的默认符文数量。
    /// </summary>
    [HideInInspector]
    public int DefaultRuneCount = 3;

    /// <summary>
    /// 默认符文生成半径。
    /// </summary>
    [HideInInspector]
    public float DefaultRuneRadius = 1.8f;

    /// <summary>
    /// 默认符文生成高度。
    /// </summary>
    [HideInInspector]
    public float DefaultRuneHeight = 1.25f;

    /// <summary>
    /// 默认符文本地缩放。
    /// </summary>
    [HideInInspector]
    public Vector3 DefaultRuneScale = new Vector3(0.35f, 0.35f, 0.35f);

    /// <summary>
    /// 是否使用数组顺序作为符文谜题顺序。
    /// </summary>
    [HideInInspector]
    public bool UseArrayOrderAsPuzzleSequence = true;

    /// <summary>
    /// 错误命中符文时是否立即激活守卫。
    /// </summary>
    [HideInInspector]
    public bool WrongRuneImmediatelyActivates = true;

    /// <summary>
    /// 守卫检测玩家并保持激活的距离。
    /// </summary>
    [HideInInspector]
    public float DetectionRange = 16f;

    /// <summary>
    /// 锁定阶段持续时间。
    /// </summary>
    [HideInInspector]
    public float LockDuration = 1.1f;

    /// <summary>
    /// 光束开火持续时间。
    /// </summary>
    [HideInInspector]
    public float FiringDuration = 0.55f;

    /// <summary>
    /// 光束攻击冷却时间。
    /// </summary>
    [HideInInspector]
    public float CooldownDuration = 1.5f;

    /// <summary>
    /// 激活后自动恢复休眠的时间，0 表示不按时间恢复。
    /// </summary>
    [HideInInspector]
    public float ActiveRecoveryDuration = 0f;

    /// <summary>
    /// 光束每秒伤害。
    /// </summary>
    [HideInInspector]
    public float BeamDamagePerSecond = 22f;

    /// <summary>
    /// 光束伤害结算间隔。
    /// </summary>
    [HideInInspector]
    public float BeamTickInterval = 0.12f;

    [Header("Death Loot References")]
    /// <summary>
    /// 守卫解除后战利品容器生成点。
    /// </summary>
    public Transform DeathLootSpawnPoint;

    private PlayerHealthController _playerHealthController;
    private EnemyAnimatorDriver _animatorDriver;
    private ICombatDamageReceiver _combatDamageReceiver;
    private float _stateTimer;
    private float _beamTickTimer;
    private float _beamTotalDamage;
    private float _activeStateTimer;
    private int _expectedRuneIndex;
    private bool _hasDroppedLoot;
    private bool _isBeamFiring;
    private bool _canSpawnDeathLoot = true;

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        EnsurePlayerReferences();
        _animatorDriver = new EnemyAnimatorDriver(this);
        ApplyHealthConfig();

        EnsureRuneWeakpointsExist();
        EnsureBeamRenderers();
        InitializeRunesIfNeeded();
        UpdateBeamVisuals();
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing AnchorSentinelConfig.", this);
            enabled = false;
            return false;
        }

        AutoInitializeRunes = _config.AutoInitializeRunes;
        AutoSpawnDefaultRunes = _config.AutoSpawnDefaultRunes;
        DefaultRuneCount = _config.DefaultRuneCount;
        DefaultRuneRadius = _config.DefaultRuneRadius;
        DefaultRuneHeight = _config.DefaultRuneHeight;
        DefaultRuneScale = _config.DefaultRuneScale;
        UseArrayOrderAsPuzzleSequence = _config.UseArrayOrderAsPuzzleSequence;
        WrongRuneImmediatelyActivates = _config.WrongRuneImmediatelyActivates;
        DetectionRange = _config.DetectionRange;
        LockDuration = _config.LockDuration;
        FiringDuration = _config.FiringDuration;
        CooldownDuration = _config.CooldownDuration;
        ActiveRecoveryDuration = _config.ActiveRecoveryDuration;
        BeamDamagePerSecond = _config.BeamDamagePerSecond;
        BeamTickInterval = _config.BeamTickInterval;
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
        if (CurrentState == SentinelState.Disabled)
        {
            UpdateBeamVisuals();
            return;
        }

        if (!EnsurePlayerReferences())
        {
            UpdateBeamVisuals();
            return;
        }

        if (CurrentState != SentinelState.Dormant)
        {
            _activeStateTimer += Time.deltaTime;
            if (ActiveRecoveryDuration > 0f && _activeStateTimer >= ActiveRecoveryDuration)
            {
                ReturnToDormantState();
                UpdateBeamVisuals();
                return;
            }
        }

        if (CurrentState != SentinelState.Dormant)
        {
            Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
            transform.LookAt(lookPosition);
        }

        switch (CurrentState)
        {
            case SentinelState.Dormant:
                TickDormantState();
                break;
            case SentinelState.Locking:
                TickLockingState();
                break;
            case SentinelState.Firing:
                TickFiringState();
                break;
            case SentinelState.Cooldown:
                TickCooldownState();
                break;
        }

        UpdateBeamVisuals();
        _animatorDriver?.SetSpeed(0f);
    }

    private void LateUpdate()
    {
        _animatorDriver?.LateUpdate();
    }

    /// <summary>
    /// 接收符文弱点命中事件，并按顺序推进或触发守卫。
    /// </summary>
    /// <param name="runeWeakpoint">被命中的符文弱点。</param>
    public void NotifyRuneHit(AnchorSentinelRuneWeakpoint runeWeakpoint)
    {
        if (CurrentState == SentinelState.Disabled || runeWeakpoint == null)
        {
            return;
        }

        if (CurrentState != SentinelState.Dormant)
        {
            return;
        }

        if (runeWeakpoint.RuneOrderIndex == _expectedRuneIndex)
        {
            // 命中正确符文后推进期待索引，所有符文解开时守卫直接失效。
            runeWeakpoint.MarkSolved();
            _expectedRuneIndex++;
            if (_expectedRuneIndex >= RuneWeakpoints.Length)
            {
                DisableSentinel();
            }
            return;
        }

        runeWeakpoint.MarkFailed();
        if (WrongRuneImmediatelyActivates)
        {
            ActivateSentinel();
        }
    }

    private void TickDormantState()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        if (!WrongRuneImmediatelyActivates && distanceToPlayer <= DetectionRange)
        {
            ActivateSentinel();
        }
    }

    private void TickLockingState()
    {
        if (IsPlayerOutOfRange())
        {
            ReturnToDormantState();
            return;
        }

        _stateTimer += Time.deltaTime;
        if (_stateTimer >= LockDuration)
        {
            // 锁定结束后进入真正伤害阶段，并重置本次光束的 tick 和日志累计。
            CurrentState = SentinelState.Firing;
            _stateTimer = 0f;
            _beamTickTimer = 0f;
            _beamTotalDamage = 0f;
            _isBeamFiring = true;
            _animatorDriver?.TriggerAttack();
        }
    }

    private void TickFiringState()
    {
        if (IsPlayerOutOfRange())
        {
            ReturnToDormantState();
            return;
        }

        _stateTimer += Time.deltaTime;
        _beamTickTimer += Time.deltaTime;

        // 使用 while 追赶 tick，避免帧率波动时漏掉光束持续伤害。
        while (_beamTickTimer >= BeamTickInterval)
        {
            _beamTickTimer -= BeamTickInterval;
            Vector3 origin = EyeOrigin != null ? EyeOrigin.position : transform.position + Vector3.up * 1.8f;
            Vector3 hitPoint = PlayerTransform != null ? PlayerTransform.position + Vector3.up * 0.9f : origin;
            _beamTotalDamage += CombatDamageUtility.ApplyDamageTo(
                _combatDamageReceiver,
                BeamDamagePerSecond * BeamTickInterval,
                hitPoint,
                hitPoint - origin,
                gameObject);
        }

        if (_stateTimer >= FiringDuration)
        {
            FinishBeamAttack();
            CurrentState = SentinelState.Cooldown;
            _stateTimer = FiringDuration;
        }
    }

    private void TickCooldownState()
    {
        if (IsPlayerOutOfRange())
        {
            ReturnToDormantState();
            return;
        }

        _stateTimer += Time.deltaTime;
        if (_stateTimer >= Mathf.Max(FiringDuration, CooldownDuration))
        {
            // CooldownDuration 表示两次光束开始时间的间隔，因此从 FiringDuration 后继续补足剩余冷却。
            CurrentState = SentinelState.Locking;
            _stateTimer = LockDuration;
        }
    }

    private void ActivateSentinel()
    {
        if (CurrentState == SentinelState.Disabled)
        {
            return;
        }

        CurrentState = SentinelState.Locking;
        _stateTimer = LockDuration;
        _activeStateTimer = 0f;
        foreach (AnchorSentinelRuneWeakpoint rune in RuneWeakpoints)
        {
            if (rune != null)
            {
                rune.MarkAlert();
            }
        }
    }

    private void DisableSentinel()
    {
        FinishBeamAttack();
        CurrentState = SentinelState.Disabled;
        UpdateBeamVisuals();
        RaidFlowController.Instance?.NotifyEnemyKilled(gameObject.name);
        SpawnDeathLootContainer();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 应用敌人来源群规则中的死亡掉落开关。
    /// </summary>
    /// <param name="canSpawnDeathLoot">允许死亡掉落时为 true。</param>
    public void SetCanSpawnDeathLoot(bool canSpawnDeathLoot)
    {
        _canSpawnDeathLoot = canSpawnDeathLoot;
    }

    private void SpawnDeathLootContainer()
    {
        EnemyDeathLootSettings deathLoot = _config != null ? _config.DeathLoot : null;
        if (_hasDroppedLoot ||
            !_canSpawnDeathLoot ||
            deathLoot == null ||
            !deathLoot.SpawnLootContainerOnDeath ||
            deathLoot.DeathLootContainerPrefab == null)
        {
            if (deathLoot != null && deathLoot.SpawnLootContainerOnDeath && deathLoot.DeathLootContainerPrefab == null)
            {
                Debug.LogWarning($"[{name}] Anchor Sentinel config has no DeathLootContainerPrefab assigned.");
            }
            return;
        }

        _hasDroppedLoot = true;
        Vector3 spawnPosition = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.position
            : transform.position + deathLoot.DeathLootSpawnOffset;
        Quaternion spawnRotation = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.rotation
            : Quaternion.identity;

        GameObject lootContainerObject = Instantiate(deathLoot.DeathLootContainerPrefab, spawnPosition, spawnRotation);
        WhiteboxCharacterVisualUtility.ApplySolidColor(lootContainerObject, new Color(0.96f, 0.96f, 0.98f, 1f));
    }

    private void InitializeRunesIfNeeded()
    {
        if (!AutoInitializeRunes || RuneWeakpoints == null)
        {
            return;
        }

        for (int i = 0; i < RuneWeakpoints.Length; i++)
        {
            if (RuneWeakpoints[i] != null)
            {
                int runeOrder = UseArrayOrderAsPuzzleSequence ? i : RuneWeakpoints[i].RuneOrderIndex;
                RuneWeakpoints[i].Initialize(this, runeOrder);
            }
        }
    }

    private bool IsPlayerOutOfRange()
    {
        if (!EnsurePlayerReferences())
        {
            return true;
        }

        return Vector3.Distance(transform.position, PlayerTransform.position) > DetectionRange;
    }

    private void ReturnToDormantState()
    {
        FinishBeamAttack();
        CurrentState = SentinelState.Dormant;
        _stateTimer = 0f;
        _beamTickTimer = 0f;
        _activeStateTimer = 0f;
        ResetPuzzleProgress();
        UpdateBeamVisuals();
    }

    private void FinishBeamAttack()
    {
        if (!_isBeamFiring)
        {
            return;
        }

        EnemySkillDamageLogger.LogSkillDamage(this, "Energy Beam", _beamTotalDamage);
        _isBeamFiring = false;
        _beamTotalDamage = 0f;
    }

    private void ResetPuzzleProgress()
    {
        _expectedRuneIndex = 0;
        if (RuneWeakpoints == null)
        {
            return;
        }

        for (int i = 0; i < RuneWeakpoints.Length; i++)
        {
            AnchorSentinelRuneWeakpoint rune = RuneWeakpoints[i];
            if (rune != null)
            {
                rune.ResetToDormant();
            }
        }
    }

    private void EnsureRuneWeakpointsExist()
    {
        bool hasAnyRune = false;
        if (RuneWeakpoints != null)
        {
            foreach (AnchorSentinelRuneWeakpoint rune in RuneWeakpoints)
            {
                if (rune != null)
                {
                    hasAnyRune = true;
                    break;
                }
            }
        }

        if (hasAnyRune || !AutoSpawnDefaultRunes)
        {
            return;
        }

        // 白模默认符文围绕守卫生成，方便没有美术/关卡配置时也能测试谜题流程。
        int runeCount = Mathf.Max(1, DefaultRuneCount);
        RuneWeakpoints = new AnchorSentinelRuneWeakpoint[runeCount];

        for (int i = 0; i < runeCount; i++)
        {
            GameObject runeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            runeObject.name = $"Rune_{i}";
            runeObject.transform.SetParent(transform, false);

            float angle = (Mathf.PI * 2f / runeCount) * i;
            Vector3 localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * DefaultRuneRadius;
            localPosition.y = DefaultRuneHeight;
            runeObject.transform.localPosition = localPosition;
            runeObject.transform.localScale = DefaultRuneScale;

            Collider runeCollider = runeObject.GetComponent<Collider>();
            if (runeCollider != null)
            {
                runeCollider.isTrigger = true;
            }

            AnchorSentinelRuneWeakpoint runeWeakpoint = runeObject.AddComponent<AnchorSentinelRuneWeakpoint>();
            runeWeakpoint.RuneRenderer = runeObject.GetComponent<Renderer>();
            RuneWeakpoints[i] = runeWeakpoint;
        }
    }

    private void EnsureBeamRenderers()
    {
        if (LockBeamRenderer == null)
        {
            LockBeamRenderer = CreateLineRenderer("LockBeam", new Color(1f, 0.85f, 0.2f, 0.95f), new Color(1f, 0.55f, 0.1f, 0.35f), 0.06f, 0.03f);
        }

        if (FiringBeamRenderer == null)
        {
            FiringBeamRenderer = CreateLineRenderer("FiringBeam", new Color(1f, 0.35f, 0.18f, 0.98f), new Color(1f, 0.82f, 0.28f, 0.55f), 0.14f, 0.05f);
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

    private void UpdateBeamVisuals()
    {
        Vector3 origin = EyeOrigin != null ? EyeOrigin.position : transform.position + Vector3.up * 1.8f;
        Vector3 target = PlayerTransform != null ? PlayerTransform.position + Vector3.up * 0.9f : origin + transform.forward * 5f;

        if (LockBeamRenderer != null)
        {
            // 锁定光束更细并带轻微脉冲，只作为预警线。
            bool showLock = CurrentState == SentinelState.Locking;
            LockBeamRenderer.enabled = showLock;
            if (showLock)
            {
                LockBeamRenderer.startWidth = 0.05f + Mathf.Sin(Time.time * 14f) * 0.01f;
                LockBeamRenderer.endWidth = 0.025f;
                LockBeamRenderer.SetPosition(0, origin);
                LockBeamRenderer.SetPosition(1, target);
            }
        }

        if (FiringBeamRenderer != null)
        {
            // 开火光束更粗，宽度脉冲强化正在造成伤害的反馈。
            bool showBeam = CurrentState == SentinelState.Firing;
            FiringBeamRenderer.enabled = showBeam;
            if (showBeam)
            {
                float pulse = 0.5f + Mathf.Sin(Time.time * 28f) * 0.5f;
                FiringBeamRenderer.startWidth = 0.14f + pulse * 0.05f;
                FiringBeamRenderer.endWidth = 0.06f + pulse * 0.03f;
                FiringBeamRenderer.SetPosition(0, origin);
                FiringBeamRenderer.SetPosition(1, target);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        if (EyeOrigin != null && PlayerTransform != null)
        {
            Gizmos.DrawLine(EyeOrigin.position, PlayerTransform.position + Vector3.up * 0.9f);
        }
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

        if (PlayerTransform != null && _playerHealthController == null)
        {
            _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
            if (_playerHealthController == null)
            {
                _playerHealthController = PlayerTransform.GetComponentInParent<PlayerHealthController>();
            }
        }

        if (_playerHealthController == null && PlayerHealthController.Instance != null)
        {
            _playerHealthController = PlayerHealthController.Instance;
            PlayerTransform = _playerHealthController.transform;
        }

        if (PlayerTransform != null &&
            CombatDamageUtility.TryGetDamageReceiver(PlayerTransform, out ICombatDamageReceiver receiver))
        {
            _combatDamageReceiver = receiver;
            if (receiver.DamageRootTransform != null)
            {
                PlayerTransform = receiver.DamageRootTransform;
            }

            _playerHealthController = receiver as PlayerHealthController ?? _playerHealthController;
        }

        if (_combatDamageReceiver == null && _playerHealthController != null)
        {
            _combatDamageReceiver = _playerHealthController;
        }

        return PlayerTransform != null &&
               _combatDamageReceiver != null &&
               _combatDamageReceiver.IsCombatDamageReceiverAlive;
    }
}
