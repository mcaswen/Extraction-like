using System;
using UnityEngine;

namespace Gameplay.Targets.Data
{
    /// <summary>
    /// Gameplay 目标运行时状态
    /// 用统一的已接触和已完成标记承载三层目标状态
    /// </summary>
    [Serializable]
    public sealed class GameplayTargetState
    {
        [SerializeField] private bool _hasBeenTouched;
        [SerializeField] private bool _hasBeenCompleted;

        public bool HasBeenTouched => _hasBeenTouched;
        public bool HasBeenCompleted => _hasBeenCompleted;

        /// <summary>
        /// 设置目标接触状态
        /// </summary>
        /// <param name="hasBeenTouched"></param>
        public void SetTouched(bool hasBeenTouched)
        {
            _hasBeenTouched = hasBeenTouched;
        }

        /// <summary>
        /// 设置目标完成状态
        /// </summary>
        /// <param name="hasBeenCompleted"></param>
        public void SetCompleted(bool hasBeenCompleted)
        {
            _hasBeenCompleted = hasBeenCompleted;
        }

        /// <summary>
        /// 标记目标已经被接触
        /// </summary>
        public void MarkTouched()
        {
            _hasBeenTouched = true;
        }

        /// <summary>
        /// 标记目标已经完成
        /// </summary>
        public void MarkCompleted()
        {
            _hasBeenCompleted = true;
        }

        /// <summary>
        /// 重置目标状态
        /// </summary>
        public void Reset()
        {
            _hasBeenTouched = false;
            _hasBeenCompleted = false;
        }
    }
}
