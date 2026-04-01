using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 单局原型的根运行时状态
    /// </summary>
    [Serializable]
    public sealed class BoardGameSessionState
    {
        [SerializeField] private string _mapId;
        [SerializeField] private float _elapsedSeconds;
        [SerializeField] private BoardSessionOutcome _outcome = BoardSessionOutcome.None;
        [SerializeField] private int _finalExtractedValue;
        [SerializeField] private string _statusMessage = "Click the AI to enter redirect mode";
        [SerializeField] private List<BoardNodeRuntimeState> _nodeStates = new List<BoardNodeRuntimeState>();
        [SerializeField] private BoardAgentState _agentState;
        [SerializeField] private bool _isAwaitingLevelUpChoice;
        [SerializeField] private List<BoardLevelUpChoice> _pendingLevelUpChoices = new List<BoardLevelUpChoice>();
        [SerializeField] private int _pendingLevelUpCount;
        [SerializeField] private string _activeLootNodeId = string.Empty;
        [SerializeField] private bool _isLootInteractionOpen;

        public BoardGameSessionState(string mapId, BoardAgentState agentState, List<BoardNodeRuntimeState> nodeStates)
        {
            _mapId = mapId;
            _agentState = agentState;
            _nodeStates = nodeStates ?? new List<BoardNodeRuntimeState>();
        }

        public string MapId => _mapId;

        public float ElapsedSeconds
        {
            get => _elapsedSeconds;
            set => _elapsedSeconds = Mathf.Max(0f, value);
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
        public BoardAgentState AgentState => _agentState;

        public bool IsAwaitingLevelUpChoice
        {
            get => _isAwaitingLevelUpChoice;
            set => _isAwaitingLevelUpChoice = value;
        }

        public List<BoardLevelUpChoice> PendingLevelUpChoices => _pendingLevelUpChoices;

        public int PendingLevelUpCount
        {
            get => _pendingLevelUpCount;
            set => _pendingLevelUpCount = Mathf.Max(0, value);
        }

        public string ActiveLootNodeId
        {
            get => _activeLootNodeId;
            set => _activeLootNodeId = value ?? string.Empty;
        }

        public bool IsAwaitingLootInteraction => !string.IsNullOrEmpty(_activeLootNodeId);

        public bool IsLootInteractionOpen
        {
            get => _isLootInteractionOpen;
            set => _isLootInteractionOpen = value;
        }
    }
}
