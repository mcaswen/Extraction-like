using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 角色运行时状态，记录移动、目标、动作、血量和背包等数据
    /// </summary>
    [Serializable]
    public sealed class BoardAgentState
    {
        [SerializeField] private string _currentNodeId;
        [SerializeField] private string _previousNodeId;
        [SerializeField] private string _currentTargetNodeId;
        [SerializeField] private List<string> _remainingPathNodeIds = new List<string>();
        [SerializeField] private BoardActionType _currentActionType = BoardActionType.Idle;
        [SerializeField] private float _currentActionProgress;
        [SerializeField] private float _currentActionDuration = 1f;
        [SerializeField] private float _currentActionAccumulatorSeconds;
        [SerializeField] private int _currentHealth = 100;
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _attack = 7;
        [SerializeField] private int _defense;
        [SerializeField] private int _level = 1;
        [SerializeField] private int _currentExperience;
        [SerializeField] private int _requiredExperienceToNextLevel = 50;
        [SerializeField] private Vector2 _worldPosition;
        [SerializeField] private BoardInventoryState _inventoryState = new BoardInventoryState(9f);
        [SerializeField] private BoardIntentSource _intentSource = BoardIntentSource.Autonomous;

        [SerializeField] private string _currentEdgeId;
        [SerializeField] private string _currentEdgeFromNodeId;
        [SerializeField] private string _currentEdgeToNodeId;
        [SerializeField] private float _currentEdgeLengthUnits;
        [SerializeField] private float _currentEdgeProgress01;
        [SerializeField] private float _currentEdgeSegmentStartProgress01;
        [SerializeField] private float _currentEdgeTargetProgress01 = 1f;

        [SerializeField] private float _autonomousDecisionElapsedSeconds;

        public BoardAgentState(int maxHealth, int attack, int defense, float maxCapacity = 9f)
        {
            _currentHealth = maxHealth;
            _maxHealth = maxHealth;
            _attack = attack;
            _defense = defense;
            _inventoryState = new BoardInventoryState(maxCapacity);
        }

        public string CurrentNodeId
        {
            get => _currentNodeId;
            set => _currentNodeId = value;
        }

        public string PreviousNodeId
        {
            get => _previousNodeId;
            set => _previousNodeId = value;
        }

        public string CurrentTargetNodeId
        {
            get => _currentTargetNodeId;
            set => _currentTargetNodeId = value;
        }

        public List<string> RemainingPathNodeIds => _remainingPathNodeIds;

        public BoardActionType CurrentActionType
        {
            get => _currentActionType;
            set => _currentActionType = value;
        }

        public float CurrentActionProgress
        {
            get => _currentActionProgress;
            set => _currentActionProgress = Mathf.Clamp01(value);
        }

        public float CurrentActionDuration
        {
            get => _currentActionDuration;
            set => _currentActionDuration = Mathf.Max(0.01f, value);
        }

        public float CurrentActionAccumulatorSeconds
        {
            get => _currentActionAccumulatorSeconds;
            set => _currentActionAccumulatorSeconds = Mathf.Max(0f, value);
        }

        public int CurrentHealth
        {
            get => _currentHealth;
            set => _currentHealth = Mathf.Clamp(value, 0, _maxHealth);
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = Mathf.Max(1, value);
        }

        public int Attack
        {
            get => _attack;
            set => _attack = Mathf.Max(1, value);
        }

        public int Defense
        {
            get => _defense;
            set => _defense = Mathf.Max(0, value);
        }

        public int Level
        {
            get => _level;
            set => _level = Mathf.Max(1, value);
        }

        public int CurrentExperience
        {
            get => _currentExperience;
            set => _currentExperience = Mathf.Max(0, value);
        }

        public int RequiredExperienceToNextLevel
        {
            get => _requiredExperienceToNextLevel;
            set => _requiredExperienceToNextLevel = Mathf.Max(1, value);
        }

        public Vector2 WorldPosition
        {
            get => _worldPosition;
            set => _worldPosition = value;
        }

        public BoardInventoryState InventoryState => _inventoryState;

        public BoardIntentSource IntentSource
        {
            get => _intentSource;
            set => _intentSource = value;
        }

        public string CurrentEdgeId
        {
            get => _currentEdgeId;
            set => _currentEdgeId = value;
        }

        public string CurrentEdgeFromNodeId
        {
            get => _currentEdgeFromNodeId;
            set => _currentEdgeFromNodeId = value;
        }

        public string CurrentEdgeToNodeId
        {
            get => _currentEdgeToNodeId;
            set => _currentEdgeToNodeId = value;
        }

        public float CurrentEdgeLengthUnits
        {
            get => _currentEdgeLengthUnits;
            set => _currentEdgeLengthUnits = Mathf.Max(0f, value);
        }

        public float CurrentEdgeProgress01
        {
            get => _currentEdgeProgress01;
            set => _currentEdgeProgress01 = Mathf.Clamp01(value);
        }

        public float CurrentEdgeSegmentStartProgress01
        {
            get => _currentEdgeSegmentStartProgress01;
            set => _currentEdgeSegmentStartProgress01 = Mathf.Clamp01(value);
        }

        public float CurrentEdgeTargetProgress01
        {
            get => _currentEdgeTargetProgress01;
            set => _currentEdgeTargetProgress01 = Mathf.Clamp01(value);
        }

        public float AutonomousDecisionElapsedSeconds
        {
            get => _autonomousDecisionElapsedSeconds;
            set => _autonomousDecisionElapsedSeconds = Mathf.Max(0f, value);
        }

        public bool IsOnEdge => !string.IsNullOrEmpty(_currentEdgeId);
        public bool IsAlive => _currentHealth > 0;

        /// <summary>
        /// 清空边上移动缓存，把角色状态收回到节点态
        /// </summary>
        public void ClearEdgeTravel()
        {
            _currentEdgeId = string.Empty;
            _currentEdgeFromNodeId = string.Empty;
            _currentEdgeToNodeId = string.Empty;
            _currentEdgeLengthUnits = 0f;
            _currentEdgeProgress01 = 0f;
            _currentEdgeSegmentStartProgress01 = 0f;
            _currentEdgeTargetProgress01 = 1f;
        }
    }
}
