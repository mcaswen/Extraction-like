using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 单局原型的根运行时状态
    /// 统一承载地图 节点 多个 Agent 和全局交互门控
    /// </summary>
    [Serializable]
    public sealed class BoardGameSessionState
    {
        [SerializeField] private string _mapId;
        [SerializeField] private float _elapsedSeconds;
        [SerializeField] private int _simulationStepIndex;
        [SerializeField] private BoardSessionOutcome _outcome = BoardSessionOutcome.None;
        [SerializeField] private int _finalExtractedValue;
        [SerializeField] private string _statusMessage = "Click an AI to focus it";
        [SerializeField] private List<BoardNodeRuntimeState> _nodeStates = new List<BoardNodeRuntimeState>();
        [SerializeField] private List<BoardAgentState> _agentStates = new List<BoardAgentState>();
        [SerializeField] private string _focusedAgentId;
        [SerializeField] private string _activeLevelUpAgentId;
        [SerializeField] private List<BoardLevelUpChoice> _pendingLevelUpChoices = new List<BoardLevelUpChoice>();
        [SerializeField] private string _activeInteractionAgentId;
        [SerializeField] private string _activeLootNodeId = string.Empty;
        [SerializeField] private bool _isLootInteractionOpen;

        public BoardGameSessionState(string mapId, List<BoardAgentState> agentStates, List<BoardNodeRuntimeState> nodeStates)
        {
            _mapId = mapId;
            _agentStates = agentStates ?? new List<BoardAgentState>();
            _nodeStates = nodeStates ?? new List<BoardNodeRuntimeState>();

            if (_agentStates.Count > 0)
            {
                _focusedAgentId = _agentStates[0].AgentId;
            }
        }

        public string MapId => _mapId;

        public float ElapsedSeconds
        {
            get => _elapsedSeconds;
            set => _elapsedSeconds = Mathf.Max(0f, value);
        }

        public int SimulationStepIndex
        {
            get => _simulationStepIndex;
            set => _simulationStepIndex = Mathf.Max(0, value);
        }

        public BoardSessionOutcome Outcome
        {
            get => _outcome;
            set => _outcome = value;
        }

        public int FinalExtractedValue
        {
            get => _finalExtractedValue;
            set => _finalExtractedValue = Mathf.Max(0, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => _statusMessage = value ?? string.Empty;
        }

        public List<BoardNodeRuntimeState> NodeStates => _nodeStates;
        public List<BoardAgentState> AgentStates => _agentStates;

        /// <summary>
        /// 兼容旧入口，默认返回当前焦点 Agent
        /// </summary>
        public BoardAgentState AgentState => GetFocusedAgentState();

        public string FocusedAgentId
        {
            get => _focusedAgentId;
            set => _focusedAgentId = value ?? string.Empty;
        }

        public string ActiveLevelUpAgentId
        {
            get => _activeLevelUpAgentId;
            set => _activeLevelUpAgentId = value ?? string.Empty;
        }

        public List<BoardLevelUpChoice> PendingLevelUpChoices => _pendingLevelUpChoices;

        public int PendingLevelUpCount
        {
            get
            {
                int totalCount = 0;

                foreach (BoardAgentState agentState in _agentStates)
                {
                    if (agentState != null)
                    {
                        totalCount += agentState.PendingLevelUpCount;
                    }
                }

                return totalCount;
            }
        }

        public bool IsAwaitingLevelUpChoice => GetActiveLevelUpAgentState() != null && _pendingLevelUpChoices.Count > 0;

        public string ActiveInteractionAgentId
        {
            get => _activeInteractionAgentId;
            set => _activeInteractionAgentId = value ?? string.Empty;
        }

        public string ActiveLootNodeId
        {
            get => _activeLootNodeId;
            set => _activeLootNodeId = value ?? string.Empty;
        }

        public bool IsAwaitingLootInteraction =>
            !string.IsNullOrEmpty(_activeLootNodeId) &&
            !string.IsNullOrEmpty(_activeInteractionAgentId);

        public bool IsLootInteractionOpen
        {
            get => _isLootInteractionOpen;
            set => _isLootInteractionOpen = value;
        }

        /// <summary>
        /// 按 AgentId 查询运行时 Agent 状态
        /// </summary>
        public BoardAgentState GetAgentState(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                return null;
            }

            for (int index = 0; index < _agentStates.Count; index++)
            {
                BoardAgentState agentState = _agentStates[index];

                if (agentState != null && agentState.AgentId == agentId)
                {
                    return agentState;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取当前焦点 Agent
        /// 焦点失效时会自动回退到第一个可聚焦 Agent
        /// </summary>
        public BoardAgentState GetFocusedAgentState()
        {
            BoardAgentState focusedAgentState = GetAgentState(_focusedAgentId);

            if (focusedAgentState != null && focusedAgentState.IsFocusable)
            {
                return focusedAgentState;
            }

            focusedAgentState = FindFirstFocusableAgent();

            if (focusedAgentState != null)
            {
                _focusedAgentId = focusedAgentState.AgentId;
            }

            return focusedAgentState;
        }

        /// <summary>
        /// 获取当前升级门控归属的 Agent
        /// </summary>
        public BoardAgentState GetActiveLevelUpAgentState()
        {
            return GetAgentState(_activeLevelUpAgentId);
        }

        /// <summary>
        /// 获取当前 loot 交互归属的 Agent
        /// </summary>
        public BoardAgentState GetActiveInteractionAgentState()
        {
            return GetAgentState(_activeInteractionAgentId);
        }

        /// <summary>
        /// 清理失效的焦点和全局交互归属
        /// 让外部控制器可以在每帧推进前做一次统一收口
        /// </summary>
        public void EnsureConsistentReferences()
        {
            BoardAgentState focusedAgentState = GetFocusedAgentState();

            if (focusedAgentState == null)
            {
                _focusedAgentId = string.Empty;
            }

            if (GetActiveLevelUpAgentState() == null)
            {
                _activeLevelUpAgentId = string.Empty;
                _pendingLevelUpChoices.Clear();
            }

            if (GetActiveInteractionAgentState() == null)
            {
                _activeInteractionAgentId = string.Empty;
                _activeLootNodeId = string.Empty;
                _isLootInteractionOpen = false;
            }
        }

        private BoardAgentState FindFirstFocusableAgent()
        {
            for (int index = 0; index < _agentStates.Count; index++)
            {
                BoardAgentState agentState = _agentStates[index];

                if (agentState != null && agentState.IsFocusable)
                {
                    return agentState;
                }
            }

            for (int index = 0; index < _agentStates.Count; index++)
            {
                if (_agentStates[index] != null)
                {
                    return _agentStates[index];
                }
            }

            return null;
        }
    }
}
