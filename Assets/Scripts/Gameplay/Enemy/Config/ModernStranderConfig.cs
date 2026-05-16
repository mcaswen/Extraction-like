using UnityEngine;

[CreateAssetMenu(fileName = "SO_Enemy_ModernStrander", menuName = "Enemies/Modern Strander Config")]
public sealed class ModernStranderConfig : EnemyPatrolConfigBase
{
    [Header("Tentacle Attack")]
    [SerializeField, Min(0.1f), Tooltip("Distance at which the tentacle attack can start.")]
    private float _attackRange = 3.2f;

    [SerializeField, Min(0.05f), Tooltip("Seconds between tentacle attacks.")]
    private float _attackInterval = 5f;

    [SerializeField, Min(0.05f), Tooltip("Maximum seconds a tentacle strike or latch remains active.")]
    private float _tentacleLatchDuration = 1.1f;

    [SerializeField, Min(0.1f), Tooltip("Width of the generated tentacle hitbox.")]
    private float _tentacleHitboxWidth = 0.55f;

    [SerializeField, Min(0.1f), Tooltip("Height of the generated tentacle hitbox.")]
    private float _tentacleHitboxHeight = 0.55f;

    [SerializeField, Min(0f), Tooltip("Pull strength applied to the player during a latch.")]
    private float _latchPullStrength = 3.4f;

    [SerializeField, Min(0f), Tooltip("Corrosion damage per second applied during a latch.")]
    private float _corrosionDamagePerSecond = 10f;

    [SerializeField, Min(0f), Tooltip("Corrosion status duration applied during a latch.")]
    private float _corrosionDuration = 2.5f;

    [SerializeField, Min(0.05f), Tooltip("Corrosion tick interval applied during a latch.")]
    private float _corrosionTickInterval = 0.25f;

    [SerializeField, Min(0f), Tooltip("Immediate damage dealt on first tentacle contact.")]
    private float _initialContactDamage = 6f;

    [Header("Corrosive Slime")]
    [SerializeField, Tooltip("Optional prefab for the corrosive puddle spawned after a successful latch.")]
    private GameObject _corrosivePuddlePrefab;

    [SerializeField, Min(0.1f), Tooltip("Seconds before the corrosive puddle is destroyed.")]
    private float _puddleLifetime = 5f;

    [SerializeField, Min(0.2f), Tooltip("Radius of the corrosive puddle trigger.")]
    private float _puddleRadius = 1.1f;

    [SerializeField, Min(0f), Tooltip("Damage per second applied by the corrosive puddle.")]
    private float _puddleDamagePerSecond = 6f;

    [SerializeField, Min(0f), Tooltip("Corrosion duration applied by the corrosive puddle.")]
    private float _puddleCorrosionDuration = 1.8f;

    [SerializeField, Min(0.05f), Tooltip("Corrosion tick interval applied by the corrosive puddle.")]
    private float _puddleTickInterval = 0.25f;

    public float AttackRange => _attackRange;
    public float AttackInterval => _attackInterval;
    public float TentacleLatchDuration => _tentacleLatchDuration;
    public float TentacleHitboxWidth => _tentacleHitboxWidth;
    public float TentacleHitboxHeight => _tentacleHitboxHeight;
    public float LatchPullStrength => _latchPullStrength;
    public float CorrosionDamagePerSecond => _corrosionDamagePerSecond;
    public float CorrosionDuration => _corrosionDuration;
    public float CorrosionTickInterval => _corrosionTickInterval;
    public float InitialContactDamage => _initialContactDamage;
    public GameObject CorrosivePuddlePrefab => _corrosivePuddlePrefab;
    public float PuddleLifetime => _puddleLifetime;
    public float PuddleRadius => _puddleRadius;
    public float PuddleDamagePerSecond => _puddleDamagePerSecond;
    public float PuddleCorrosionDuration => _puddleCorrosionDuration;
    public float PuddleTickInterval => _puddleTickInterval;

    protected override void OnValidate()
    {
        base.OnValidate();
        _attackRange = Mathf.Max(0.1f, _attackRange);
        _attackInterval = Mathf.Max(0.05f, _attackInterval);
        _tentacleLatchDuration = Mathf.Max(0.05f, _tentacleLatchDuration);
        _tentacleHitboxWidth = Mathf.Max(0.1f, _tentacleHitboxWidth);
        _tentacleHitboxHeight = Mathf.Max(0.1f, _tentacleHitboxHeight);
        _latchPullStrength = Mathf.Max(0f, _latchPullStrength);
        _corrosionDamagePerSecond = Mathf.Max(0f, _corrosionDamagePerSecond);
        _corrosionDuration = Mathf.Max(0f, _corrosionDuration);
        _corrosionTickInterval = Mathf.Max(0.05f, _corrosionTickInterval);
        _initialContactDamage = Mathf.Max(0f, _initialContactDamage);
        _puddleLifetime = Mathf.Max(0.1f, _puddleLifetime);
        _puddleRadius = Mathf.Max(0.2f, _puddleRadius);
        _puddleDamagePerSecond = Mathf.Max(0f, _puddleDamagePerSecond);
        _puddleCorrosionDuration = Mathf.Max(0f, _puddleCorrosionDuration);
        _puddleTickInterval = Mathf.Max(0.05f, _puddleTickInterval);
    }
}
