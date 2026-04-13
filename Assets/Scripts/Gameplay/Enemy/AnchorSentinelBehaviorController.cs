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

    [Header("References")]
    public Transform PlayerTransform;
    public Transform EyeOrigin;
    public LineRenderer LockBeamRenderer;
    public LineRenderer FiringBeamRenderer;
    public AnchorSentinelRuneWeakpoint[] RuneWeakpoints;

    [Header("Puzzle Rules")]
    public bool AutoInitializeRunes = true;
    public bool AutoSpawnDefaultRunes = true;
    public int DefaultRuneCount = 3;
    public float DefaultRuneRadius = 1.8f;
    public float DefaultRuneHeight = 1.25f;
    public Vector3 DefaultRuneScale = new Vector3(0.35f, 0.35f, 0.35f);
    public bool UseArrayOrderAsPuzzleSequence = true;
    public bool WrongRuneImmediatelyActivates = true;

    [Header("Beam Attack")]
    public float DetectionRange = 16f;
    public float LockDuration = 1.1f;
    public float FiringDuration = 0.55f;
    public float CooldownDuration = 1.5f;
    public float ActiveRecoveryDuration = 5f;
    public float BeamDamagePerSecond = 22f;
    public float BeamTickInterval = 0.12f;

    [Header("Death Loot")]
    public bool SpawnLootContainerOnDisable = true;
    public GameObject DeathLootContainerPrefab;
    public Transform DeathLootSpawnPoint;
    public Vector3 DeathLootSpawnOffset = new Vector3(0f, 0.25f, 0f);

    private PlayerHealthController _playerHealthController;
    private float _stateTimer;
    private float _beamTickTimer;
    private float _activeStateTimer;
    private int _expectedRuneIndex;
    private bool _hasDroppedLoot;

    private void Start()
    {
        EnsurePlayerReferences();

        EnsureRuneWeakpointsExist();
        EnsureBeamRenderers();
        InitializeRunesIfNeeded();
        UpdateBeamVisuals();
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
            if (_activeStateTimer >= ActiveRecoveryDuration)
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
                _playerHealthController.TakeDamage(BeamDamagePerSecond * BeamTickInterval);
            }
        }

        if (_stateTimer >= FiringDuration)
        {
            CurrentState = SentinelState.Cooldown;
            _stateTimer = 0f;
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
        if (_stateTimer >= CooldownDuration)
        {
            CurrentState = SentinelState.Locking;
            _stateTimer = 0f;
        }
    }

    private void ActivateSentinel()
    {
        if (CurrentState == SentinelState.Disabled)
        {
            return;
        }

        CurrentState = SentinelState.Locking;
        _stateTimer = 0f;
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
        CurrentState = SentinelState.Disabled;
        UpdateBeamVisuals();
        RaidFlowController.Instance?.NotifyEnemyKilled(gameObject.name);
        SpawnDeathLootContainer();
        gameObject.SetActive(false);
    }

    private void SpawnDeathLootContainer()
    {
        if (!SpawnLootContainerOnDisable || DeathLootContainerPrefab == null || _hasDroppedLoot)
        {
            if (SpawnLootContainerOnDisable && DeathLootContainerPrefab == null)
            {
                Debug.LogWarning($"[{name}] Anchor Sentinel has no DeathLootContainerPrefab assigned.");
            }
            return;
        }

        _hasDroppedLoot = true;
        Vector3 spawnPosition = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.position
            : transform.position + DeathLootSpawnOffset;
        Quaternion spawnRotation = DeathLootSpawnPoint != null
            ? DeathLootSpawnPoint.rotation
            : Quaternion.identity;

        GameObject lootContainerObject = Instantiate(DeathLootContainerPrefab, spawnPosition, spawnRotation);
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
        CurrentState = SentinelState.Dormant;
        _stateTimer = 0f;
        _beamTickTimer = 0f;
        _activeStateTimer = 0f;
        ResetPuzzleProgress();
        UpdateBeamVisuals();
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
