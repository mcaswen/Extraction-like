using System;

namespace Core.BehaviorTree.Blackboard
{
    /// <summary>
    /// 单个黑板键的结构体定义
    /// </summary>
    [Serializable]
    public readonly struct BlackboardKey : IEquatable<BlackboardKey>
    {
        public readonly string Name;

        public BlackboardKey(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public bool Equals(BlackboardKey other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BlackboardKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;
        }

        public override string ToString()
        {
            return Name;
        }

        public static implicit operator BlackboardKey(string name)
        {
            return new BlackboardKey(name);
        }
    }
}