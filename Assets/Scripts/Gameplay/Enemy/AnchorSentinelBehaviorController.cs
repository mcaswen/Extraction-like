using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 锚点守卫白模行为。
/// 玩家需按顺序摧毁符文弱点，否则守卫会激活并发射能量光束。
/// </summary>
public class AnchorSentinelBehaviorController : MonoBehaviour
{
    public enum SentinelState
    {
        Dormant,
        Locking,
        Firing,
        Cooldown,
        Disabled
    }

    public SentinelState CurrentState = SentinelState.Dormant;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this enemy's tunable values.")]
    private AnchorSentinelConfig _config;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform EyeOrigin;
    public LineRenderer LockBeamRenderer;
    public LineRenderer FiringBeamRenderer;
    public AnchorSentinelRuneWeakpoint[] RuneWeakpoints;

    [HideInInspector]
    public bool AutoInitializeRunes = true;
    [HideInInspector]
    public bool AutoSpawnDefaultRunes = true;
    [HideInInspector]
    public int DefaultRuneCount = 3;
    [HideInInspector]
    public float DefaultRuneRadius = 1.8f;
    [HideInInspector]
    public float DefaultRuneHeight = 1.25f;
    [HideInInspector]
    public Vector3 DefaultRuneScale = new Vector3(0.35f, 0.35f, 0.35f);
    [HideInInspector]
    public bool UseArrayOrderAsPuzzleSequence = true;
    [HideInInspector]
    public bool WrongRuneImmediatelyActivates = true;

    [HideInInspector]
    public float DetectionRange = 16f;
    [HideInInspector]
    public float LockDuration = 1.1f;
    [HideInInspector]
    public float FiringDuration = 0.55f;
    [HideInInspector]
    public float CooldownDuration = 1.5f;
    [HideInInspector]
    public float ActiveRecoveryDuration = 0f;
    [HideInInspector]
    public float BeamDamagePerSecond = 22f;
    [HideInInspector]
    public float BeamTickInterval = 0.12f;

    [Header("Death Loot References")]
    public Transform DeathLootSpawnPoint;

    private PlayerHealthController _playerHealthController;
    private float _stateTimer;
    private float _beamTickTimer;
    private float _beamTotalDamage;
    private float _activeStateTimer;
    private int _expectedRuneIndex;
    private bool _hasDroppedLoot;
    private bool _isBeamFiring;

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        EnsurePlayerReferences();

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
    }

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
            CurrentState = SentinelState.Firing;
            _stateTimer = 0f;
            _beamTickTimer = 0f;
            _beamTotalDamage = 0f;
            _isBeamFiring = true;
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

        while (_beamTickTimer >= BeamTickInterval)
        {
            _beamTickTimer -= BeamTickInterval;
            if (_playerHealthController != null)
            {
                _beamTotalDamage += _playerHealthController.TakeDamage(BeamDamagePerSecond * BeamTickInterval);
            }
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

    private void SpawnDeathLootContainer()
    {
        EnemyDeathLootSettings deathLoot = _config != null ? _config.DeathLoot : null;
        if (_hasDroppedLoot ||
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
        LootBoxEntity lootBox = lootContainerObject.GetComponent<LootBoxEntity>();
        if (lootBox == null)
        {
            lootBox = lootContainerObject.GetComponentInChildren<LootBoxEntity>();
        }

        if (lootBox != null)
        {
            lootBox.PrecalculateLootIfNeeded();
        }
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
                _playerHealthController = PlayerTransform.gameObject.AddComponent<PlayerHealthController>();
            }
        }

        if (_playerHealthController == null && PlayerHealthController.Instance != null)
        {
            _playerHealthController = PlayerHealthController.Instance;
            PlayerTransform = _playerHealthController.transform;
        }

        return PlayerTransform != null && _playerHealthController != null;
    }
}
