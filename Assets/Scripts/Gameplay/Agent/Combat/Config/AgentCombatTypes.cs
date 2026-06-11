using System;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗元素类型
    /// </summary>
    public enum AgentCombatElementType
    {
        Physical,
        Fire,
        Ice,
        Earth
    }

    /// <summary>
    /// Agent 战斗武器类型
    /// </summary>
    public enum AgentCombatWeaponType
    {
        None,
        Catalyst,
        Sword
    }

    /// <summary>
    /// 战斗数值缩放来源
    /// </summary>
    public enum AgentCombatStatScalingSource
    {
        Attack,
        Defense
    }

    /// <summary>
    /// Agent 战斗运行时属性快照
    /// </summary>
    public readonly struct AgentCombatRuntimeStats
    {
        /// <summary>
        /// 创建战斗运行时属性快照
        /// </summary>
        /// <param name="maxHealth"></param>
        /// <param name="attack"></param>
        /// <param name="defense"></param>
        public AgentCombatRuntimeStats(int maxHealth, float attack, float defense)
        {
            MaxHealth = Mathf.Max(1, maxHealth);
            Attack = Mathf.Max(0f, attack);
            Defense = Mathf.Max(0f, defense);
        }

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHealth { get; }

        /// <summary>
        /// 攻击属性
        /// </summary>
        public float Attack { get; }

        /// <summary>
        /// 防御属性
        /// </summary>
        public float Defense { get; }

        /// <summary>
        /// 根据缩放来源读取对应属性值
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public float GetValue(AgentCombatStatScalingSource source)
        {
            return source == AgentCombatStatScalingSource.Defense ? Defense : Attack;
        }
    }

    /// <summary>
    /// 战斗属性缩放配置
    /// </summary>
    [Serializable]
    public struct AgentCombatStatScaling
    {
        [SerializeField] private AgentCombatStatScalingSource _source;
        [SerializeField, Min(0f)] private float _multiplier;

        /// <summary>
        /// 创建战斗属性缩放配置
        /// </summary>
        /// <param name="source"></param>
        /// <param name="multiplier"></param>
        public AgentCombatStatScaling(AgentCombatStatScalingSource source, float multiplier)
        {
            _source = source;
            _multiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// 属性来源
        /// </summary>
        public AgentCombatStatScalingSource Source => _source;

        /// <summary>
        /// 缩放倍率
        /// </summary>
        public float Multiplier => Mathf.Max(0f, _multiplier);

        /// <summary>
        /// 根据运行时属性计算缩放结果
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public float Evaluate(AgentCombatRuntimeStats stats)
        {
            return stats.GetValue(_source) * Multiplier;
        }
    }

    /// <summary>
    /// 子弹附加状态配置
    /// </summary>
    public readonly struct AgentCombatProjectileStatus
    {
        /// <summary>
        /// 创建子弹附加状态配置
        /// </summary>
        /// <param name="slowMultiplier"></param>
        /// <param name="slowDurationSeconds"></param>
        /// <param name="freezeDurationSeconds"></param>
        public AgentCombatProjectileStatus(
            float slowMultiplier,
            float slowDurationSeconds,
            float freezeDurationSeconds)
        {
            SlowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
            SlowDurationSeconds = Mathf.Max(0f, slowDurationSeconds);
            FreezeDurationSeconds = Mathf.Max(0f, freezeDurationSeconds);
        }

        /// <summary>
        /// 减速倍率
        /// </summary>
        public float SlowMultiplier { get; }

        /// <summary>
        /// 减速持续时间
        /// </summary>
        public float SlowDurationSeconds { get; }

        /// <summary>
        /// 冰冻持续时间
        /// </summary>
        public float FreezeDurationSeconds { get; }

        /// <summary>
        /// 无附加状态
        /// </summary>
        public static AgentCombatProjectileStatus None => new AgentCombatProjectileStatus(1f, 0f, 0f);
    }
}
