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
        // 角色当前站立的节点 ID；若位于边上则为空
        [SerializeField] private string _currentNodeId;
        // 上一个完成抵达的节点 ID，用于描述移动来源
        [SerializeField] private string _previousNodeId;
        // 当前最终目标节点 ID
        [SerializeField] private string _currentTargetNodeId;
        // 从当前位置到目标节点尚未走完的路径节点序列
        [SerializeField] private List<string> _remainingPathNodeIds = new List<string>();
        // 角色当前动作类型
        [SerializeField] private BoardActionType _currentActionType = BoardActionType.Idle;
        // 当前动作进度，范围 0 到 1
        [SerializeField] private float _currentActionProgress;
        // 当前动作总时长
        [SerializeField] private float _currentActionDuration = 1f;
        // 当前动作的内部累计计时，用于战斗 tick 等逻辑
        [SerializeField] private float _currentActionAccumulatorSeconds;
        // 当前生命值
        [SerializeField] private int _currentHealth = 100;
        // 最大生命值
        [SerializeField] private int _maxHealth = 100;
        // 当前攻击力
        [SerializeField] private int _attack = 7;
        // 当前防御力
        [SerializeField] private int _defense;
        // 当前等级
        [SerializeField] private int _level = 1;
        // 当前经验值
        [SerializeField] private int _currentExperience;
        // 升至下一级所需经验值
        [SerializeField] private int _requiredExperienceToNextLevel = 50;
        // 当前世界坐标，用于地图表现层插值显示
        [SerializeField] private Vector2 _worldPosition;
        // 当前背包与收益状态
        [SerializeField] private BoardInventoryState _inventoryState = new BoardInventoryState();
        // 当前目标来源，区分 AI 默认意图和玩家改写意图
        [SerializeField] private BoardIntentSource _intentSource = BoardIntentSource.Autonomous;

        // 当前所在边 ID；不为空表示角色位于边上
        [SerializeField] private string _currentEdgeId;
        // 当前边起点节点 ID
        [SerializeField] private string _currentEdgeFromNodeId;
        // 当前边终点节点 ID
        [SerializeField] private string _currentEdgeToNodeId;
        // 当前边长度
        [SerializeField] private float _currentEdgeLengthUnits;
        // 当前在边上的归一化进度
        [SerializeField] private float _currentEdgeProgress01;
        // 当前这一段移动开始时的边进度
        [SerializeField] private float _currentEdgeSegmentStartProgress01;
        // 当前这一段移动希望抵达的边进度
        [SerializeField] private float _currentEdgeTargetProgress01 = 1f;

        // 距离上次默认决策后的累计时间
        [SerializeField] private float _autonomousDecisionElapsedSeconds;

        public BoardAgentState(int maxHealth, int attack, int defense)
        {
            _currentHealth = maxHealth;
            _maxHealth = maxHealth;
            _attack = attack;
            _defense = defense;
            _inventoryState = new BoardInventoryState();
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
