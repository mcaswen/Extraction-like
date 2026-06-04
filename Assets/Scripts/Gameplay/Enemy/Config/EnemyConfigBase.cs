using System;
using UnityEngine;

public abstract class EnemyConfigBase : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("Stable designer-facing identifier for this enemy config.")]
    private string _enemyId = "enemy";

    [SerializeField, Tooltip("Display name used by designers to identify this enemy config.")]
    private string _displayName = "Enemy";

    public string EnemyId => _enemyId;
    public string DisplayName => _displayName;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_enemyId))
        {
            _enemyId = name;
        }

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            _displayName = _enemyId;
        }
    }
}

public abstract class EnemyHealthConfigBase : EnemyConfigBase
{
    [Header("Attributes")]
    [SerializeField, Min(1f), Tooltip("Maximum health for this enemy.")]
    private float _maxHealth = 100f;

    [Header("Death Loot")]
    [SerializeField, Tooltip("Death loot settings owned by this enemy config.")]
    private EnemyDeathLootSettings _deathLoot = new EnemyDeathLootSettings();

    public float MaxHealth => _maxHealth;
    public EnemyDeathLootSettings DeathLoot => _deathLoot;

    protected override void OnValidate()
    {
        base.OnValidate();
        _maxHealth = Mathf.Max(1f, _maxHealth);
        _deathLoot ??= new EnemyDeathLootSettings();
        _deathLoot.Validate();
    }
}

public abstract class EnemyPatrolConfigBase : EnemyHealthConfigBase
{
    [Header("Patrol")]
    [SerializeField, Tooltip("Current random-radius patrol settings.")]
    private EnemyPatrolSettings _patrol = new EnemyPatrolSettings();

    [Header("Detection")]
    [SerializeField, Tooltip("Player detection and disengage settings.")]
    private EnemyDetectionSettings _detection = new EnemyDetectionSettings();

    public EnemyPatrolSettings Patrol => _patrol;
    public EnemyDetectionSettings Detection => _detection;

    protected override void OnValidate()
    {
        base.OnValidate();
        _patrol ??= new EnemyPatrolSettings();
        _detection ??= new EnemyDetectionSettings();
        _patrol.Validate();
        _detection.Validate();
    }
}

public enum EnemyPatrolMode
{
    RandomRadius,
    FixedRoute
}

public enum EnemyAwarenessPreset
{
    SimpleVisionOnly,
    VisionWithSearch,
    FullSuspicion
}

public static class EnemyAwarenessPresetUtility
{
    public static bool UsesPatrolAwareness(this EnemyAwarenessPreset preset)
    {
        return preset != EnemyAwarenessPreset.SimpleVisionOnly;
    }

    public static bool ShouldReportPlayerLastSeen(this EnemyAwarenessPreset preset)
    {
        return preset != EnemyAwarenessPreset.SimpleVisionOnly;
    }

    public static bool AllowsSuspicionRecord(this EnemyAwarenessPreset preset, EnemySuspicionRecord record)
    {
        switch (preset)
        {
            case EnemyAwarenessPreset.SimpleVisionOnly:
                return false;
            case EnemyAwarenessPreset.VisionWithSearch:
                return record.Type == EnemySuspicionStimulusType.PlayerLastSeen;
            default:
                return true;
        }
    }
}

[Serializable]
public sealed class EnemyPatrolSettings
{
    [SerializeField, Tooltip("How this enemy chooses patrol destinations.")]
    private EnemyPatrolMode _patrolMode = EnemyPatrolMode.RandomRadius;

    [SerializeField, Min(0.1f), Tooltip("Radius around the spawn position used for random patrol points.")]
    private float _patrolRadius = 10f;

    [SerializeField, Min(0f), Tooltip("Seconds to wait after reaching a patrol point.")]
    private float _patrolWaitTime = 2f;

    public EnemyPatrolMode PatrolMode => _patrolMode;
    public float PatrolRadius => _patrolRadius;
    public float PatrolWaitTime => _patrolWaitTime;

    public void Validate()
    {
        _patrolRadius = Mathf.Max(0.1f, _patrolRadius);
        _patrolWaitTime = Mathf.Max(0f, _patrolWaitTime);
    }
}

[Serializable]
public sealed class EnemyDetectionSettings
{
    [SerializeField, Tooltip("High-level patrol awareness behavior exposed to designers.")]
    private EnemyAwarenessPreset _awarenessPreset = EnemyAwarenessPreset.FullSuspicion;

    [SerializeField, Min(0.1f), Tooltip("View range at which this enemy can start chasing or attacking the player.")]
    private float _detectionRange = 15f;

    [SerializeField, Range(1f, 360f), Tooltip("Horizontal view angle used while patrolling.")]
    private float _viewAngle = 112f;

    [SerializeField, Tooltip("Layers that block line of sight between this enemy and the player.")]
    private LayerMask _lineOfSightBlockMask = 1;

    [SerializeField, Tooltip("Layers sampled by the gameplay vision overlay when fitting the visible area to the ground.")]
    private LayerMask _groundMask = 1;

    [SerializeField, Min(0f), Tooltip("Height offset used as this enemy's line-of-sight origin.")]
    private float _eyeHeight = 1.2f;

    [SerializeField, Min(0f), Tooltip("Height offset used as the player's line-of-sight target.")]
    private float _targetHeight = 1f;

    [SerializeField, Min(0.1f), Tooltip("Distance at which this enemy stops engaging and returns to patrol.")]
    private float _loseRange = 20f;

    public EnemyAwarenessPreset AwarenessPreset => _awarenessPreset;
    public float DetectionRange => _detectionRange;
    public float ViewAngle => _viewAngle;
    public LayerMask LineOfSightBlockMask => _lineOfSightBlockMask;
    public LayerMask GroundMask => _groundMask;
    public float EyeHeight => _eyeHeight;
    public float TargetHeight => _targetHeight;
    public float LoseRange => _loseRange;

    public void Validate()
    {
        _detectionRange = Mathf.Max(0.1f, _detectionRange);
        _viewAngle = Mathf.Clamp(_viewAngle, 1f, 360f);
        _eyeHeight = Mathf.Max(0f, _eyeHeight);
        _targetHeight = Mathf.Max(0f, _targetHeight);
        _loseRange = Mathf.Max(_detectionRange, _loseRange);
    }
}

[Serializable]
public sealed class EnemyDeathLootSettings
{
    [SerializeField, Tooltip("Whether this enemy should spawn a loot container when defeated.")]
    private bool _spawnLootContainerOnDeath = true;

    [SerializeField, Tooltip("Loot container prefab spawned when this enemy is defeated.")]
    private GameObject _deathLootContainerPrefab;

    [SerializeField, Tooltip("Fallback spawn offset when no death loot spawn point is assigned on the prefab.")]
    private Vector3 _deathLootSpawnOffset = new Vector3(0f, 0.1f, 0f);

    public bool SpawnLootContainerOnDeath => _spawnLootContainerOnDeath;
    public GameObject DeathLootContainerPrefab => _deathLootContainerPrefab;
    public Vector3 DeathLootSpawnOffset => _deathLootSpawnOffset;

    public void Validate()
    {
    }
}
