using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;
using UnityEngine;

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
        private readonly SO_BoardGame_AgentRoster _agentRoster;
        private readonly BoardGraphService _graphService;
        private readonly BoardCombatResolutionService _combatResolutionService;
        private readonly BoardLootResolutionService _lootResolutionService;

        public BoardGameSessionBootstrapController(
            SO_BoardGame_MapDefinition mapDefinition,
            SO_BoardGame_RuleSet ruleSet,
            SO_BoardGame_AgentRoster agentRoster,
            BoardGraphService graphService,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService)
        {
            _mapDefinition = mapDefinition;
            _ruleSet = ruleSet;
            _agentRoster = agentRoster;
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
            List<BoardAgentState> agentStates = BuildAgentStates();
            return new BoardGameSessionState(_mapDefinition.MapId, agentStates, nodeStates);
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
        private List<BoardAgentState> BuildAgentStates()
        {
            List<BoardAgentState> agentStates = new List<BoardAgentState>();
            IReadOnlyList<BoardGameAgentRosterEntry> rosterEntries = ResolveAgentRosterEntries();

            for (int index = 0; index < rosterEntries.Count; index++)
            {
                BoardGameAgentRosterEntry rosterEntry = rosterEntries[index];
                string agentId = string.IsNullOrEmpty(rosterEntry.AgentId)
                    ? $"agent_{index + 1:00}"
                    : rosterEntry.AgentId;
                string displayName = string.IsNullOrEmpty(rosterEntry.DisplayName)
                    ? $"Agent {index + 1}"
                    : rosterEntry.DisplayName;

                BoardAgentState agentState = new BoardAgentState(
                    agentId,
                    displayName,
                    rosterEntry.AgentColor,
                    _ruleSet.AgentStats.MaxHealth,
                    _ruleSet.AgentStats.Attack,
                    _ruleSet.AgentStats.Defense,
                    _ruleSet.AgentStats.MaxCarryCapacity);
                agentState.Level = _ruleSet.ProgressionRules.StartingLevel;
                agentState.CurrentExperience = 0;
                agentState.RequiredExperienceToNextLevel = _ruleSet.ProgressionRules.StartingRequiredExperience;

                string startNodeId = ResolveAgentStartNodeId(rosterEntry);
                agentState.CurrentNodeId = startNodeId;
                agentState.WorldPosition = _graphService.GetNodePosition(startNodeId);
                agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;

                foreach (BoardItemInstance itemInstance in _lootResolutionService.CreateStartingItems(_ruleSet.AgentStats.StartingHealingPotionCount))
                {
                    agentState.InventoryState.Items.Add(itemInstance);
                }

                agentStates.Add(agentState);
            }

            return agentStates;
        }

        /// <summary>
        /// 解析当前应使用的 Agent roster
        /// 未配置时回退到四个默认 Agent，避免原型场景因为漏挂配置直接失效
        /// </summary>
        private IReadOnlyList<BoardGameAgentRosterEntry> ResolveAgentRosterEntries()
        {
            if (_agentRoster != null && _agentRoster.AgentEntries != null && _agentRoster.AgentEntries.Count > 0)
            {
                return _agentRoster.AgentEntries;
            }

            return new List<BoardGameAgentRosterEntry>
            {
                new BoardGameAgentRosterEntry("agent_01", "Agent 1", new Color(0.94f, 0.39f, 0.31f, 1f)),
                new BoardGameAgentRosterEntry("agent_02", "Agent 2", new Color(0.27f, 0.62f, 0.98f, 1f)),
                new BoardGameAgentRosterEntry("agent_03", "Agent 3", new Color(0.27f, 0.79f, 0.44f, 1f)),
                new BoardGameAgentRosterEntry("agent_04", "Agent 4", new Color(0.96f, 0.76f, 0.23f, 1f))
            };
        }

        /// <summary>
        /// 解析单个 Agent 的出生节点
        /// roster 未显式指定时统一回退到地图默认起点
        /// </summary>
        private string ResolveAgentStartNodeId(BoardGameAgentRosterEntry rosterEntry)
        {
            if (rosterEntry != null &&
                !string.IsNullOrEmpty(rosterEntry.StartNodeId) &&
                _graphService.TryGetNode(rosterEntry.StartNodeId, out _))
            {
                return rosterEntry.StartNodeId;
            }

            return _graphService.GetStartNodeId();
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
