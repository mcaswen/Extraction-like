using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// Anchor Sentinel behavior.
/// The sentinel is now a standard enemy: the player damages its body directly,
/// and the sentinel attacks when the player enters detection range.
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

    private EnemyAnimatorDriver _animatorDriver;
    private ICombatDamageReceiver _combatDamageReceiver;
    private float _stateTimer;
    private float _beamTickTimer;
    private float _beamTotalDamage;
    private float _activeStateTimer;
    private bool _isBeamFiring;

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        EnsurePlayerReferences();
        _animatorDriver = new EnemyAnimatorDriver(this);
        ApplyHealthConfig();
        EnsureBeamRenderers();
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

    private void TickDormantState()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        if (distanceToPlayer <= DetectionRange)
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

    private void EnsureBeamRenderers()
    {
        if (LockBeamRenderer == null)
        {
            LockBeamRenderer = CreateLineRenderer(
                "LockBeam",
                new Color(1f, 0.85f, 0.2f, 0.95f),
                new Color(1f, 0.55f, 0.1f, 0.35f),
                0.06f,
                0.03f);
        }

        if (FiringBeamRenderer == null)
        {
            FiringBeamRenderer = CreateLineRenderer(
                "FiringBeam",
                new Color(1f, 0.35f, 0.18f, 0.98f),
                new Color(1f, 0.82f, 0.28f, 0.55f),
                0.14f,
                0.05f);
        }
    }

    private LineRenderer CreateLineRenderer(
        string objectName,
        Color startColor,
        Color endColor,
        float startWidth,
        float endWidth)
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
        Vector3 target = PlayerTransform != null
            ? PlayerTransform.position + Vector3.up * 0.9f
            : origin + transform.forward * 5f;

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
            PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out PlayerTransform);
        }

        if (PlayerTransform != null &&
            CombatDamageUtility.TryGetDamageReceiver(PlayerTransform, out ICombatDamageReceiver receiver))
        {
            _combatDamageReceiver = receiver;
            if (receiver.DamageRootTransform != null)
            {
                PlayerTransform = receiver.DamageRootTransform;
            }
        }

        return PlayerTransform != null &&
               _combatDamageReceiver != null &&
               _combatDamageReceiver.IsCombatDamageReceiverAlive;
    }
}
