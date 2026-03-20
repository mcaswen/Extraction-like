using System;
using System.Collections.Generic;

namespace Core.BehaviorTree.Blackboard
{
    /// <summary>
    /// 行为树/分层状态机框架的全局共享变量/参数表
    /// 存储黑板所有键值对集合，并提供事件发布/增删改查方法
    /// </summary>
    public sealed class BehaviorBlackboard
    {
        private readonly Dictionary<BlackboardKey, BlackboardEntry> _entriesByKeys =
            new Dictionary<BlackboardKey, BlackboardEntry>();

        private int _globalVersion;

        public event EventHandler<BlackboardValueChangedEventArgs> ValueChanged;

        /// <summary>
        /// 检查黑板中是否存在指定键
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(BlackboardKey key)
        {
            return _entriesByKeys.ContainsKey(key);
        }

        /// <summary>
        /// 尝试从黑板中获取指定键的值, 若不存在则返回 false 和默认值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue<T>(BlackboardKey key, out T value)
        {
            if (_entriesByKeys.TryGetValue(key, out BlackboardEntry entry))
            {
                if (entry.Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                if (entry.Value == null && default(T) == null)
                {
                    value = default;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 直接从黑板中获取指定键的值, 若不存在则返回默认值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetValueOrDefault<T>(BlackboardKey key, T defaultValue = default)
        {
            return TryGetValue(key, out T value) ? value : defaultValue;
        }

        /// <summary>
        /// 设置黑板中指定键的值, 若键不存在则添加新键值对
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timestamp"></param>
        public void SetValue<T>(BlackboardKey key, T value, double timestamp)
        {
            object oldValue = null;
            bool hasOldValue = _entriesByKeys.TryGetValue(key, out BlackboardEntry entry);

            if (!hasOldValue)
            {
                entry = new BlackboardEntry();
                _entriesByKeys.Add(key, entry);
            }
            else
            {
                oldValue = entry.Value;
            }

            if (hasOldValue && Equals(oldValue, value))
            {
                return;
            }

            _globalVersion++;

            entry.Value = value;
            entry.ValueType = typeof(T);
            entry.Version = _globalVersion;
            entry.Timestamp = timestamp;

            ValueChanged?.Invoke(
                this,
                new BlackboardValueChangedEventArgs(
                    key,
                    oldValue,
                    value,
                    entry.Version,
                    entry.Timestamp));
        }

        /// <summary>
        /// 从黑板中移除指定键的值, 若键不存在则返回 false
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public bool RemoveValue(BlackboardKey key, double timestamp)
        {
            if (!_entriesByKeys.TryGetValue(key, out BlackboardEntry entry))
            {
                return false;
            }

            object oldValue = entry.Value;
            _entriesByKeys.Remove(key);
            _globalVersion++;

            ValueChanged?.Invoke(
                this,
                new BlackboardValueChangedEventArgs(
                    key,
                    oldValue,
                    null,
                    _globalVersion,
                    timestamp));

            return true;
        }

        /// <summary>
        /// 清空黑板中所有键值对, 并触发对应的移除事件
        /// </summary>
        /// <param name="timestamp"></param>
        public void Clear(double timestamp)
        {
            if (_entriesByKeys.Count == 0)
            {
                return;
            }

            var keys = new List<BlackboardKey>(_entriesByKeys.Keys);
            for (int index = 0; index < keys.Count; index++)
            {
                RemoveValue(keys[index], timestamp);
            }
        }
    }
}