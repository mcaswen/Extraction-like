using System.Collections.Generic;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 默认 AI 选点决策服务，只从当前节点的相邻节点中选择目标
    /// 当前版本故意把 AI 做得更笨，先硬比较风险，再比较收益
    /// </summary>
    public sealed class BoardAgentDecisionService
    {
        private readonly BoardGraphService _graphService;

        public BoardAgentDecisionService(BoardGraphService graphService)
        {
            _graphService = graphService;
        }

        /// <summary>
        /// 只从当前节点的相邻节点中挑选默认目标
        /// 该方法体现“先比最低风险，再比最高收益，撤离只做兜底”的默认逻辑
        /// </summary>
        public BoardDecisionResult ChooseAutonomousTarget(
            BoardAgentState agentState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            if (string.IsNullOrEmpty(agentState.CurrentNodeId))
            {
                return BoardDecisionResult.Invalid("AI is not on a node and cannot choose a target");
            }

            List<Config.BoardMapNodeDefinition> neighbors = _graphService.GetNeighbors(agentState.CurrentNodeId);
            bool canSelectExtract = CanSelectExtract(neighbors, nodeStatesById);

            bool hasCandidate = false;
            BoardNeighborDecisionScore bestCandidate = default;

            foreach (Config.BoardMapNodeDefinition neighbor in neighbors)
            {
                if (!nodeStatesById.TryGetValue(neighbor.NodeId, out BoardNodeRuntimeState nodeState))
                {
                    continue;
                }

                if (nodeState.NodeType == BoardNodeType.Extract && !canSelectExtract)
                {
                    continue;
                }

                BoardNeighborDecisionScore candidate = BuildDecisionScore(
                    nodeState,
                    neighbor.NodeId == agentState.PreviousNodeId);

                if (hasCandidate && !IsBetterCandidate(candidate, bestCandidate))
                {
                    continue;
                }

                hasCandidate = true;
                bestCandidate = candidate;
            }

            if (!hasCandidate)
            {
                return BoardDecisionResult.Invalid("No adjacent target is available");
            }

            string reason = bestCandidate.NodeType == BoardNodeType.Extract
                ? $"All other adjacent nodes are empty, extracting at {bestCandidate.NodeId}"
                : $"Default logic selected lowest-risk adjacent node {bestCandidate.NodeId}";

            return BoardDecisionResult.Valid(bestCandidate.NodeId, reason);
        }

        /// <summary>
        /// 只有当相邻非撤离节点都已经空掉时，撤离点才允许进入候选
        /// </summary>
        private static bool CanSelectExtract(
            IReadOnlyList<Config.BoardMapNodeDefinition> neighbors,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById)
        {
            bool hasExtractNeighbor = false;

            foreach (Config.BoardMapNodeDefinition neighbor in neighbors)
            {
                if (!nodeStatesById.TryGetValue(neighbor.NodeId, out BoardNodeRuntimeState nodeState))
                {
                    continue;
                }

                if (nodeState.NodeType == BoardNodeType.Extract)
                {
                    hasExtractNeighbor = true;
                    continue;
                }

                if (!IsNodeEmpty(nodeState))
                {
                    return false;
                }
            }

            return hasExtractNeighbor;
        }

        /// <summary>
        /// 把节点转成用于比较的风险与收益档位
        /// 风险永远先比较，收益只在风险相同时才生效
        /// </summary>
        private static BoardNeighborDecisionScore BuildDecisionScore(BoardNodeRuntimeState nodeState, bool isPreviousNode)
        {
            return new BoardNeighborDecisionScore(
                nodeState.NodeId,
                nodeState.NodeType,
                GetRiskRank(nodeState),
                GetRewardRank(nodeState),
                isPreviousNode);
        }

        /// <summary>
        /// 比较两个候选节点谁更符合当前笨 AI 的偏好
        /// </summary>
        private static bool IsBetterCandidate(BoardNeighborDecisionScore candidate, BoardNeighborDecisionScore currentBest)
        {
            if (candidate.RiskRank != currentBest.RiskRank)
            {
                return candidate.RiskRank < currentBest.RiskRank;
            }

            if (candidate.RewardRank != currentBest.RewardRank)
            {
                return candidate.RewardRank > currentBest.RewardRank;
            }

            if (candidate.IsPreviousNode != currentBest.IsPreviousNode)
            {
                return !candidate.IsPreviousNode;
            }

            return string.CompareOrdinal(candidate.NodeId, currentBest.NodeId) < 0;
        }

        /// <summary>
        /// 计算节点的风险档位
        /// 空节点和资源点都视为零风险
        /// </summary>
        private static int GetRiskRank(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                case BoardNodeType.Start:
                case BoardNodeType.Extract:
                    return 0;

                case BoardNodeType.Enemy:
                    return nodeState.EnemyState == BoardEnemyStateType.Cleared
                        ? 0
                        : (int)nodeState.DangerTier;

                case BoardNodeType.Boss:
                    return nodeState.BossState == BoardBossStateType.Defeated ? 0 : 4;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 计算节点的收益档位
        /// 只在风险相同的时候用于决定更值得去哪个点
        /// </summary>
        private static int GetRewardRank(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    if (nodeState.ResourceState == BoardResourceStateType.Looted)
                    {
                        return 0;
                    }

                    return (int)nodeState.ResourceTier * 100 + (nodeState.IsPartiallyProcessed() ? 10 : 0);

                case BoardNodeType.Enemy:
                    if (nodeState.EnemyState == BoardEnemyStateType.Cleared && !nodeState.HasPendingLootInteraction())
                    {
                        return 0;
                    }

                    return (int)nodeState.DangerTier * 100 +
                           (nodeState.HasPendingLootInteraction() ? 50 : 0) +
                           (nodeState.IsPartiallyProcessed() ? 10 : 0);

                case BoardNodeType.Boss:
                    if (nodeState.BossState == BoardBossStateType.Defeated && !nodeState.HasPendingLootInteraction())
                    {
                        return 0;
                    }

                    return 400 + (nodeState.HasPendingLootInteraction() ? 50 : 0);

                case BoardNodeType.Extract:
                    return 1000;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 判断一个节点是否已经没有可做的事情
        /// 撤离点不参与这个判断，因为撤离是单独的兜底分支
        /// </summary>
        private static bool IsNodeEmpty(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Start:
                    return true;
                case BoardNodeType.Resource:
                    return nodeState.ResourceState == BoardResourceStateType.Looted;
                case BoardNodeType.Enemy:
                    return nodeState.EnemyState == BoardEnemyStateType.Cleared && !nodeState.HasPendingLootInteraction();
                case BoardNodeType.Boss:
                    return nodeState.BossState == BoardBossStateType.Defeated && !nodeState.HasPendingLootInteraction();
                case BoardNodeType.Extract:
                    return false;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// 相邻节点比较时用到的中间评分结构
    /// </summary>
    internal readonly struct BoardNeighborDecisionScore
    {
        public BoardNeighborDecisionScore(
            string nodeId,
            BoardNodeType nodeType,
            int riskRank,
            int rewardRank,
            bool isPreviousNode)
        {
            NodeId = nodeId;
            NodeType = nodeType;
            RiskRank = riskRank;
            RewardRank = rewardRank;
            IsPreviousNode = isPreviousNode;
        }

        public string NodeId { get; }
        public BoardNodeType NodeType { get; }
        public int RiskRank { get; }
        public int RewardRank { get; }
        public bool IsPreviousNode { get; }
    }

    /// <summary>
    /// 默认 AI 选点结果
    /// </summary>
    public sealed class BoardDecisionResult
    {
        private BoardDecisionResult() { }

        public bool HasDecision { get; private set; }
        public string TargetNodeId { get; private set; }
        public string Reason { get; private set; }

        public static BoardDecisionResult Valid(string targetNodeId, string reason)
        {
            return new BoardDecisionResult
            {
                HasDecision = true,
                TargetNodeId = targetNodeId,
                Reason = reason
            };
        }

        public static BoardDecisionResult Invalid(string reason)
        {
            return new BoardDecisionResult
            {
                HasDecision = false,
                Reason = reason
            };
        }
    }
}
