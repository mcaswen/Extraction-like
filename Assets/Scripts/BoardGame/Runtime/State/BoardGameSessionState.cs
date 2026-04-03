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
        // 本局使用的地图 ID
        [SerializeField] private string _mapId;
        // 本局已运行时长
        [SerializeField] private float _elapsedSeconds;
        // 对局结果
        [SerializeField] private BoardSessionOutcome _outcome = BoardSessionOutcome.None;
        // 成功撤离时最终带出的总价值
        [SerializeField] private int _finalExtractedValue;
        // 当前对局状态提示文案
        [SerializeField] private string _statusMessage = "Hover and click a node to redirect the AI target";
        // 全部节点运行时状态列表
        [SerializeField] private List<BoardNodeRuntimeState> _nodeStates = new List<BoardNodeRuntimeState>();
        // 角色运行时状态
        [SerializeField] private BoardAgentState _agentState;
        // 当前是否正在等待升级选择
        [SerializeField] private bool _isAwaitingLevelUpChoice;
        // 当前待选升级项
        [SerializeField] private List<BoardLevelUpChoice> _pendingLevelUpChoices = new List<BoardLevelUpChoice>();
        // 已经升级但尚未结算增益的次数
        [SerializeField] private int _pendingLevelUpCount;
        // 当前卡住 AI 的战利品节点
        [SerializeField] private string _activeLootNodeId = string.Empty;
        // 战利品 UI 当前是否处于打开状态
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
