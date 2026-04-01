using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 对局初始化控制器
    /// 负责根据静态配置创建节点状态和角色初始状态
    /// </summary>
    public sealed class BoardGameSessionBootstrapController
    {
        private readonly SO_BoardGame_MapDefinition _mapDefinition;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly BoardGraphService _graphService;
        private readonly BoardCombatResolutionService _combatResolutionService;
        private readonly BoardLootResolutionService _lootResolutionService;

        public BoardGameSessionBootstrapController(
            SO_BoardGame_MapDefinition mapDefinition,
            SO_BoardGame_RuleSet ruleSet,
            BoardGraphService graphService,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService)
        {
            _mapDefinition = mapDefinition;
            _ruleSet = ruleSet;
            _graphService = graphService;
            _combatResolutionService = combatResolutionService;
            _lootResolutionService = lootResolutionService;
        }

        /// <summary>
        /// 创建一局全新的运行时会话
        /// </summary>
        public BoardGameSessionState CreateSession(Dictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            List<BoardNodeRuntimeState> nodeStates = BuildNodeStates(nodeStatesById);
            BoardAgentState agentState = BuildAgentState();
            return new BoardGameSessionState(_mapDefinition.MapId, agentState, nodeStates);
        }

        /// <summary>
        /// 根据静态配置创建全部节点运行时状态
        /// </summary>
        private List<BoardNodeRuntimeState> BuildNodeStates(Dictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            List<BoardNodeRuntimeState> nodeStates = new List<BoardNodeRuntimeState>();
            nodeStatesById.Clear();

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
                nodeStatesById[nodeDefinition.NodeId] = nodeState;
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
    }
}
