using System;
using Gameplay.Targets.Data;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// Gameplay 目标场景配置基类
    /// 负责稳定 ID、基础状态和注册表生命周期
    /// </summary>
    public abstract class GameplayTargetAuthoringBase : MonoBehaviour
    {
        [SerializeField] private string _targetId;
        [SerializeField] private string _displayName;
        [SerializeField] private GameplayTargetState _state = new GameplayTargetState();

        public string TargetId => _targetId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public GameplayTargetState State => _state;
        public bool HasBeenTouched => _state != null && _state.HasBeenTouched;
        public bool HasBeenCompleted => _state != null && _state.HasBeenCompleted;
        public virtual Vector3 CenterPosition => transform.position;

        public abstract GameplayTargetLevel TargetLevel { get; }
        public abstract GameplayTargetKind TargetKind { get; }

        protected virtual string IdPrefix => TargetKind.ToString();

        protected virtual void Reset()
        {
            EnsureTargetId();
        }

        protected virtual void OnValidate()
        {
            EnsureTargetId();
            EnsureState();
        }

        protected virtual void OnEnable()
        {
            EnsureTargetId();
            EnsureState();
            GameplayTargetRegistry.GetOrCreate().RegisterTarget(this);
        }

        protected virtual void OnDisable()
        {
            GameplayTargetRegistry activeRegistry = GameplayTargetRegistry.ActiveInstance;
            if (activeRegistry != null)
                activeRegistry.UnregisterTarget(this);
        }

        /// <summary>
        /// 标记当前目标已经被接触
        /// </summary>
        public void MarkTouched()
        {
            EnsureState();
            _state.MarkTouched();
            NotifyStateChanged();
        }

        /// <summary>
        /// 标记当前目标已经完成
        /// </summary>
        public void MarkCompleted()
        {
            EnsureState();
            _state.MarkCompleted();
            NotifyStateChanged();
        }

        /// <summary>
        /// 设置当前目标的接触状态
        /// </summary>
        /// <param name="hasBeenTouched"></param>
        public void SetTouched(bool hasBeenTouched)
        {
            EnsureState();
            if (_state.HasBeenTouched == hasBeenTouched)
                return;

            _state.SetTouched(hasBeenTouched);
            NotifyStateChanged();
        }

        /// <summary>
        /// 设置当前目标的完成状态
        /// </summary>
        /// <param name="hasBeenCompleted"></param>
        public void SetCompleted(bool hasBeenCompleted)
        {
            EnsureState();
            if (_state.HasBeenCompleted == hasBeenCompleted)
                return;

            _state.SetCompleted(hasBeenCompleted);
            NotifyStateChanged();
        }

        /// <summary>
        /// 重置当前目标的运行时状态
        /// </summary>
        public void ResetState()
        {
            EnsureState();
            _state.Reset();
            NotifyStateChanged();
        }

        [ContextMenu("Generate Missing Target Id")]
        private void GenerateMissingTargetIdFromMenu()
        {
            EnsureTargetId();
        }

        // 保证目标在编辑器和运行时都有稳定 ID
        protected void EnsureTargetId()
        {
            if (!string.IsNullOrWhiteSpace(_targetId))
            {
                _targetId = _targetId.Trim();
                return;
            }

            _targetId = $"{IdPrefix}_{Guid.NewGuid():N}";
        }

        // Prefab 群目标实例可能继承同一个序列化 ID，运行时遇到冲突时为实例补一个新 ID
        internal void RegenerateTargetIdForDuplicate()
        {
            _targetId = $"{IdPrefix}_{Guid.NewGuid():N}";
        }

        // 兼容旧序列化数据中状态字段为空的情况
        protected void EnsureState()
        {
            if (_state == null)
                _state = new GameplayTargetState();
        }

        // 目标状态变化后通知注册表刷新上层聚合状态
        protected virtual void NotifyStateChanged()
        {
            GameplayTargetRegistry.ActiveInstance?.NotifyTargetStateChanged(this);
        }
    }
}
