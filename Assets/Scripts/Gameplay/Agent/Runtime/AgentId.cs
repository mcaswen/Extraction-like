using System;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 运行时 Agent 标识。
    /// 使用独立值对象，避免多 Agent 场景里继续靠单例或 GameObject 名字寻址。
    /// </summary>
    [Serializable]
    public struct AgentId : IEquatable<AgentId>
    {
        [SerializeField] private string _value;

        public AgentId(string value)
        {
            _value = Normalize(value);
        }

        public static AgentId Empty => new AgentId(string.Empty);

        public string Value => _value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public static AgentId FromString(string value)
        {
            return new AgentId(value);
        }

        public static bool TryCreate(string value, out AgentId agentId)
        {
            agentId = new AgentId(value);
            return !agentId.IsEmpty;
        }

        public bool Equals(AgentId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AgentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(AgentId left, AgentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AgentId left, AgentId right)
        {
            return !left.Equals(right);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
