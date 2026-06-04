using System;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 运行时 Agent 标识
    /// 使用独立值对象，避免多 Agent 场景里继续靠单例或 GameObject 名字寻址
    /// </summary>
    [Serializable]
    public struct AgentId : IEquatable<AgentId>
    {
        [SerializeField] private string _value;

        /// <summary>
        /// 创建一个运行时 Agent 标识
        /// </summary>
        /// <param name="value"></param>
        public AgentId(string value)
        {
            _value = Normalize(value);
        }

        /// <summary>
        /// 空 AgentId
        /// 用于表示未指定目标 Agent
        /// </summary>
        public static AgentId Empty => new AgentId(string.Empty);

        /// <summary>
        /// 规范化后的 AgentId 字符串
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// 当前 AgentId 是否为空
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        /// <summary>
        /// 从字符串创建 AgentId
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static AgentId FromString(string value)
        {
            return new AgentId(value);
        }

        /// <summary>
        /// 尝试从字符串创建非空 AgentId
        /// </summary>
        /// <param name="value"></param>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public static bool TryCreate(string value, out AgentId agentId)
        {
            agentId = new AgentId(value);
            return !agentId.IsEmpty;
        }

        /// <summary>
        /// 判断两个 AgentId 是否代表同一个运行时标识
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(AgentId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断对象是否为相同 AgentId
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj is AgentId other && Equals(other);
        }

        /// <summary>
        /// 获取 AgentId 的哈希值
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <summary>
        /// 返回 AgentId 字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// 判断两个 AgentId 是否相等
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(AgentId left, AgentId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 AgentId 是否不相等
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
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
