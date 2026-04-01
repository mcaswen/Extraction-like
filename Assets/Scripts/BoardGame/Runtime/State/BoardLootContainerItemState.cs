using System;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 节点战利品容器中的单个物品状态�
    /// </summary>
    [Serializable]
    public sealed class BoardLootContainerItemState
    {
        [SerializeField] private BoardItemInstance _itemInstance;
        [SerializeField] private int _gridX;
        [SerializeField] private int _gridY;
        [SerializeField] private bool _isRotated;
        [SerializeField] private int _revealSequenceIndex;
        [SerializeField] private bool _isRevealed;
        [SerializeField] private float _revealProgressSeconds;
        [SerializeField] private float _revealDurationSeconds;

        public BoardLootContainerItemState(
            BoardItemInstance itemInstance,
            int gridX,
            int gridY,
            int revealSequenceIndex,
            float revealDurationSeconds)
        {
            _itemInstance = itemInstance;
            _gridX = gridX;
            _gridY = gridY;
            _revealSequenceIndex = revealSequenceIndex;
            _revealDurationSeconds = Mathf.Max(0.01f, revealDurationSeconds);
        }

        public BoardItemInstance ItemInstance
        {
            get => _itemInstance;
            set => _itemInstance = value;
        }

        public int GridX
        {
            get => _gridX;
            set => _gridX = Mathf.Max(0, value);
        }

        public int GridY
        {
            get => _gridY;
            set => _gridY = Mathf.Max(0, value);
        }

        public bool IsRotated
        {
            get => _isRotated;
            set => _isRotated = value;
        }

        public int RevealSequenceIndex
        {
            get => _revealSequenceIndex;
            set => _revealSequenceIndex = Mathf.Max(0, value);
        }

        public bool IsRevealed
        {
            get => _isRevealed;
            set
            {
                _isRevealed = value;

                if (_isRevealed)
                {
                    _revealProgressSeconds = _revealDurationSeconds;
                }
            }
        }

        public float RevealProgressSeconds
        {
            get => _revealProgressSeconds;
            set => _revealProgressSeconds = Mathf.Clamp(value, 0f, _revealDurationSeconds);
        }

        public float RevealDurationSeconds
        {
            get => _revealDurationSeconds;
            set => _revealDurationSeconds = Mathf.Max(0.01f, value);
        }

        public float RevealProgress01 => _revealDurationSeconds <= Mathf.Epsilon
            ? 1f
            : Mathf.Clamp01(_revealProgressSeconds / _revealDurationSeconds);

        public bool AdvanceReveal(float deltaTime)
        {
            if (_isRevealed)
            {
                return false;
            }

            _revealProgressSeconds = Mathf.Min(_revealProgressSeconds + Mathf.Max(0f, deltaTime), _revealDurationSeconds);

            if (_revealProgressSeconds < _revealDurationSeconds)
            {
                return false;
            }

            _isRevealed = true;
            _revealProgressSeconds = _revealDurationSeconds;
            return true;
        }
    }
}
