using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 原型核心状态机，负责移动、搜索、战斗、撤离和玩家打断
    /// </summary>
    public sealed class BoardAgentActionStateMachine
    {
        private readonly BoardGraphService _graphService;
        private readonly BoardPathfindingService _pathfindingService;
        private readonly BoardAgentDecisionService _decisionService;
        private readonly BoardCombatResolutionService _combatResolutionService;
        private readonly BoardLootResolutionService _lootResolutionService;
        private readonly BoardInterruptService _interruptService;
        private readonly SO_BoardGame_RuleSet _ruleSet;

        public BoardAgentActionStateMachine(
            BoardGraphService graphService,
            BoardPathfindingService pathfindingService,
            BoardAgentDecisionService decisionService,
            BoardCombatResolutionService combatResolutionService,
            BoardLootResolutionService lootResolutionService,
            BoardInterruptService interruptService,
            SO_BoardGame_RuleSet ruleSet)
        {
            _graphService = graphService;
            _pathfindingService = pathfindingService;
            _decisionService = decisionService;
            _combatResolutionService = combatResolutionService;
            _lootResolutionService = lootResolutionService;
            _interruptService = interruptService;
            _ruleSet = ruleSet;
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
                    TickSearching(sessionState, nodeStatesById, deltaTime);
                    break;
                case BoardActionType.FightingEnemy:
                    TickCombat(sessionState, nodeStatesById, deltaTime, false);
                    break;
                case BoardActionType.FightingBoss:
                    TickCombat(sessionState, nodeStatesById, deltaTime, true);
                    break;
                case BoardActionType.Extracting:
                    TickExtracting(sessionState, nodeStatesById, deltaTime);
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

            if (agentState.RemainingPathNodeIds.Count > 0)
            {
                BeginNextMovementSegment(sessionState);
                return;
            }

            if (TryBeginActionOnCurrentTargetNode(sessionState, nodeStatesById))
            {
                return;
            }

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
        /// 搜索状态处理
        /// 搜索进度直接写回节点状态，因此被打断后天然可以续接
        /// </summary>
        private void TickSearching(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            nodeState.ResourceState = BoardResourceStateType.Searching;
            nodeState.SearchProgressSeconds = Mathf.Min(nodeState.SearchRequiredSeconds, nodeState.SearchProgressSeconds + deltaTime);
            agentState.CurrentActionDuration = nodeState.SearchRequiredSeconds;
            agentState.CurrentActionProgress = nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;

            if (nodeState.SearchProgressSeconds < nodeState.SearchRequiredSeconds)
            {
                return;
            }

            nodeState.ResourceState = BoardResourceStateType.SearchCompleted;
            List<BoardItemInstance> generatedItems = _lootResolutionService.GenerateResourceLoot(nodeState);
            BoardAutoCollectResult collectResult = _lootResolutionService.AutoCollect(agentState.InventoryState, generatedItems);
            nodeState.ResourceState = BoardResourceStateType.Looted;
            FinishCurrentTarget(sessionState, $"Search complete{collectResult.Summary}");
        }

        /// <summary>
        /// 普通战斗与 Boss 战的统一处理
        /// 通过 isBoss 区分不同节点状态字段和打断规则
        /// </summary>
        private void TickCombat(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime, bool isBoss)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            agentState.CurrentActionAccumulatorSeconds += deltaTime;
            int currentHealth = isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int maxHealth = Mathf.Max(1, isBoss ? nodeState.BossMaxHealth : nodeState.EnemyMaxHealth);
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = 1f - (float)currentHealth / maxHealth;

            // 战斗按固定 tick 结算，避免帧率变化直接影响数值结果
            while (agentState.CurrentActionAccumulatorSeconds >= _ruleSet.CombatRules.TickIntervalSeconds)
            {
                agentState.CurrentActionAccumulatorSeconds -= _ruleSet.CombatRules.TickIntervalSeconds;
                BoardCombatTickResult result = _combatResolutionService.ResolveCombatTick(agentState, nodeState, isBoss);

                if (result.AgentDefeated)
                {
                    return;
                }

                if (!result.EncounterDefeated)
                {
                    sessionState.StatusMessage = $"Combat ongoing  AI dealt {result.DamageDealt} damage and took {result.DamageTaken} damage";
                    continue;
                }

                List<BoardItemInstance> generatedItems = _lootResolutionService.GenerateEncounterLoot(nodeState, isBoss);
                BoardAutoCollectResult collectResult = _lootResolutionService.AutoCollect(agentState.InventoryState, generatedItems);
                FinishCurrentTarget(sessionState, isBoss ? $"Boss defeated{collectResult.Summary}" : $"Enemy cleared{collectResult.Summary}");
                return;
            }
        }

        /// <summary>
        /// 撤离状态处理
        /// 撤离进度保存在节点状态里，是否允许打断与是否保留进度由配置决定
        /// </summary>
        private void TickExtracting(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (!TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState nodeState))
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                return;
            }

            nodeState.ExtractState = BoardExtractStateType.Extracting;
            nodeState.ExtractProgressSeconds = Mathf.Min(_ruleSet.ExtractRules.DurationSeconds, nodeState.ExtractProgressSeconds + deltaTime);
            agentState.CurrentActionDuration = _ruleSet.ExtractRules.DurationSeconds;
            agentState.CurrentActionProgress = nodeState.ExtractProgressSeconds / _ruleSet.ExtractRules.DurationSeconds;

            if (nodeState.ExtractProgressSeconds < _ruleSet.ExtractRules.DurationSeconds)
            {
                return;
            }

            nodeState.ExtractState = BoardExtractStateType.Extracted;
            agentState.CurrentActionType = BoardActionType.Completed;
            agentState.CurrentActionProgress = 1f;
            sessionState.FinalExtractedValue = agentState.InventoryState.TotalValue;
            sessionState.Outcome = BoardSessionOutcome.Success;
            sessionState.StatusMessage = $"Extraction complete, final extracted value {sessionState.FinalExtractedValue}";
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
        /// 当角色真正抵达“当前目标节点”时，尝试进入对应的持续动作
        /// 中途路过的节点不会触发这里的逻辑
        /// </summary>
        private bool TryBeginActionOnCurrentTargetNode(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (string.IsNullOrEmpty(agentState.CurrentNodeId) ||
                string.IsNullOrEmpty(agentState.CurrentTargetNodeId) ||
                agentState.CurrentNodeId != agentState.CurrentTargetNodeId ||
                !nodeStatesById.TryGetValue(agentState.CurrentNodeId, out BoardNodeRuntimeState nodeState))
            {
                return false;
            }

            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    if (!nodeState.HasUnfinishedSearch()) return false;
                    nodeState.ResourceState = BoardResourceStateType.Searching;
                    agentState.CurrentActionType = BoardActionType.Searching;
                    agentState.CurrentActionDuration = nodeState.SearchRequiredSeconds;
                    agentState.CurrentActionProgress = nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
                    sessionState.StatusMessage = $"Started searching {nodeState.NodeId}";
                    return true;

                case BoardNodeType.Enemy:
                    if (!nodeState.HasUnclearedEnemy()) return false;
                    nodeState.EnemyState = BoardEnemyStateType.Engaged;
                    agentState.CurrentActionType = BoardActionType.FightingEnemy;
                    agentState.CurrentActionDuration = 1f;
                    agentState.CurrentActionAccumulatorSeconds = 0f;
                    agentState.CurrentActionProgress = 1f - (float)nodeState.EnemyCurrentHealth / Mathf.Max(1, nodeState.EnemyMaxHealth);
                    sessionState.StatusMessage = $"Started engaging {nodeState.NodeId}";
                    return true;

                case BoardNodeType.Boss:
                    if (!nodeState.HasUndefeatedBoss()) return false;
                    nodeState.BossState = BoardBossStateType.Engaged;
                    agentState.CurrentActionType = BoardActionType.FightingBoss;
                    agentState.CurrentActionDuration = 1f;
                    agentState.CurrentActionAccumulatorSeconds = 0f;
                    agentState.CurrentActionProgress = 1f - (float)nodeState.BossCurrentHealth / Mathf.Max(1, nodeState.BossMaxHealth);
                    sessionState.StatusMessage = $"Started boss fight at {nodeState.NodeId}";
                    return true;

                case BoardNodeType.Extract:
                    if (nodeState.ExtractState == BoardExtractStateType.Extracted) return false;
                    nodeState.ExtractState = BoardExtractStateType.Extracting;
                    agentState.CurrentActionType = BoardActionType.Extracting;
                    agentState.CurrentActionDuration = _ruleSet.ExtractRules.DurationSeconds;
                    agentState.CurrentActionProgress = nodeState.ExtractProgressSeconds / _ruleSet.ExtractRules.DurationSeconds;
                    sessionState.StatusMessage = $"Started extracting at {nodeState.NodeId}";
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 默认 AI 只有在当前撤离点周围已经没有别的可做节点时，才会启动撤离
        /// </summary>
        private bool TryBeginAutonomousExtract(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
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
        /// 搜索保留进度，普通敌人保留剩余血量，撤离按配置保留或重置进度
        /// </summary>
        private void FinalizeCurrentActionForRedirect(BoardGameSessionState sessionState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            BoardAgentState agentState = sessionState.AgentState;

            if (agentState.CurrentActionType == BoardActionType.Searching &&
                TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState searchNode))
            {
                searchNode.ResourceState = searchNode.SearchProgressSeconds > 0f ? BoardResourceStateType.PartiallySearched : BoardResourceStateType.Unsearched;
            }

            if (agentState.CurrentActionType == BoardActionType.FightingEnemy &&
                TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState enemyNode))
            {
                enemyNode.EnemyState = enemyNode.EnemyCurrentHealth < enemyNode.EnemyMaxHealth ? BoardEnemyStateType.Damaged : BoardEnemyStateType.Disengaged;
            }

            if (agentState.CurrentActionType == BoardActionType.Extracting &&
                TryGetCurrentNodeState(agentState, nodeStatesById, out BoardNodeRuntimeState extractNode))
            {
                if (!_ruleSet.ExtractRules.PreserveProgressOnInterrupt)
                {
                    extractNode.ExtractProgressSeconds = 0f;
                }

                extractNode.ExtractState = BoardExtractStateType.Available;
            }

            if (agentState.CurrentActionType != BoardActionType.Moving)
            {
                agentState.CurrentActionType = BoardActionType.Idle;
                agentState.CurrentActionProgress = 0f;
            }

            agentState.CurrentActionAccumulatorSeconds = 0f;
        }

        /// <summary>
        /// 完成当前目标节点动作后，重置为可继续默认决策的空闲状态
        /// </summary>
        private void FinishCurrentTarget(BoardGameSessionState sessionState, string statusMessage)
        {
            BoardAgentState agentState = sessionState.AgentState;
            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentTargetNodeId = string.Empty;
            agentState.IntentSource = BoardIntentSource.Autonomous;
            agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            sessionState.StatusMessage = statusMessage;
        }

        /// <summary>
        /// 从节点索引中读取角色当前所在节点状态
        /// </summary>
        private static bool TryGetCurrentNodeState(BoardAgentState agentState, IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById, out BoardNodeRuntimeState nodeState)
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
    }
}
