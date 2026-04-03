using System;
using System.Collections.Generic;
using System.Linq;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 原型总控，负责创建运行时状态、对外暴露只读查询，并协调输入与表现层
    /// </summary>
    public sealed class BoardGamePrototypeController
    {
        private readonly SO_BoardGame_MapDefinition _mapDefinition;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly SO_BoardGame_LootTableSet _lootTableSet;
        private readonly BoardGameBagLayoutSettings _bagLayoutSettings;

        private readonly BoardGraphService _graphService;
        private readonly BoardPathfindingService _pathfindingService;
        private readonly BoardCombatResolutionService _combatResolutionService;
        private readonly BoardLootResolutionService _lootResolutionService;
        private readonly BoardProgressionService _progressionService;
        private readonly BoardAgentDecisionService _decisionService;
        private readonly BoardInterruptService _interruptService;
        private readonly BoardAgentActionStateMachine _actionStateMachine;
        private readonly Dictionary<string, BoardNodeRuntimeState> _nodeStatesById =
            new Dictionary<string, BoardNodeRuntimeState>();

        private readonly BoardGameSessionState _sessionState;

        private string _selectedNodeId = string.Empty;

        public BoardGamePrototypeController(
            SO_BoardGame_MapDefinition mapDefinition,
            SO_BoardGame_RuleSet ruleSet,
            SO_BoardGame_LootTableSet lootTableSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _mapDefinition = mapDefinition;
            _ruleSet = ruleSet;
            _lootTableSet = lootTableSet;
            _bagLayoutSettings = bagLayoutSettings ?? new BoardGameBagLayoutSettings();

            _graphService = new BoardGraphService(mapDefinition);
            _pathfindingService = new BoardPathfindingService(_graphService);
            _combatResolutionService = new BoardCombatResolutionService(ruleSet);
            _lootResolutionService = new BoardLootResolutionService(lootTableSet);
            _progressionService = new BoardProgressionService(ruleSet, lootTableSet);
            _decisionService = new BoardAgentDecisionService(_graphService);
            _interruptService = new BoardInterruptService(ruleSet);
            _actionStateMachine = new BoardAgentActionStateMachine(
                _graphService,
                _pathfindingService,
                _decisionService,
                _combatResolutionService,
                _lootResolutionService,
                _progressionService,
                _interruptService,
                ruleSet,
                _bagLayoutSettings);

            List<BoardNodeRuntimeState> nodeStates = BuildNodeStates();
            BoardAgentState agentState = BuildAgentState();
            _sessionState = new BoardGameSessionState(mapDefinition.MapId, agentState, nodeStates);

            if (!IsProgressionEnabled)
            {
                ClearPendingLevelUpState();
            }

            NotifySessionChanged();
        }

        public event Action SessionChanged;
        public event Action SelectionChanged;

        public SO_BoardGame_MapDefinition MapDefinition => _mapDefinition;
        public SO_BoardGame_RuleSet RuleSet => _ruleSet;
        public SO_BoardGame_LootTableSet LootTableSet => _lootTableSet;
        public BoardGameBagLayoutSettings BagLayoutSettings => _bagLayoutSettings;
        public BoardGraphService GraphService => _graphService;
        public BoardGameSessionState SessionState => _sessionState;
        public string SelectedNodeId => _selectedNodeId;
        public bool IsProgressionEnabled => _ruleSet.ProgressionRules.Enabled;
        public bool IsAwaitingLevelUpChoice => IsProgressionEnabled && _sessionState.IsAwaitingLevelUpChoice;
        public bool IsAwaitingLootInteraction => _sessionState.IsAwaitingLootInteraction;
        public bool IsLootInteractionOpen => _sessionState.IsLootInteractionOpen;
        public bool IsInteractionLocked => IsAwaitingLevelUpChoice || IsLootInteractionOpen;

        /// <summary>
        /// 推进整套原型运行时逻辑
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsProgressionEnabled &&
                (_sessionState.IsAwaitingLevelUpChoice ||
                 _sessionState.PendingLevelUpCount > 0 ||
                 _sessionState.PendingLevelUpChoices.Count > 0))
            {
                ClearPendingLevelUpState();
            }

            if (IsAwaitingLevelUpChoice)
            {
                NotifySessionChanged();
                return;
            }

            if (IsAwaitingLootInteraction)
            {
                SyncLootActionProgress();
                NotifySessionChanged();
                return;
            }

            _actionStateMachine.Tick(_sessionState, _nodeStatesById, deltaTime);
            NotifySessionChanged();
        }

        /// <summary>
        /// 设置当前鼠标悬停的节点
        /// 仅用于地图高亮表现
        /// </summary>
        public void SelectNode(string nodeId)
        {
            if (_selectedNodeId == nodeId)
            {
                return;
            }

            _selectedNodeId = nodeId;
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// 尝试把 AI 当前目标改写到指定节点
        /// </summary>
        public bool TryRedirectToNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (IsAwaitingLevelUpChoice)
            {
                _sessionState.StatusMessage = "Choose a level-up upgrade before redirecting";
                NotifySessionChanged();
                return false;
            }

            if (IsAwaitingLootInteraction)
            {
                _sessionState.StatusMessage = "Finish the current loot interaction before redirecting";
                NotifySessionChanged();
                return false;
            }

            bool success = _actionStateMachine.TryRedirect(_sessionState, _nodeStatesById, nodeId, out _);

            NotifySessionChanged();
            return success;
        }

        /// <summary>
        /// 尝试使用一个道具栏里的道具
        /// </summary>
        public bool TryUseItem(string instanceId)
        {
            if (IsAwaitingLevelUpChoice)
            {
                _sessionState.StatusMessage = "Choose a level-up upgrade before using items";
                NotifySessionChanged();
                return false;
            }

            bool success = _lootResolutionService.TryConsumeItem(_sessionState.AgentState, instanceId, out string message);
            _sessionState.StatusMessage = message;
            NotifySessionChanged();
            return success;
        }

        /// <summary>
        /// 应用一个升级选项
        /// </summary>
        public bool TryApplyLevelUpChoice(int choiceIndex)
        {
            if (!IsProgressionEnabled)
            {
                _sessionState.StatusMessage = "Level-up progression is currently disabled";
                NotifySessionChanged();
                return false;
            }

            bool success = _progressionService.TryApplyLevelUpChoice(_sessionState, choiceIndex, out string message);

            if (!string.IsNullOrEmpty(message))
            {
                _sessionState.StatusMessage = message;
            }

            NotifySessionChanged();
            return success;
        }

        /// <summary>
        /// 查询某个节点当前是否允许作为玩家改写目标
        /// </summary>
        public bool IsNodeValidRedirectTarget(string nodeId)
        {
            if (IsAwaitingLevelUpChoice || IsAwaitingLootInteraction)
            {
                return false;
            }

            BoardInterruptEvaluation evaluation = _interruptService.Evaluate(_sessionState, _nodeStatesById, nodeId);
            return evaluation.CanInterrupt;
        }

        public BoardNodeRuntimeState GetSelectedNodeState()
        {
            return GetNodeState(_selectedNodeId);
        }

        public BoardNodeRuntimeState GetNodeState(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && _nodeStatesById.TryGetValue(nodeId, out BoardNodeRuntimeState nodeState)
                ? nodeState
                : null;
        }

        public BoardNodeRuntimeState GetActiveLootNodeState()
        {
            return GetNodeState(_sessionState.ActiveLootNodeId);
        }

        public bool CanOpenActiveLootNode()
        {
            if (!_sessionState.IsAwaitingLootInteraction || _sessionState.IsLootInteractionOpen)
            {
                return false;
            }

            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();
            return nodeState != null && nodeState.HasPendingLootContainer();
        }

        public bool TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState)
        {
            nodeState = GetActiveLootNodeState();

            if (nodeState == null || !CanOpenActiveLootNode())
            {
                return false;
            }

            _sessionState.IsLootInteractionOpen = true;
            _sessionState.StatusMessage = nodeState.IsLootRevealComplete()
                ? $"Loot bag opened at {nodeState.NodeId}"
                : $"Searching {nodeState.NodeId}";
            NotifySessionChanged();
            return true;
        }

        public void ApplyLootRevealProgress(int revealedItemCount)
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();

            if (nodeState == null)
            {
                return;
            }

            nodeState.LootRevealedItemCount = revealedItemCount;
            nodeState.SyncSearchProgressFromLootReveal();

            if (nodeState.NodeType == BoardNodeType.Resource)
            {
                nodeState.ResourceState = nodeState.IsLootRevealComplete()
                    ? BoardResourceStateType.SearchCompleted
                    : BoardResourceStateType.Searching;
            }

            SyncLootActionProgress();
            NotifySessionChanged();
        }

        public void CloseActiveLootNode(
            IReadOnlyList<BoardLootContainerItemState> remainingLootItems,
            IReadOnlyList<BoardItemInstance> playerInventoryItems,
            int revealedItemCount)
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();

            if (nodeState == null)
            {
                return;
            }

            _sessionState.AgentState.InventoryState.Items.Clear();

            if (playerInventoryItems != null)
            {
                _sessionState.AgentState.InventoryState.Items.AddRange(playerInventoryItems.Where(item => item != null));
            }

            nodeState.LootContainerItems.Clear();

            if (remainingLootItems != null)
            {
                nodeState.LootContainerItems.AddRange(remainingLootItems.Where(item => item != null));
            }

            nodeState.LootRevealedItemCount = revealedItemCount;
            nodeState.SyncSearchProgressFromLootReveal();
            _sessionState.IsLootInteractionOpen = false;
            bool shouldFinalizeNode = !nodeState.HasRemainingLootItems() && nodeState.IsLootRevealComplete();

            if (nodeState.NodeType == BoardNodeType.Resource)
            {
                if (shouldFinalizeNode)
                {
                    nodeState.ResourceState = BoardResourceStateType.Looted;
                }
                else if (nodeState.IsLootRevealComplete())
                {
                    nodeState.ResourceState = BoardResourceStateType.SearchCompleted;
                }
                else
                {
                    nodeState.ResourceState = nodeState.SearchProgressSeconds > 0f
                        ? BoardResourceStateType.PartiallySearched
                        : BoardResourceStateType.Unsearched;
                }
            }

            if (shouldFinalizeNode)
            {
                FinalizeActiveLootNode(nodeState);
            }
            else
            {
                ResumeAfterLootInteraction(
                    nodeState,
                    nodeState.IsLootRevealComplete()
                        ? $"Closed loot at {nodeState.NodeId}, AI resumed. The node can be revisited"
                        : $"Paused loot search at {nodeState.NodeId}, AI resumed. The node can be revisited");
            }

            NotifySessionChanged();
        }

        /// <summary>
        /// 为地图表现层生成当前应高亮的路径边列表
        /// </summary>
        public List<string> GetHighlightedEdgeIds()
        {
            List<string> edgeIds = new List<string>();
            BoardAgentState agentState = _sessionState.AgentState;

            if (agentState.IsOnEdge && !string.IsNullOrEmpty(agentState.CurrentEdgeId))
            {
                edgeIds.Add(agentState.CurrentEdgeId);
            }

            string previousNodeId = agentState.CurrentNodeId;

            if (string.IsNullOrEmpty(previousNodeId) && agentState.RemainingPathNodeIds.Count > 0)
            {
                previousNodeId = agentState.RemainingPathNodeIds[0];
            }

            for (int index = 0; index < agentState.RemainingPathNodeIds.Count; index++)
            {
                string nextNodeId = agentState.RemainingPathNodeIds[index];

                if (string.IsNullOrEmpty(previousNodeId) || previousNodeId == nextNodeId)
                {
                    previousNodeId = nextNodeId;
                    continue;
                }

                if (_graphService.TryGetEdgeBetween(previousNodeId, nextNodeId, out Config.BoardMapEdgeDefinition edgeDefinition))
                {
                    edgeIds.Add(edgeDefinition.EdgeId);
                }

                previousNodeId = nextNodeId;
            }

            return edgeIds;
        }

        /// <summary>
        /// 生成当前对局快照
        /// </summary>
        public BoardGameSerializableSnapshot CreateSnapshot()
        {
            return new BoardGameSerializableSnapshot(
                _mapDefinition.MapId,
                _ruleSet.RuleSetId,
                _lootTableSet.LootSetId,
                _sessionState);
        }

        /// <summary>
        /// 根据静态配置创建全部节点运行时状态
        /// </summary>
        private List<BoardNodeRuntimeState> BuildNodeStates()
        {
            List<BoardNodeRuntimeState> nodeStates = new List<BoardNodeRuntimeState>();

            foreach (BoardMapNodeDefinition nodeDefinition in _mapDefinition.Nodes)
            {
                // 静态配置和动态状态分离，运行时统一复制一份状态对象
                BoardNodeRuntimeState nodeState = new BoardNodeRuntimeState(
                    nodeDefinition.NodeId,
                    nodeDefinition.Description,
                    nodeDefinition.NodeType,
                    nodeDefinition.ResourceTier,
                    nodeDefinition.DangerTier);

                if (nodeDefinition.NodeType == BoardNodeType.Resource)
                {
                    nodeState.SearchRequiredSeconds = GetSearchDuration(nodeDefinition.ResourceTier);
                    nodeState.ResourceState = BoardResourceStateType.Unsearched;
                }

                if (nodeDefinition.NodeType == BoardNodeType.Extract)
                {
                    nodeState.ExtractState = BoardExtractStateType.Available;
                    nodeState.ExtractRequiredSeconds = _ruleSet.ExtractRules.DurationSeconds;
                }

                _combatResolutionService.InitializeNodeCombatState(nodeState);
                _nodeStatesById[nodeDefinition.NodeId] = nodeState;
                nodeStates.Add(nodeState);
            }

            return nodeStates;
        }

        /// <summary>
        /// 初始化角色运行时状态
        /// 包含起点、基础属性和开局自带道具
        /// </summary>
        private BoardAgentState BuildAgentState()
        {
            BoardAgentState agentState = new BoardAgentState(
                _ruleSet.AgentStats.MaxHealth,
                _ruleSet.AgentStats.Attack,
                _ruleSet.AgentStats.Defense,
                _ruleSet.AgentStats.MaxCarryCapacity);
            agentState.Level = _ruleSet.ProgressionRules.StartingLevel;
            agentState.CurrentExperience = 0;
            agentState.RequiredExperienceToNextLevel = _ruleSet.ProgressionRules.StartingRequiredExperience;

            string startNodeId = _graphService.GetStartNodeId();
            agentState.CurrentNodeId = startNodeId;
            agentState.WorldPosition = _graphService.GetNodePosition(startNodeId);
            agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;

            foreach (BoardItemInstance itemInstance in _lootResolutionService.CreateStartingItems(_ruleSet.AgentStats.StartingHealingPotionCount))
            {
                agentState.InventoryState.Items.Add(itemInstance);
            }

            return agentState;
        }

        /// <summary>
        /// 按资源等级读取搜索总时长
        /// </summary>
        private float GetSearchDuration(BoardResourceTier resourceTier)
        {
            switch (resourceTier)
            {
                case BoardResourceTier.Low:
                    return _ruleSet.SearchDurations.LowTierSeconds;
                case BoardResourceTier.Medium:
                    return _ruleSet.SearchDurations.MediumTierSeconds;
                case BoardResourceTier.High:
                    return _ruleSet.SearchDurations.HighTierSeconds;
                default:
                    return _ruleSet.SearchDurations.LowTierSeconds;
            }
        }

        /// <summary>
        /// 广播会话更新事件，供地图视图和 UI 刷新
        /// </summary>
        private void SyncLootActionProgress()
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();

            if (nodeState == null)
            {
                return;
            }

            BoardAgentState agentState = _sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();
        }

        private void FinalizeActiveLootNode(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    nodeState.ResourceState = BoardResourceStateType.Looted;
                    break;
                case BoardNodeType.Enemy:
                    nodeState.EnemyState = BoardEnemyStateType.Cleared;
                    break;
                case BoardNodeType.Boss:
                    nodeState.BossState = BoardBossStateType.Defeated;
                    break;
            }

            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;
            nodeState.ResetLootContainer();

            BoardAgentState agentState = _sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentTargetNodeId = string.Empty;
            agentState.IntentSource = BoardIntentSource.Autonomous;
            agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            _sessionState.StatusMessage = $"Finished searching {nodeState.NodeId}";
        }

        private void ResumeAfterLootInteraction(BoardNodeRuntimeState nodeState, string statusMessage)
        {
            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;

            BoardAgentState agentState = _sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentTargetNodeId = string.Empty;
            agentState.IntentSource = BoardIntentSource.Autonomous;
            agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            _sessionState.StatusMessage = statusMessage;
        }

        private void NotifySessionChanged()
        {
            SessionChanged?.Invoke();
        }

        private void ClearPendingLevelUpState()
        {
            _sessionState.IsAwaitingLevelUpChoice = false;
            _sessionState.PendingLevelUpCount = 0;
            _sessionState.PendingLevelUpChoices.Clear();
        }
    }
}
