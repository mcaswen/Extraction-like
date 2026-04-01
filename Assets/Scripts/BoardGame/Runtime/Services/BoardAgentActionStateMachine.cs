using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 原型核心状态机，负责移动、默认决策、玩家打断与节点动作调度
    /// </summary>
    public sealed class BoardAgentActionStateMachine
    {
        private readonly BoardGraphService _graphService;
        private readonly BoardPathfindingService _pathfindingService;
        private readonly BoardAgentDecisionService _decisionService;
        private readonly BoardInterruptService _interruptService;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly BoardNodeActionHandlerContext _nodeActionHandlerContext;
        private readonly Dictionary<BoardNodeType, IBoardNodeActionHandler> _nodeActionHandlersByNodeType =
            new Dictionary<BoardNodeType, IBoardNodeActionHandler>();
        private readonly Dictionary<BoardActionType, IBoardNodeActionHandler> _nodeActionHandlersByActionType =
            new Dictionary<BoardActionType, IBoardNodeActionHandler>();

        public BoardAgentActionStateMachine(
            BoardGraphService graphService,
            BoardPathfindingService pathfindingService,
            BoardAgentDecisionService decisionService,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService,
            BoardProgressionService progressionService,
            BoardInterruptService interruptService,
            SO_BoardGame_RuleSet ruleSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _graphService = graphService;
            _pathfindingService = pathfindingService;
            _decisionService = decisionService;
            _interruptService = interruptService;
            _ruleSet = ruleSet;
            _nodeActionHandlerContext = new BoardNodeActionHandlerContext(
                ruleSet,
                combatResolutionService,
                lootResolutionService,
                progressionService,
                bagLayoutSettings);

            RegisterNodeActionHandler(new BoardSearchActionHandler());
            RegisterNodeActionHandler(new BoardCombatActionHandler(false));
            RegisterNodeActionHandler(new BoardCombatActionHandler(true));
            RegisterNodeActionHandler(new BoardExtractActionHandler());
        }

        /// <summary>
        /// 推进一次状态机
        /// 每帧只更新当前动作，对局结束后不再推进
        /// </summary>
        public void Tick(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime)
        {
            if (sessionState.Outcome != BoardSessionOutcome.None)
            {
                return;
            }

            BoardAgentState agentState = sessionState.AgentState;
            sessionState.ElapsedSeconds += deltaTime;
            agentState.AutonomousDecisionElapsedSeconds += deltaTime;

            if (!agentState.IsAlive)
            {
                FailSession(sessionState, "AI HP reached zero, run failed");
                return;
            }

            switch (agentState.CurrentActionType)
            {
                case BoardActionType.Idle:
                    TickIdle(sessionState, nodeStatesById);
                    break;
                case BoardActionType.Moving:
                    TickMoving(sessionState, nodeStatesById, deltaTime);
                    break;
                case BoardActionType.Searching:
                case BoardActionType.FightingEnemy:
                case BoardActionType.FightingBoss:
                case BoardActionType.Extracting:
                    TickCurrentNodeAction(sessionState, nodeStatesById, deltaTime);
                    break;
            }

            if (!agentState.IsAlive)
            {
                FailSession(sessionState, "AI HP reached zero, run failed");
            }
        }

        /// <summary>
        /// 由玩家发起一次目标改写
        /// 会先检查能否打断，再把当前动作安全收口，然后重新计算路径
        /// </summary>
        public bool TryRedirect(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            string targetNodeId,
            out string message)
        {
            BoardInterruptEvaluation evaluation = _interruptService.Evaluate(sessionState, nodeStatesById, targetNodeId);

            if (!evaluation.CanInterrupt)
            {
                message = evaluation.Message;
                sessionState.StatusMessage = message;
                return false;
            }

            FinalizeCurrentActionForRedirect(sessionState, nodeStatesById);

            BoardResolvedPathPlan pathPlan = _pathfindingService.ResolveFromAgent(sessionState.AgentState, targetNodeId);

            if (!pathPlan.IsValid)
            {
                message = "The target node is unreachable";
                sessionState.StatusMessage = message;
                return false;
            }

            ApplyPathPlan(sessionState, nodeStatesById, pathPlan, BoardIntentSource.PlayerRedirect);
            message = $"Player redirected the target to {nodeStatesById[targetNodeId].NodeId}";
            sessionState.StatusMessage = message;
            return true;
        }

        /// <summary>
        /// Idle 状态处理
        /// 优先续行剩余路径，其次执行当前目标节点动作，最后才进入默认 AI 选点
        /// </summary>
        private void TickIdle(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            // 已经有剩余路径时，Idle 只负责继续推进移动，不重复做新的选点
            if (agentState.RemainingPathNodeIds.Count > 0)
            {
                BeginNextMovementSegment(sessionState);
                return;
            }

            // 到达目标点后，优先尝试消费这个目标点上的动作
            if (TryBeginActionOnCurrentTargetNode(sessionState, nodeStatesById))
            {
                return;
            }

            // 当前节点本身是可撤离点时，先判断是否满足默认 AI 的撤离条件
            if (TryBeginAutonomousExtract(sessionState, nodeStatesById))
            {
                return;
            }

            if (agentState.IntentSource == BoardIntentSource.PlayerRedirect &&
                !string.IsNullOrEmpty(agentState.CurrentTargetNodeId) &&
                agentState.CurrentNodeId == agentState.CurrentTargetNodeId)
            {
                agentState.CurrentTargetNodeId = string.Empty;
                agentState.IntentSource = BoardIntentSource.Autonomous;
                sessionState.StatusMessage = "Player target reached, AI resumed default behavior";
            }

            // 默认 AI 不是每帧重新选点，而是按固定 reevaluate 间隔重算一次目标
            if (agentState.AutonomousDecisionElapsedSeconds < _ruleSet.AutonomousRules.ReevaluateIntervalSeconds)
            {
                return;
            }

            BoardDecisionResult decision = _decisionService.ChooseAutonomousTarget(agentState, nodeStatesById);

            if (!decision.HasDecision)
            {
                sessionState.StatusMessage = decision.Reason;
                return;
            }

            BoardResolvedPathPlan pathPlan = _pathfindingService.ResolveFromNode(agentState.CurrentNodeId, decision.TargetNodeId);

            if (!pathPlan.IsValid)
            {
                sessionState.StatusMessage = "The default target is unreachable";
                return;
            }

            // 只有当决策和寻路都成功时，才真正把目标和路径写回角色状态
            ApplyPathPlan(sessionState, nodeStatesById, pathPlan, BoardIntentSource.Autonomous);
            sessionState.StatusMessage = decision.Reason;
        }

        /// <summary>
        /// 沿边移动状态处理
        /// 支持在边中途回头，因为目标进度可以在 0 和 1 之间切换
        /// </summary>
        private void TickMoving(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (!_graphService.TryGetEdge(agentState.CurrentEdgeId, out BoardMapEdgeDefinition edgeDefinition))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            float segmentDuration = Mathf.Max(
                0.01f,
                Mathf.Abs(agentState.CurrentEdgeTargetProgress01 - agentState.CurrentEdgeSegmentStartProgress01) *
                edgeDefinition.LengthUnits *
                _ruleSet.MovementRules.SecondsPerLengthUnit);

            float deltaProgress = deltaTime / (edgeDefinition.LengthUnits * _ruleSet.MovementRules.SecondsPerLengthUnit);
            float direction = Mathf.Sign(agentState.CurrentEdgeTargetProgress01 - agentState.CurrentEdgeProgress01);
            agentState.CurrentEdgeProgress01 = Mathf.Clamp01(agentState.CurrentEdgeProgress01 + direction * deltaProgress);

            // 用边上插值位置驱动角色表现层，而不是瞬移到节点
            agentState.WorldPosition = _graphService.GetPositionOnEdge(edgeDefinition, agentState.CurrentEdgeProgress01);

            float span = Mathf.Abs(agentState.CurrentEdgeTargetProgress01 - agentState.CurrentEdgeSegmentStartProgress01);
            float walkedSpan = Mathf.Abs(agentState.CurrentEdgeProgress01 - agentState.CurrentEdgeSegmentStartProgress01);
            agentState.CurrentActionDuration = segmentDuration;
            agentState.CurrentActionProgress = span <= Mathf.Epsilon ? 1f : walkedSpan / span;

            bool reachedDestination = direction >= 0f
                ? agentState.CurrentEdgeProgress01 >= agentState.CurrentEdgeTargetProgress01
                : agentState.CurrentEdgeProgress01 <= agentState.CurrentEdgeTargetProgress01;

            if (!reachedDestination)
            {
                return;
            }

            string arrivedNodeId = agentState.CurrentEdgeTargetProgress01 <= 0.5f ? edgeDefinition.FromNodeId : edgeDefinition.ToNodeId;
            string previousNodeId = arrivedNodeId == edgeDefinition.FromNodeId ? edgeDefinition.ToNodeId : edgeDefinition.FromNodeId;
            agentState.PreviousNodeId = previousNodeId;
            agentState.CurrentNodeId = arrivedNodeId;
            agentState.WorldPosition = _graphService.GetNodePosition(arrivedNodeId);
            agentState.ClearEdgeTravel();
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionAccumulatorSeconds = 0f;

            // 只有真正到达了剩余路径的第一个节点，才会消费掉该节点
            if (agentState.RemainingPathNodeIds.Count > 0 && agentState.RemainingPathNodeIds[0] == arrivedNodeId)
            {
                agentState.RemainingPathNodeIds.RemoveAt(0);
            }

            // 玩家把目标改到更远节点时，沿途碰到可处理节点也要先停下来执行，而不是直接一路穿过去
            if (TryBeginActionOnVisitedRedirectNode(sessionState, nodeStatesById))
            {
                return;
            }

            if (agentState.RemainingPathNodeIds.Count > 0)
            {
                // 还有后续节点时，立刻衔接下一段移动，避免先回 Idle 再重新起步
                BeginNextMovementSegment(sessionState);
                return;
            }

            // 路径走完后，优先尝试开始当前节点动作，若没有可做动作再退回 Idle 选点
            if (!TryBeginActionOnCurrentTargetNode(sessionState, nodeStatesById))
            {
                TickIdle(sessionState, nodeStatesById);
            }
        }

        /// <summary>
        /// 推进当前所在节点上的持续动作
        /// 具体实现由节点动作处理器负责
        /// </summary>
        private void TickCurrentNodeAction(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (!_nodeActionHandlersByActionType.TryGetValue(agentState.CurrentActionType, out IBoardNodeActionHandler handler))
            {
                return;
            }

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            handler.Tick(_nodeActionHandlerContext, sessionState, nodeState, deltaTime);
        }

        /// <summary>
        /// 把寻路结果写入角色状态
        /// 如果角色当前就在边上，则优先复用当前边的剩余路段
        /// </summary>
        private void ApplyPathPlan(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardResolvedPathPlan pathPlan,
            BoardIntentSource intentSource)
        {
            BoardAgentState agentState = sessionState.AgentState;
            agentState.IntentSource = intentSource;
            agentState.CurrentTargetNodeId = pathPlan.TargetNodeId;
            agentState.RemainingPathNodeIds.Clear();
            agentState.RemainingPathNodeIds.AddRange(pathPlan.RemainingNodeIds);
            agentState.AutonomousDecisionElapsedSeconds = 0f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = 0f;

            if (agentState.IsOnEdge && pathPlan.UsesCurrentEdge)
            {
                agentState.CurrentActionType = BoardActionType.Moving;
                agentState.CurrentEdgeSegmentStartProgress01 = agentState.CurrentEdgeProgress01;
                agentState.CurrentEdgeTargetProgress01 = pathPlan.EdgeTargetProgress01;
                agentState.CurrentActionDuration = Mathf.Max(
                    0.01f,
                    Mathf.Abs(agentState.CurrentEdgeTargetProgress01 - agentState.CurrentEdgeSegmentStartProgress01) *
                    agentState.CurrentEdgeLengthUnits *
                    _ruleSet.MovementRules.SecondsPerLengthUnit);
                return;
            }

            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionDuration = 1f;

            if (agentState.RemainingPathNodeIds.Count > 0)
            {
                BeginNextMovementSegment(sessionState);
                return;
            }

            if (!TryBeginActionOnCurrentTargetNode(sessionState, nodeStatesById))
            {
                TickIdle(sessionState, nodeStatesById);
            }
        }

        /// <summary>
        /// 从当前节点出发，切入下一条边的移动
        /// </summary>
        private bool BeginNextMovementSegment(BoardGameSessionState sessionState)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (string.IsNullOrEmpty(agentState.CurrentNodeId) || agentState.RemainingPathNodeIds.Count == 0)
            {
                return false;
            }

            string nextNodeId = agentState.RemainingPathNodeIds[0];

            if (!_graphService.TryGetEdgeBetween(agentState.CurrentNodeId, nextNodeId, out BoardMapEdgeDefinition edgeDefinition))
            {
                sessionState.StatusMessage = "A required edge in the path does not exist";
                return false;
            }

            agentState.CurrentActionType = BoardActionType.Moving;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionDuration = edgeDefinition.LengthUnits * _ruleSet.MovementRules.SecondsPerLengthUnit;
            agentState.CurrentEdgeId = edgeDefinition.EdgeId;
            agentState.CurrentEdgeFromNodeId = edgeDefinition.FromNodeId;
            agentState.CurrentEdgeToNodeId = edgeDefinition.ToNodeId;
            agentState.CurrentEdgeLengthUnits = edgeDefinition.LengthUnits;

            if (edgeDefinition.FromNodeId == agentState.CurrentNodeId)
            {
                agentState.CurrentEdgeProgress01 = 0f;
                agentState.CurrentEdgeSegmentStartProgress01 = 0f;
                agentState.CurrentEdgeTargetProgress01 = 1f;
            }
            else
            {
                agentState.CurrentEdgeProgress01 = 1f;
                agentState.CurrentEdgeSegmentStartProgress01 = 1f;
                agentState.CurrentEdgeTargetProgress01 = 0f;
            }

            agentState.WorldPosition = _graphService.GetPositionOnEdge(edgeDefinition, agentState.CurrentEdgeProgress01);
            agentState.CurrentNodeId = string.Empty;
            return true;
        }

        /// <summary>
        /// 当角色真正抵达当前目标节点时，尝试进入对应的持续动作
        /// 玩家远点引导下的沿途节点会走单独的中途停靠逻辑
        /// </summary>
        private bool TryBeginActionOnCurrentTargetNode(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            return TryBeginActionOnCurrentNode(sessionState, nodeStatesById, true);
        }

        /// <summary>
        /// 玩家指定远处目标时，允许沿途节点先执行自己的动作
        /// 这样重定向既能表达前进方向，也不会跳过路上的资源点或战斗点
        /// </summary>
        private bool TryBeginActionOnVisitedRedirectNode(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (agentState.IntentSource != BoardIntentSource.PlayerRedirect ||
                agentState.RemainingPathNodeIds.Count == 0)
            {
                return false;
            }

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState) ||
                nodeState.NodeType == BoardNodeType.Extract)
            {
                return false;
            }

            return TryBeginActionOnCurrentNode(sessionState, nodeStatesById, false);
        }

        /// <summary>
        /// 尝试在当前节点开始动作
        /// 可选择是否要求该节点必须等于当前目标节点
        /// </summary>
        private bool TryBeginActionOnCurrentNode(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            bool requireCurrentTargetMatch)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (string.IsNullOrEmpty(agentState.CurrentNodeId) ||
                !nodeStatesById.TryGetValue(agentState.CurrentNodeId, out BoardNodeRuntimeState nodeState))
            {
                return false;
            }

            if (requireCurrentTargetMatch &&
                (string.IsNullOrEmpty(agentState.CurrentTargetNodeId) ||
                 agentState.CurrentNodeId != agentState.CurrentTargetNodeId))
            {
                return false;
            }

            if (TryBeginPendingLootInteraction(sessionState, nodeState))
            {
                return true;
            }

            if (!_nodeActionHandlersByNodeType.TryGetValue(nodeState.NodeType, out IBoardNodeActionHandler handler))
            {
                return false;
            }

            return handler.TryBegin(_nodeActionHandlerContext, sessionState, nodeState);
        }

        /// <summary>
        /// 默认 AI 只有在当前撤离点周围已经没有别的可做节点时，才会启动撤离
        /// </summary>
        private bool TryBeginAutonomousExtract(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (string.IsNullOrEmpty(agentState.CurrentNodeId) ||
                !nodeStatesById.TryGetValue(agentState.CurrentNodeId, out BoardNodeRuntimeState nodeState) ||
                nodeState.NodeType != BoardNodeType.Extract ||
                nodeState.ExtractState == BoardExtractStateType.Extracted ||
                !CanAutonomousExtractFromCurrentNode(agentState.CurrentNodeId, nodeStatesById))
            {
                return false;
            }

            agentState.CurrentTargetNodeId = agentState.CurrentNodeId;
            agentState.IntentSource = BoardIntentSource.Autonomous;
            return TryBeginActionOnCurrentTargetNode(sessionState, nodeStatesById);
        }

        /// <summary>
        /// 只有当相邻非撤离节点都已经空掉时，当前撤离点才允许被默认 AI 直接执行
        /// </summary>
        private bool CanAutonomousExtractFromCurrentNode(
            string currentNodeId,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            List<BoardMapNodeDefinition> neighbors = _graphService.GetNeighbors(currentNodeId);

            foreach (BoardMapNodeDefinition neighbor in neighbors)
            {
                if (!nodeStatesById.TryGetValue(neighbor.NodeId, out BoardNodeRuntimeState neighborState))
                {
                    continue;
                }

                if (neighborState.NodeType == BoardNodeType.Extract)
                {
                    continue;
                }

                if (!IsAutonomousEmptyNode(neighborState))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判断相邻节点是否已经没有默认 AI 愿意做的事情
        /// </summary>
        private static bool IsAutonomousEmptyNode(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Start:
                    return true;
                case BoardNodeType.Resource:
                    return nodeState.ResourceState == BoardResourceStateType.Looted;
                case BoardNodeType.Enemy:
                    return nodeState.EnemyState == BoardEnemyStateType.Cleared;
                case BoardNodeType.Boss:
                    return nodeState.BossState == BoardBossStateType.Defeated;
                case BoardNodeType.Extract:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 玩家改写目标前，对当前动作做安全收口
        /// 具体节点动作的收口逻辑交给对应处理器
        /// </summary>
        private void FinalizeCurrentActionForRedirect(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (_nodeActionHandlersByActionType.TryGetValue(agentState.CurrentActionType, out IBoardNodeActionHandler handler) &&
                TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                handler.FinalizeForRedirect(_nodeActionHandlerContext, sessionState, nodeState);
            }

            if (agentState.CurrentActionType != BoardActionType.Moving)
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                agentState.CurrentActionProgress = 0f;
            }

            agentState.CurrentActionAccumulatorSeconds = 0f;
        }

        /// <summary>
        /// 从节点索引中读取角色当前所在节点状态
        /// </summary>
        private static bool TryGetCurrentNodeState(
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            out BoardNodeRuntimeState nodeState)
        {
            nodeState = null;
            return !string.IsNullOrEmpty(agentState.CurrentNodeId) &&
                   nodeStatesById.TryGetValue(agentState.CurrentNodeId, out nodeState);
        }

        /// <summary>
        /// 统一设置失败结果
        /// </summary>
        private static void FailSession(BoardGameSessionState sessionState, string message)
        {
            sessionState.Outcome = BoardSessionOutcome.Failure;
            sessionState.AgentState.CurrentActionType = BoardActionType.Downed;
            sessionState.AgentState.CurrentActionProgress = 0f;
            sessionState.StatusMessage = message;
        }

        private bool TryBeginPendingLootInteraction(BoardGameSessionState sessionState, BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null || !nodeState.HasPendingLootContainer() || !nodeState.HasRemainingLootItems())
            {
                return false;
            }

            BoardAgentState agentState = sessionState.AgentState;
            sessionState.ActiveLootNodeId = nodeState.NodeId;
            sessionState.IsLootInteractionOpen = false;
            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();

            if (nodeState.NodeType == BoardNodeType.Resource)
            {
                nodeState.ResourceState = nodeState.IsLootRevealComplete()
                    ? BoardResourceStateType.SearchCompleted
                    : BoardResourceStateType.Searching;
            }

            sessionState.StatusMessage = nodeState.IsLootRevealComplete()
                ? $"Loot remains at {nodeState.NodeId}, press F to reopen"
                : $"Loot remains at {nodeState.NodeId}, press F to continue searching";
            return true;
        }

        private void RegisterNodeActionHandler(IBoardNodeActionHandler handler)
        {
            _nodeActionHandlersByNodeType[handler.SupportedNodeType] = handler;
            _nodeActionHandlersByActionType[handler.SupportedActionType] = handler;
        }
    }
}
