using System;
using UnityEngine;

namespace Gameplay.Targets.Data
{
    /// <summary>
    /// 群目标中的具体实体成员
    /// 保存实体引用、稳定 ID 和接触完成状态
    /// </summary>
    [Serializable]
    public sealed class GameplayTargetEntityMember
    {
        [SerializeField] private string _entityId;
        [SerializeField] private GameObject _entityObject;
        [SerializeField] private GameplayTargetState _state = new GameplayTargetState();

        public GameplayTargetEntityMember()
        {
        }

        public GameplayTargetEntityMember(string entityId, GameObject entityObject)
        {
            _entityId = entityId;
            _entityObject = entityObject;
            _state = new GameplayTargetState();
        }

        public string EntityId => _entityId ?? string.Empty;
        public GameObject EntityObject => _entityObject;
        public GameplayTargetState State => _state;
        public bool HasBeenTouched => _state != null && _state.HasBeenTouched;
        public bool HasBeenCompleted => _state != null && _state.HasBeenCompleted;
        public Vector3 Position => _entityObject != null ? _entityObject.transform.position : Vector3.zero;

        /// <summary>
        /// 设置成员绑定的场景对象
        /// </summary>
        /// <param name="entityObject"></param>
        public void SetEntityObject(GameObject entityObject)
        {
            _entityObject = entityObject;
        }

        /// <summary>
        /// 确保成员拥有稳定 ID
        /// </summary>
        /// <param name="ownerId"></param>
        /// <param name="index"></param>
        public void EnsureEntityId(string ownerId, int index)
        {
            if (!string.IsNullOrWhiteSpace(_entityId))
            {
                _entityId = _entityId.Trim();
                return;
            }

            string safeOwnerId = string.IsNullOrWhiteSpace(ownerId) ? "Target" : ownerId.Trim();
            _entityId = $"{safeOwnerId}_Entity_{index}";
        }

        /// <summary>
        /// 判断传入对象是否属于当前成员
        /// </summary>
        /// <param name="targetObject"></param>
        /// <returns></returns>
        public bool Matches(GameObject targetObject)
        {
            if (_entityObject == null || targetObject == null)
                return false;

            return _entityObject == targetObject ||
                   targetObject.transform.IsChildOf(_entityObject.transform) ||
                   _entityObject.transform.IsChildOf(targetObject.transform);
        }

        /// <summary>
        /// 从成员对象、父级或子级查找指定组件
        /// </summary>
        /// <param name="component"></param>
        /// <typeparam name="TComponent"></typeparam>
        /// <returns></returns>
        public bool TryGetComponent<TComponent>(out TComponent component)
            where TComponent : Component
        {
            component = null;
            if (_entityObject == null)
                return false;

            component = _entityObject.GetComponent<TComponent>();
            if (component != null)
                return true;

            component = _entityObject.GetComponentInParent<TComponent>();
            if (component != null)
                return true;

            component = _entityObject.GetComponentInChildren<TComponent>();
            return component != null;
        }

        /// <summary>
        /// 标记成员已经被接触
        /// </summary>
        public void MarkTouched()
        {
            EnsureState();
            _state.MarkTouched();
        }

        /// <summary>
        /// 标记成员已经完成
        /// </summary>
        public void MarkCompleted()
        {
            EnsureState();
            _state.MarkCompleted();
        }

        /// <summary>
        /// 设置成员完成状态
        /// </summary>
        /// <param name="hasBeenCompleted"></param>
        public void SetCompleted(bool hasBeenCompleted)
        {
            EnsureState();
            _state.SetCompleted(hasBeenCompleted);
        }

        // 兼容旧序列化数据中状态字段为空的情况
        private void EnsureState()
        {
            if (_state == null)
                _state = new GameplayTargetState();
        }
    }
}
