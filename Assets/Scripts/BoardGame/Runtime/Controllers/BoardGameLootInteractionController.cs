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
    /// Loot 交互控制器
    /// 负责打开、推进、关闭和自动结算节点战利品
    /// </summary>
    public sealed class BoardGameLootInteractionController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly IReadOnlyDictionary<string, BoardNodeRuntimeState> _nodeStatesById;
        private readonly BoardLootResolutionService _lootResolutionService;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly BoardGameBagLayoutSettings _bagLayoutSettings;

        public BoardGameLootInteractionController(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardLootResolutionService lootResolutionService,
            SO_BoardGame_RuleSet ruleSet,
            BoardGameBagLayoutSettings bagLayoutSettings)
        {
            _sessionState = sessionState;
            _nodeStatesById = nodeStatesById;
            _lootResolutionService = lootResolutionService;
            _ruleSet = ruleSet;
            _bagLayoutSettings = bagLayoutSettings;
        }

        public event Action Changed;

        /// <summary>
        /// 获取当前正在进行 loot 交互的节点状态
        /// </summary>
        public BoardNodeRuntimeState GetActiveLootNodeState()
        {
            return !string.IsNullOrEmpty(_sessionState.ActiveLootNodeId) &&
                   _nodeStatesById.TryGetValue(_sessionState.ActiveLootNodeId, out BoardNodeRuntimeState nodeState)
                ? nodeState
                : null;
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
        public bool TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState)
        {
            nodeState = GetActiveLootNodeState();

            if (!_bagLayoutSettings.EnableBagSystem || nodeState == null || !CanOpenActiveLootNode())
            {
                return false;
            }

            _sessionState.IsLootInteractionOpen = true;
            _sessionState.StatusMessage = nodeState.IsLootRevealComplete()
                ? $"Loot bag opened at {nodeState.NodeId}"
                : $"Searching {nodeState.NodeId}";
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 同步当前 loot 揭露进度到节点状态和动作表现
        /// </summary>
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
            NotifyChanged();
        }

        /// <summary>
        /// 关闭当前 loot 节点，并把剩余掉落和玩家背包结果回写到运行时状态
        /// </summary>
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

            NotifyChanged();
        }

        /// <summary>
        /// 推进当前等待中的 loot 交互
        /// 返回 true 时表示本帧已被 loot 逻辑消费
        /// </summary>
        public bool TickAwaitingLootInteraction(float deltaTime)
        {
            if (!_sessionState.IsAwaitingLootInteraction)
            {
                return false;
            }

            if (!_bagLayoutSettings.EnableBagSystem)
            {
                ResolveActiveLootWithoutBagSystem(deltaTime);
            }
            else
            {
                // 开着背包系统时，Searching 的动作进度完全由 reveal 进度驱动
                SyncLootActionProgress();
                return true;
            }

            if (_sessionState.IsAwaitingLootInteraction)
            {
                // 自动结算后若节点仍未真正收口，继续维持 Searching 表现并等待后续流程完成
                SyncLootActionProgress();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 背包系统关闭时，直接按旧的数字容量规则结算当前节点掉落
        /// </summary>
        private void ResolveActiveLootWithoutBagSystem(float deltaTime)
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();

            if (nodeState == null)
            {
                return;
            }

            if (nodeState.NodeType == BoardNodeType.Resource && !nodeState.IsLootRevealComplete())
            {
                float requiredSeconds = Mathf.Max(0.1f, nodeState.SearchRequiredSeconds);
                nodeState.SearchProgressSeconds = Mathf.Min(requiredSeconds, nodeState.SearchProgressSeconds + Mathf.Max(0f, deltaTime));
                nodeState.ResourceState = BoardResourceStateType.Searching;
                _sessionState.StatusMessage = $"Searching {nodeState.NodeId}";
                SyncLootActionProgress();

                if (nodeState.SearchProgressSeconds + Mathf.Epsilon < requiredSeconds)
                {
                    return;
                }

                nodeState.SearchProgressSeconds = requiredSeconds;
                nodeState.LootRevealedItemCount = nodeState.LootTotalItemCount;
                nodeState.ResourceState = BoardResourceStateType.SearchCompleted;
            }
            else if (!nodeState.IsLootRevealComplete())
            {
                AdvanceBaglessLootReveal(nodeState, deltaTime);
                _sessionState.StatusMessage = $"Searching loot at {nodeState.NodeId}";
                SyncLootActionProgress();

                if (!nodeState.IsLootRevealComplete())
                {
                    return;
                }
            }

            List<BoardItemInstance> sourceItems = nodeState.LootContainerItems
                .Where(itemState => itemState?.ItemInstance != null)
                .Select(itemState => itemState.ItemInstance)
                .ToList();

            // 先让自动收取逻辑直接改写当前库存，再把节点 loot 清空并统一走关闭流程
            BoardAutoCollectResult autoCollectResult = _lootResolutionService.AutoCollect(
                _sessionState.AgentState.InventoryState,
                sourceItems);

            int revealedItemCount = nodeState.LootTotalItemCount;
            CloseActiveLootNode(
                new List<BoardLootContainerItemState>(),
                _sessionState.AgentState.InventoryState.Items.ToList(),
                revealedItemCount);
            _sessionState.StatusMessage = $"Auto collected loot at {nodeState.NodeId}. {autoCollectResult.Summary}";
        }

        private static void AdvanceBaglessLootReveal(BoardNodeRuntimeState nodeState, float deltaTime)
        {
            BoardLootContainerItemState nextHiddenItem = GetNextHiddenLootItem(nodeState);

            if (nextHiddenItem == null)
            {
                nodeState.LootRevealedItemCount = nodeState.LootTotalItemCount;
                return;
            }

            if (!nextHiddenItem.AdvanceReveal(deltaTime))
            {
                return;
            }

            nodeState.LootRevealedItemCount = Mathf.Min(
                nodeState.LootRevealedItemCount + 1,
                nodeState.LootTotalItemCount);
        }

        private static BoardLootContainerItemState GetNextHiddenLootItem(BoardNodeRuntimeState nodeState)
        {
            BoardLootContainerItemState nextHiddenItem = null;
            int bestRevealSequence = int.MaxValue;

            foreach (BoardLootContainerItemState itemState in nodeState.LootContainerItems)
            {
                if (itemState == null || itemState.IsRevealed || itemState.RevealSequenceIndex >= bestRevealSequence)
                {
                    continue;
                }

                bestRevealSequence = itemState.RevealSequenceIndex;
                nextHiddenItem = itemState;
            }

            return nextHiddenItem;
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

            if (!_bagLayoutSettings.EnableBagSystem &&
                nodeState.NodeType == BoardNodeType.Resource &&
                !nodeState.IsLootRevealComplete())
            {
                agentState.CurrentActionDuration = Mathf.Max(0.1f, nodeState.SearchRequiredSeconds);
                agentState.CurrentActionProgress = nodeState.SearchRequiredSeconds <= Mathf.Epsilon
                    ? 1f
                    : nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
                return;
            }

            if (!_bagLayoutSettings.EnableBagSystem &&
                nodeState.HasPendingLootContainer() &&
                nodeState.HasRemainingLootItems() &&
                !nodeState.IsLootRevealComplete())
            {
                agentState.CurrentActionDuration = 1f;
                agentState.CurrentActionProgress = nodeState.GetLootRevealProgressWithPartial01();
                return;
            }

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

            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;
            nodeState.ResetLootContainer();

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
        /// 广播 loot 模块状态变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
