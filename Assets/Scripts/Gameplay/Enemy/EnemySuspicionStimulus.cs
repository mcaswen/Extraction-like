using System;
using UnityEngine;

/// <summary>
/// 敌人怀疑刺激的来源类型。
/// </summary>
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

/// <summary>
/// 一次会被敌人听觉/感知系统接收的怀疑刺激。
/// </summary>
public readonly struct EnemySuspicionStimulus
{
    /// <summary>
    /// 刺激类型。
    /// </summary>
    public readonly EnemySuspicionStimulusType Type;

    /// <summary>
    /// 刺激发生的世界坐标。
    /// </summary>
    public readonly Vector3 Position;

    /// <summary>
    /// 刺激传播半径。
    /// </summary>
    public readonly float Radius;

    /// <summary>
    /// 刺激强度，范围为 0 到 1。
    /// </summary>
    public readonly float Strength;

    /// <summary>
    /// 被敌人记录后的有效持续时间。
    /// </summary>
    public readonly float Duration;

    /// <summary>
    /// 敌人估算刺激位置时允许的随机误差半径。
    /// </summary>
    public readonly float UncertaintyRadius;

    /// <summary>
    /// 刺激来源对象，用于避免敌人响应自己产生的刺激。
    /// </summary>
    public readonly Transform Source;

    /// <summary>
    /// 创建一条怀疑刺激并修正半径、强度、持续时间等输入范围。
    /// </summary>
    /// <param name="type">刺激类型。</param>
    /// <param name="position">刺激发生位置。</param>
    /// <param name="radius">传播半径。</param>
    /// <param name="strength">原始强度。</param>
    /// <param name="duration">记录持续时间。</param>
    /// <param name="uncertaintyRadius">位置不确定半径。</param>
    /// <param name="source">刺激来源对象。</param>
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

/// <summary>
/// 单个敌人已经接收并估算后的怀疑记录。
/// </summary>
public readonly struct EnemySuspicionRecord
{
    /// <summary>
    /// 记录来源的刺激类型。
    /// </summary>
    public readonly EnemySuspicionStimulusType Type;

    /// <summary>
    /// 敌人估算出的调查位置。
    /// </summary>
    public readonly Vector3 EstimatedPosition;

    /// <summary>
    /// 刺激真实发生位置。
    /// </summary>
    public readonly Vector3 SourcePosition;

    /// <summary>
    /// 敌人接收到的最终强度。
    /// </summary>
    public readonly float Strength;

    /// <summary>
    /// 记录过期的游戏时间。
    /// </summary>
    public readonly float ExpireTime;

    /// <summary>
    /// 刺激来源对象。
    /// </summary>
    public readonly Transform Source;

    /// <summary>
    /// 创建一条敌人内部使用的怀疑记录。
    /// </summary>
    /// <param name="type">刺激类型。</param>
    /// <param name="estimatedPosition">估算调查位置。</param>
    /// <param name="sourcePosition">真实刺激位置。</param>
    /// <param name="strength">最终强度。</param>
    /// <param name="expireTime">过期时间。</param>
    /// <param name="source">刺激来源对象。</param>
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

/// <summary>
/// 敌人怀疑刺激事件总线。
/// 负责把脚步、枪声、命中和交互等事件广播给附近敌人的感知传感器。
/// </summary>
public static class EnemySuspicionStimulusBus
{
    /// <summary>
    /// 怀疑刺激被抛出时触发的全局事件。
    /// </summary>
    public static event Action<EnemySuspicionStimulus> StimulusRaised;

    /// <summary>
    /// 广播一条已经构造好的怀疑刺激。
    /// </summary>
    /// <param name="stimulus">待广播的刺激。</param>
    public static void Raise(EnemySuspicionStimulus stimulus)
    {
        StimulusRaised?.Invoke(stimulus);
    }

    /// <summary>
    /// 按参数构造并广播一条怀疑刺激。
    /// </summary>
    /// <param name="type">刺激类型。</param>
    /// <param name="position">刺激发生位置。</param>
    /// <param name="radius">传播半径。</param>
    /// <param name="strength">刺激强度。</param>
    /// <param name="duration">记录持续时间。</param>
    /// <param name="uncertaintyRadius">位置不确定半径。</param>
    /// <param name="source">刺激来源对象。</param>
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

    /// <summary>
    /// 报告玩家脚步声刺激。
    /// </summary>
    /// <param name="position">脚步发生位置。</param>
    /// <param name="source">产生脚步的对象。</param>
    public static void ReportFootstep(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.Footstep, position, 11f, 0.32f, 1.4f, 1.6f, source);
    }

    /// <summary>
    /// 报告枪声刺激。
    /// </summary>
    /// <param name="position">枪声发生位置。</param>
    /// <param name="source">开枪对象。</param>
    public static void ReportGunshot(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.Gunshot, position, 34f, 1f, 5f, 2.6f, source);
    }

    /// <summary>
    /// 报告投射物命中环境或目标产生的刺激。
    /// </summary>
    /// <param name="position">命中位置。</param>
    /// <param name="source">投射物来源对象。</param>
    public static void ReportProjectileImpact(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.ProjectileImpact, position, 19f, 0.72f, 3.5f, 2f, source);
    }

    /// <summary>
    /// 报告敌人受到非直接攻击时产生的刺激。
    /// </summary>
    /// <param name="position">受击位置。</param>
    /// <param name="source">刺激来源对象。</param>
    public static void ReportEnemyDamaged(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.EnemyDamaged, position, 24f, 0.88f, 4.5f, 1.2f, source);
    }

    /// <summary>
    /// 报告玩家翻找或交互战利品时产生的刺激。
    /// </summary>
    /// <param name="position">交互位置。</param>
    /// <param name="source">交互来源对象。</param>
    public static void ReportLootInteraction(Vector3 position, Transform source)
    {
        Raise(EnemySuspicionStimulusType.LootInteraction, position, 15f, 0.58f, 3f, 1.8f, source);
    }
}
