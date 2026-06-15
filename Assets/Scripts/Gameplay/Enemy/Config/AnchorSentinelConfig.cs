using UnityEngine;

/// <summary>
/// Anchor Sentinel health and beam attack config.
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_AnchorSentinel", menuName = "Enemies/Anchor Sentinel Config")]
public sealed class AnchorSentinelConfig : EnemyHealthConfigBase
{
    [Header("Beam Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the sentinel detects and attacks the player.")]
    private float _detectionRange = 16f;

    [SerializeField, Min(0.05f), Tooltip("Seconds from activation to the first beam volley.")]
    private float _lockDuration = 5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the firing beam remains active.")]
    private float _firingDuration = 3f;

    [SerializeField, Min(0.05f), Tooltip("Seconds after a beam finishes before the sentinel starts locking again.")]
    private float _cooldownDuration = 5f;

    [SerializeField, Min(0f), Tooltip("Seconds before the sentinel returns dormant while active. Set to 0 to stay active until out of range.")]
    private float _activeRecoveryDuration = 0f;

    [SerializeField, Min(0f), Tooltip("Beam damage per second while firing.")]
    private float _beamDamagePerSecond = 22f;

    [SerializeField, Min(0.05f), Tooltip("Damage tick interval while the beam is firing.")]
    private float _beamTickInterval = 0.12f;

    public float DetectionRange => _detectionRange;
    public float LockDuration => _lockDuration;
    public float FiringDuration => _firingDuration;
    public float CooldownDuration => _cooldownDuration;
    public float ActiveRecoveryDuration => _activeRecoveryDuration;
    public float BeamDamagePerSecond => _beamDamagePerSecond;
    public float BeamTickInterval => _beamTickInterval;

    protected override void OnValidate()
    {
        base.OnValidate();
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _lockDuration = Mathf.Max(0.05f, _lockDuration);
        _firingDuration = Mathf.Max(0.05f, _firingDuration);
        _cooldownDuration = Mathf.Max(0.05f, _cooldownDuration);
        _activeRecoveryDuration = Mathf.Max(0f, _activeRecoveryDuration);
        _beamDamagePerSecond = Mathf.Max(0f, _beamDamagePerSecond);
        _beamTickInterval = Mathf.Max(0.05f, _beamTickInterval);
    }
}
