using System;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public enum AgentCombatElementType
    {
        Physical,
        Fire,
        Ice,
        Earth
    }

    public enum AgentCombatWeaponType
    {
        None,
        Catalyst,
        Sword
    }

    public enum AgentCombatStatScalingSource
    {
        Attack,
        Defense
    }

    public readonly struct AgentCombatRuntimeStats
    {
        public AgentCombatRuntimeStats(int maxHealth, float attack, float defense)
        {
            MaxHealth = Mathf.Max(1, maxHealth);
            Attack = Mathf.Max(0f, attack);
            Defense = Mathf.Max(0f, defense);
        }

        public int MaxHealth { get; }
        public float Attack { get; }
        public float Defense { get; }

        public float GetValue(AgentCombatStatScalingSource source)
        {
            return source == AgentCombatStatScalingSource.Defense ? Defense : Attack;
        }
    }

    [Serializable]
    public struct AgentCombatStatScaling
    {
        [SerializeField] private AgentCombatStatScalingSource _source;
        [SerializeField, Min(0f)] private float _multiplier;

        public AgentCombatStatScaling(AgentCombatStatScalingSource source, float multiplier)
        {
            _source = source;
            _multiplier = Mathf.Max(0f, multiplier);
        }

        public AgentCombatStatScalingSource Source => _source;
        public float Multiplier => Mathf.Max(0f, _multiplier);

        public float Evaluate(AgentCombatRuntimeStats stats)
        {
            return stats.GetValue(_source) * Multiplier;
        }
    }

    public readonly struct AgentCombatProjectileStatus
    {
        public AgentCombatProjectileStatus(
            float slowMultiplier,
            float slowDurationSeconds,
            float freezeDurationSeconds)
        {
            SlowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
            SlowDurationSeconds = Mathf.Max(0f, slowDurationSeconds);
            FreezeDurationSeconds = Mathf.Max(0f, freezeDurationSeconds);
        }

        public float SlowMultiplier { get; }
        public float SlowDurationSeconds { get; }
        public float FreezeDurationSeconds { get; }

        public static AgentCombatProjectileStatus None => new AgentCombatProjectileStatus(1f, 0f, 0f);
    }
}
