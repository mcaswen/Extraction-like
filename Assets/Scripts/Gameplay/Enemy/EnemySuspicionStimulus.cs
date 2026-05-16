using System;
using UnityEngine;

public enum EnemySuspicionStimulusType
{
    Footstep,
    Gunshot,
    ProjectileImpact,
    LootInteraction,
    EnemyDamaged,
    PlayerLastSeen,
    AllyAlert
}

public readonly struct EnemySuspicionStimulus
{
    public readonly EnemySuspicionStimulusType Type;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Strength;
    public readonly float Duration;
    public readonly float UncertaintyRadius;
    public readonly Transform Source;

    public EnemySuspicionStimulus(
        EnemySuspicionStimulusType type,
        Vector3 position,
        float radius,
        float strength,
        float duration,
        float uncertaintyRadius,
        Transform source)
    {
        Type = type;
        Position = position;
        Radius = Mathf.Max(0.1f, radius);
        Strength = Mathf.Clamp01(strength);
        Duration = Mathf.Max(0.1f, duration);
        UncertaintyRadius = Mathf.Max(0f, uncertaintyRadius);
        Source = source;
    }
}

public readonly struct EnemySuspicionRecord
{
    public readonly EnemySuspicionStimulusType Type;
    public readonly Vector3 EstimatedPosition;
    public readonly Vector3 SourcePosition;
    public readonly float Strength;
    public readonly float ExpireTime;
    public readonly Transform Source;

    public EnemySuspicionRecord(
        EnemySuspicionStimulusType type,
        Vector3 estimatedPosition,
        Vector3 sourcePosition,
        float strength,
        float expireTime,
        Transform source)
    {
        Type = type;
        EstimatedPosition = estimatedPosition;
        SourcePosition = sourcePosition;
        Strength = Mathf.Clamp01(strength);
        ExpireTime = expireTime;
        Source = source;
    }
}

public static class EnemySuspicionStimulusBus
{
    public static event Action<EnemySuspicionStimulus> StimulusRaised;

    public static void Raise(EnemySuspicionStimulus stimulus)
    {
        StimulusRaised?.Invoke(stimulus);
    }

    public static void Raise(
        EnemySuspicionStimulusType type,
        Vector3 position,
        float radius,
        float strength,
        float duration,
        float uncertaintyRadius = 0f,
        Transform source = null)
    {
        Raise(new EnemySuspicionStimulus(
            type,
            position,
            radius,
            strength,
            duration,
            uncertaintyRadius,
            source));
    }

    public static void ReportFootstep(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.Footstep, position, 11f, 0.32f, 1.4f, 1.6f, source);
    }

    public static void ReportGunshot(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.Gunshot, position, 34f, 1f, 5f, 2.6f, source);
    }

    public static void ReportProjectileImpact(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.ProjectileImpact, position, 19f, 0.72f, 3.5f, 2f, source);
    }

    public static void ReportEnemyDamaged(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.EnemyDamaged, position, 24f, 0.88f, 4.5f, 1.2f, source);
    }

    public static void ReportLootInteraction(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.LootInteraction, position, 15f, 0.58f, 3f, 1.8f, source);
    }
}
