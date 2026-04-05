using System;
using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 原型总控，负责装配运行时状态和各个子 controller，并向表现层提供兼容 facade
    /// </summary>
    public sealed class BoardGamePrototypeController
    {
        private readonly SO_BoardGame_MapDefinition _mapDefinition;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly SO_BoardGame_LootTableSet _lootTableSet;
        private readonly SO_BoardGame_AgentRoster _agentRoster;
        private readonly BoardGameBagLayoutSettings _bagLayoutSettings;

        private readonly BoardGraphService _graphService;
        private readonly BoardPathfindingService _pathfindingService;
        private readonly BoardCombatResolutionService _combatResolutionService;
        private readonly BoardLootResolutionService _lootResolutionService;
        private readonly BoardProgressionService _progressionService;
        private readonly BoardAgentDecisionService _decisionService;
        private readonly BoardInterruptService _interruptService;
        private readonly BoardAgentActionStateMachine _actionStateMachine;
        private readonly BoardGameSessionBootstrapController _sessionBootstrapController;
        private readonly BoardGameSelectionStateController _selectionStateController;
        private readonly BoardGameAgentFocusController _agentFocusController;
        private readonly BoardGameRuntimeQueryController _runtimeQueryController;
        private readonly BoardGameTargetRedirectController _targetRedirectController;
        private readonly BoardGameItemUseController _itemUseController;
        private readonly BoardGameProgressionController _progressionController;
        private readonly BoardGameLootInteractionController _lootInteractionController;
        private readonly BoardGameSnapshotController _snapshotController;
        private readonly BoardGameSessionFlowController _sessionFlowController;
        private readonly Dictionary<string, BoardNodeRuntimeState> _nodeStatesById =
            new Dictionary<string, BoardNodeRuntimeState>();

        private readonly BoardGameSessionState _sessionState;

        public BoardGamePrototypeController(
            SO_BoardGame_MapDefinition mapDefinition,
            SO_BoardGame_RuleSet ruleSet,
            SO_BoardGame_LootTableSet lootTableSet,
            SO_BoardGame_AgentRoster agentRoster,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _mapDefinition = mapDefinition;
            _ruleSet = ruleSet;
            _lootTableSet = lootTableSet;
            _agentRoster = agentRoster;
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
            _sessionBootstrapController = new BoardGameSessionBootstrapController(
                _mapDefinition,
                _ruleSet,
                _agentRoster,
                _graphService,
                _combatResolutionService,
                _lootResolutionService);

            _sessionState = _sessionBootstrapController.CreateSession(_nodeStatesById);
            _selectionStateController = new BoardGameSelectionStateController();
            _agentFocusController = new BoardGameAgentFocusController(_sessionState);
            _selectionStateController.SelectionChanged += HandleSelectionChanged;
            _runtimeQueryController = new BoardGameRuntimeQueryController(
                _sessionState,
                _nodeStatesById,
                _selectionStateController,
                _agentFocusController,
                _graphService,
                _ruleSet,
                _bagLayoutSettings);
            _targetRedirectController = new BoardGameTargetRedirectController(
                _sessionState,
                _nodeStatesById,
                _interruptService,
                _actionStateMachine);
            _itemUseController = new BoardGameItemUseController(_sessionState, _lootResolutionService);
            _progressionController = new BoardGameProgressionController(_sessionState, _ruleSet, _progressionService);
            _lootInteractionController = new BoardGameLootInteractionController(
                _sessionState,
                _nodeStatesById,
                _lootResolutionService,
                _ruleSet,
                _bagLayoutSettings);
            _snapshotController = new BoardGameSnapshotController(
                _mapDefinition,
                _ruleSet,
                _lootTableSet,
                _agentRoster,
                _sessionState);
            _sessionFlowController = new BoardGameSessionFlowController(
                _sessionState,
                _nodeStatesById,
                _progressionController,
                _lootInteractionController,
                _actionStateMachine);

            _agentFocusController.Changed += HandleSessionChanged;
            _targetRedirectController.Changed += HandleSessionChanged;
            _itemUseController.Changed += HandleSessionChanged;
            _progressionController.Changed += HandleSessionChanged;
            _lootInteractionController.Changed += HandleSessionChanged;
            _sessionFlowController.Changed += HandleSessionChanged;

            if (!IsProgressionEnabled)
            {
                _progressionController.ClearPendingState();
            }
        }

        public event Action SessionChanged;
        public event Action SelectionChanged;

        public SO_BoardGame_MapDefinition MapDefinition => _mapDefinition;
        public SO_BoardGame_RuleSet RuleSet => _ruleSet;
        public SO_BoardGame_LootTableSet LootTableSet => _lootTableSet;
        public SO_BoardGame_AgentRoster AgentRoster => _agentRoster;
        public BoardGameBagLayoutSettings BagLayoutSettings => _bagLayoutSettings;
        public BoardGraphService GraphService => _graphService;
        public BoardGameSessionBootstrapController SessionBootstrapController => _sessionBootstrapController;
        public BoardGameSelectionStateController SelectionStateController => _selectionStateController;
        public BoardGameAgentFocusController AgentFocusController => _agentFocusController;
        public BoardGameRuntimeQueryController RuntimeQueryController => _runtimeQueryController;
        public BoardGameTargetRedirectController TargetRedirectController => _targetRedirectController;
        public BoardGameItemUseController ItemUseController => _itemUseController;
        public BoardGameProgressionController ProgressionController => _progressionController;
        public BoardGameLootInteractionController LootInteractionController => _lootInteractionController;
        public BoardGameSnapshotController SnapshotController => _snapshotController;
        public BoardGameSessionFlowController SessionFlowController => _sessionFlowController;
        public BoardGameSessionState SessionState => _sessionState;
        public string SelectedNodeId => _selectionStateController.SelectedNodeId;
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
            _sessionFlowController.Tick(deltaTime);
        }

        /// <summary>
        /// 设置当前鼠标悬停的节点
        /// 仅用于地图高亮表现
        /// </summary>
        public void SelectNode(string nodeId)
        {
            _selectionStateController.SelectNode(nodeId);
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
            return _targetRedirectController.TryRedirectToNode(nodeId);
        }

        /// <summary>
        /// 尝试把指定 Agent 的目标改写到指定节点
        /// </summary>
        public bool TryRedirectAgentToNode(string agentId, string nodeId)
        {
            return _targetRedirectController.TryRedirectToNode(agentId, nodeId);
        }

        /// <summary>
        /// 尝试切换当前焦点 Agent
        /// </summary>
        public bool TrySetFocusedAgent(string agentId)
        {
            return _agentFocusController.TrySetFocusedAgent(agentId);
        }

        /// <summary>
        /// 切换到下一个焦点 Agent
        /// </summary>
        public bool FocusNextAgent()
        {
            return _agentFocusController.FocusNextAgent();
        }

        /// <summary>
        /// 切换到上一个焦点 Agent
        /// </summary>
        public bool FocusPreviousAgent()
        {
            return _agentFocusController.FocusPreviousAgent();
        }

        /// <summary>
        /// 尝试使用一个道具栏里的道具
        /// </summary>
        public bool TryUseItem(string instanceId)
        {
            return _itemUseController.TryUseItem(instanceId);
        }

        /// <summary>
        /// 应用一个升级选项
        /// </summary>
        public bool TryApplyLevelUpChoice(int choiceIndex)
        {
            return _progressionController.TryApplyLevelUpChoice(choiceIndex);
        }

        /// <summary>
        /// 查询某个节点当前是否允许作为玩家改写目标
        /// </summary>
        public bool IsNodeValidRedirectTarget(string nodeId)
        {
            return _targetRedirectController.IsNodeValidRedirectTarget(nodeId);
        }

        /// <summary>
        /// 获取当前被鼠标选中的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetSelectedNodeState()
        {
            return _runtimeQueryController.GetSelectedNodeState();
        }

        /// <summary>
        /// 按节点 ID 查询对应的运行时状态
        /// </summary>
        public BoardNodeRuntimeState GetNodeState(string nodeId)
        {
            return _runtimeQueryController.GetNodeState(nodeId);
        }

        /// <summary>
        /// 获取当前正在进行 loot 交互的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetActiveLootNodeState()
        {
            return _lootInteractionController.GetActiveLootNodeState();
        }

        /// <summary>
        /// 判断当前是否允许打开活跃的 loot 节点
        /// </summary>
        public bool CanOpenActiveLootNode()
        {
            return _lootInteractionController.CanOpenActiveLootNode();
        }

        /// <summary>
        /// 尝试打开当前活跃节点的 loot 面板
        /// </summary>
        public bool TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState)
        {
            return _lootInteractionController.TryOpenActiveLootNode(out nodeState);
        }

        /// <summary>
        /// 同步当前 loot 揭露进度到节点状态和动作表现
        /// </summary>
        public void ApplyLootRevealProgress(int revealedItemCount)
        {
            _lootInteractionController.ApplyLootRevealProgress(revealedItemCount);
        }

        /// <summary>
        /// 关闭当前 loot 节点，并把剩余掉落和玩家背包结果回写到运行时状态
        /// </summary>
        public void CloseActiveLootNode(
            IReadOnlyList<BoardLootContainerItemState> remainingLootItems,
            IReadOnlyList<BoardItemInstance> playerInventoryItems,
            int revealedItemCount)
        {
            _lootInteractionController.CloseActiveLootNode(remainingLootItems, playerInventoryItems, revealedItemCount);
        }

        /// <summary>
        /// 为地图表现层生成当前应高亮的路径边列表
        /// </summary>
        public List<string> GetHighlightedEdgeIds()
        {
            return _runtimeQueryController.GetHighlightedEdgeIds();
        }

        /// <summary>
        /// 生成当前对局快照
        /// </summary>
        public BoardGameSerializableSnapshot CreateSnapshot()
        {
            return _snapshotController.CreateSnapshot();
        }

        /// <summary>
        /// 转发节点选择变化事件，供地图和 HUD 刷新
        /// </summary>
        private void HandleSelectionChanged()
        {
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// 转发子模块和流程控制器的会话变更事件，兼容旧版监听入口
        /// </summary>
        private void HandleSessionChanged()
        {
            _runtimeQueryController.NotifyChanged();
            SessionChanged?.Invoke();
        }
    }
}
