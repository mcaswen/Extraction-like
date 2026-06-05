using UnityEngine;

/// <summary>
/// 锚点守卫的符文谜题和光束攻击配置。
/// </summary>
[CreateAssetMenu(fileName = "SO_Enemy_AnchorSentinel", menuName = "Enemies/Anchor Sentinel Config")]
public sealed class AnchorSentinelConfig : EnemyHealthConfigBase
{
    [Header("Rune Puzzle")]
    [SerializeField, Tooltip("Initialize assigned rune weakpoints on startup.")]
    private bool _autoInitializeRunes = true;

    [SerializeField, Tooltip("Create default rune weakpoints when none are assigned.")]
    private bool _autoSpawnDefaultRunes = true;

    [SerializeField, Min(1), Tooltip("Number of default rune weakpoints created when needed.")]
    private int _defaultRuneCount = 3;

    [SerializeField, Min(0.1f), Tooltip("Radius used to place generated rune weakpoints.")]
    private float _defaultRuneRadius = 1.8f;

    [SerializeField, Tooltip("Height offset used to place generated rune weakpoints.")]
    private float _defaultRuneHeight = 1.25f;

    [SerializeField, Tooltip("Local scale used for generated rune weakpoints.")]
    private Vector3 _defaultRuneScale = new Vector3(0.35f, 0.35f, 0.35f);

    [SerializeField, Tooltip("Use the rune array order as the required puzzle hit sequence.")]
    private bool _useArrayOrderAsPuzzleSequence = true;

    [SerializeField, Tooltip("Wrong rune hits immediately activate the sentinel.")]
    private bool _wrongRuneImmediatelyActivates = true;

    [Header("Beam Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the sentinel detects and attacks the player.")]
    private float _detectionRange = 16f;

    [SerializeField, Min(0.05f), Tooltip("Seconds from activation to the first beam volley.")]
    private float _lockDuration = 5f;

    [SerializeField, Min(0.05f), Tooltip("Seconds the firing beam remains active.")]
    private float _firingDuration = 3f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between beam volley start times.")]
    private float _cooldownDuration = 5f;

    [SerializeField, Min(0f), Tooltip("Seconds before the sentinel returns dormant while active. Set to 0 to stay active until out of range.")]
    private float _activeRecoveryDuration = 0f;

    [SerializeField, Min(0f), Tooltip("Beam damage per second while firing.")]
    private float _beamDamagePerSecond = 22f;

    [SerializeField, Min(0.05f), Tooltip("Damage tick interval while the beam is firing.")]
    private float _beamTickInterval = 0.12f;

    /// <summary>
    /// 启动时是否初始化已配置的符文弱点。
    /// </summary>
    public bool AutoInitializeRunes => _autoInitializeRunes;

    /// <summary>
    /// 未配置符文时是否自动生成默认符文。
    /// </summary>
    public bool AutoSpawnDefaultRunes => _autoSpawnDefaultRunes;

    /// <summary>
    /// 自动生成默认符文的数量。
    /// </summary>
    public int DefaultRuneCount => _defaultRuneCount;

    /// <summary>
    /// 默认符文围绕守卫生成的半径。
    /// </summary>
    public float DefaultRuneRadius => _defaultRuneRadius;

    /// <summary>
    /// 默认符文的生成高度。
    /// </summary>
    public float DefaultRuneHeight => _defaultRuneHeight;

    /// <summary>
    /// 默认符文的本地缩放。
    /// </summary>
    public Vector3 DefaultRuneScale => _defaultRuneScale;

    /// <summary>
    /// 是否将符文数组顺序作为谜题命中顺序。
    /// </summary>
    public bool UseArrayOrderAsPuzzleSequence => _useArrayOrderAsPuzzleSequence;

    /// <summary>
    /// 命中错误符文时是否立即激活守卫。
    /// </summary>
    public bool WrongRuneImmediatelyActivates => _wrongRuneImmediatelyActivates;

    /// <summary>
    /// 守卫检测并攻击玩家的距离。
    /// </summary>
    public float DetectionRange => _detectionRange;

    /// <summary>
    /// 守卫激活后到第一道光束开火前的锁定时间。
    /// </summary>
    public float LockDuration => _lockDuration;

    /// <summary>
    /// 光束持续造成伤害的时间。
    /// </summary>
    public float FiringDuration => _firingDuration;

    /// <summary>
    /// 两次光束开火起点之间的间隔。
    /// </summary>
    public float CooldownDuration => _cooldownDuration;

    /// <summary>
    /// 守卫激活后自动恢复休眠的时间，0 表示不按时间恢复。
    /// </summary>
    public float ActiveRecoveryDuration => _activeRecoveryDuration;

    /// <summary>
    /// 光束每秒造成的伤害。
    /// </summary>
    public float BeamDamagePerSecond => _beamDamagePerSecond;

    /// <summary>
    /// 光束伤害的结算间隔。
    /// </summary>
    public float BeamTickInterval => _beamTickInterval;

    /// <summary>
    /// 校验符文谜题和光束攻击参数。
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        _defaultRuneCount = Mathf.Max(1, _defaultRuneCount);
        _defaultRuneRadius = Mathf.Max(0.1f, _defaultRuneRadius);
        _defaultRuneScale = new Vector3(
            Mathf.Max(0.01f, _defaultRuneScale.x),
            Mathf.Max(0.01f, _defaultRuneScale.y),
            Mathf.Max(0.01f, _defaultRuneScale.z));
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _lockDuration = Mathf.Max(0.05f, _lockDuration);
        _firingDuration = Mathf.Max(0.05f, _firingDuration);
        _cooldownDuration = Mathf.Max(0.05f, _cooldownDuration);
        _activeRecoveryDuration = Mathf.Max(0f, _activeRecoveryDuration);
        _beamDamagePerSecond = Mathf.Max(0f, _beamDamagePerSecond);
        _beamTickInterval = Mathf.Max(0.05f, _beamTickInterval);
    }
}
