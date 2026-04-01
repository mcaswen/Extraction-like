using System;
using System.Collections.Generic;
using System.Linq;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;
using UnityEngine;

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
        public bool IsBagSystemEnabled => _bagLayoutSettings.EnableBagSystem;
        public bool IsRedirectModeActive => false;
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
            // 升级系统关闭时，要把历史遗留的待选状态清掉，避免表现层还停留在锁定态
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
                if (!IsBagSystemEnabled)
                {
                    // 关闭背包系统时，不再等待玩家手动拖拽，直接按旧容量规则结算当前节点 loot
                    ResolveActiveLootWithoutBagSystem();
                }
                else
                {
                    // 开着背包系统时，Searching 的动作进度完全由 reveal 进度驱动
                    SyncLootActionProgress();
                    NotifySessionChanged();
                    return;
                }

                if (IsAwaitingLootInteraction)
                {
                    // 自动结算后若节点仍未真正收口，继续维持 Searching 表现并等待后续流程完成
                    SyncLootActionProgress();
                    NotifySessionChanged();
                    return;
                }
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
        /// 保留旧版场景调用入口
        /// 当前版本的重定向流程不再依赖显式进入该模式
        /// </summary>
        public void EnterRedirectMode()
        {
        }

        /// <summary>
        /// 保留旧版场景调用入口
        /// 当前版本的重定向流程不再依赖显式退出该模式
        /// </summary>
        public void ExitRedirectMode()
        {
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

        /// <summary>
        /// 获取当前被鼠标选中的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetSelectedNodeState()
        {
            return GetNodeState(_selectedNodeId);
        }

        /// <summary>
        /// 按节点 ID 查询对应的运行时状态
        /// </summary>
        public BoardNodeRuntimeState GetNodeState(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && _nodeStatesById.TryGetValue(nodeId, out BoardNodeRuntimeState nodeState)
                ? nodeState
                : null;
        }

        /// <summary>
        /// 获取当前正在进行 loot 交互的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetActiveLootNodeState()
        {
            return GetNodeState(_sessionState.ActiveLootNodeId);
        }

        /// <summary>
        /// 判断当前是否允许打开活跃的 loot 节点
        /// </summary>
        public bool CanOpenActiveLootNode()
        {
            if (!_sessionState.IsAwaitingLootInteraction || _sessionState.IsLootInteractionOpen)
            {
                return false;
            }

            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();
            return nodeState != null && nodeState.HasPendingLootContainer();
        }

        /// <summary>
        /// 尝试打开当前活跃节点的 loot 面板
        /// </summary>
        /// <param name="nodeState"></param>
        /// <returns></returns>
        public bool TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState)
        {
            nodeState = GetActiveLootNodeState();

            if (!IsBagSystemEnabled || nodeState == null || !CanOpenActiveLootNode())
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

        /// <summary>
        /// 同步当前 loot 揭露进度到节点状态和动作表现
        /// </summary>
        /// <param name="revealedItemCount"></param>
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

        /// <summary>
        /// 关闭当前 loot 节点，并把剩余掉落和玩家背包结果回写到运行时状态
        /// </summary>
        /// <param name="remainingLootItems"></param>
        /// <param name="playerInventoryItems"></param>
        /// <param name="revealedItemCount"></param>
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
        /// 背包系统关闭时，直接按旧的数字容量规则结算当前节点掉落
        /// </summary>
        private void ResolveActiveLootWithoutBagSystem()
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();

            if (nodeState == null)
            {
                return;
            }

            List<BoardItemInstance> sourceItems = nodeState.LootContainerItems
                .Where(itemState => itemState?.ItemInstance != null)
                .Select(itemState => itemState.ItemInstance)
                .ToList();

            // 先让自动收取逻辑直接改写当前库存，再把节点 loot 清空并统一走关闭流程
            BoardAutoCollectResult autoCollectResult = _lootResolutionService.AutoCollect(
                _sessionState.AgentState.InventoryState,
                sourceItems);

            int revealedItemCount = Mathf.Max(nodeState.LootTotalItemCount, nodeState.LootContainerItems.Count);
            CloseActiveLootNode(new List<BoardLootContainerItemState>(), _sessionState.AgentState.InventoryState.Items.ToList(), revealedItemCount);
            _sessionState.StatusMessage = $"Auto collected loot at {nodeState.NodeId}. {autoCollectResult.Summary}";
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
        /// 将当前 loot 揭露进度同步到角色动作表现
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

        /// <summary>
        /// 完成当前 loot 节点的最终结算，并把角色动作重置回空闲态
        /// </summary>
        private void FinalizeActiveLootNode(BoardNodeRuntimeState nodeState)
        {
            // 节点最终状态要根据节点类型分别落到各自的终态字段上，避免混用通用状态
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

            // loot 交互结束后，要把动作、目标和自动决策计时全部收回到 Idle 基线
            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;
            nodeState.ResetLootContainer();

            BoardAgentState agentState = _sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;

            // 玩家指定远点且后续路径还没走完时，搜完当前节点后要继续朝最终目标前进
            if (ShouldResumeRedirectPathAfterLoot())
            {
                agentState.AutonomousDecisionElapsedSeconds = 0f;
            }
            else
            {
                agentState.CurrentTargetNodeId = string.Empty;
                agentState.IntentSource = BoardIntentSource.Autonomous;
                agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            }

            _sessionState.StatusMessage = $"Finished searching {nodeState.NodeId}";
        }

        /// <summary>
        /// 暂停当前 loot 交互并恢复自动行动，保留节点剩余掉落供后续回访
        /// </summary>
        private void ResumeAfterLootInteraction(BoardNodeRuntimeState nodeState, string statusMessage)
        {
            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;

            BoardAgentState agentState = _sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;

            if (ShouldResumeRedirectPathAfterLoot())
            {
                agentState.AutonomousDecisionElapsedSeconds = 0f;
            }
            else
            {
                agentState.CurrentTargetNodeId = string.Empty;
                agentState.IntentSource = BoardIntentSource.Autonomous;
                agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            }

            _sessionState.StatusMessage = statusMessage;
        }

        /// <summary>
        /// 判断当前 loot 收口后是否应继续沿玩家指定的远点路径前进
        /// </summary>
        private bool ShouldResumeRedirectPathAfterLoot()
        {
            BoardAgentState agentState = _sessionState.AgentState;
            return agentState.IntentSource == BoardIntentSource.PlayerRedirect &&
                   agentState.RemainingPathNodeIds.Count > 0 &&
                   !string.IsNullOrEmpty(agentState.CurrentTargetNodeId);
        }

        /// <summary>
        /// 广播会话变更事件，供地图和 HUD 刷新
        /// </summary>
        private void NotifySessionChanged()
        {
            SessionChanged?.Invoke();
        }

        /// <summary>
        /// 关闭升级系统时，清空所有等待中的升级选择状态
        /// </summary>
        private void ClearPendingLevelUpState()
        {
            _sessionState.IsAwaitingLevelUpChoice = false;
            _sessionState.PendingLevelUpCount = 0;
            _sessionState.PendingLevelUpChoices.Clear();
        }
    }
}
