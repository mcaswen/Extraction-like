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
            BoardAgentState activeAgentState = GetActiveInteractionAgentState();
            BoardAgentState focusedAgentState = _sessionState.GetFocusedAgentState();
            return nodeState != null &&
                   nodeState.HasPendingLootContainer() &&
                   activeAgentState != null &&
                   focusedAgentState != null &&
                   activeAgentState.AgentId == focusedAgentState.AgentId;
        }

        /// <summary>
        /// 尝试打开当前活跃节点的 loot 面板
        /// </summary>
        public bool TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState)
        {
            nodeState = GetActiveLootNodeState();

            if (!_bagLayoutSettings.EnableBagSystem)
            {
                return false;
            }

            if ((nodeState == null || !CanOpenActiveLootNode()) && !TryActivateFocusedLootInteraction())
            {
                return false;
            }

            nodeState = GetActiveLootNodeState();

            if (nodeState == null || !CanOpenActiveLootNode())
            {
                return false;
            }

            _sessionState.IsLootInteractionOpen = true;
            _sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                GetActiveInteractionAgentState(),
                nodeState,
                nodeState.IsLootRevealComplete()
                    ? "Loot bag opened"
                    : "Searching loot");
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
            BoardAgentState agentState = GetActiveInteractionAgentState();

            if (nodeState == null || agentState == null)
            {
                return;
            }

            agentState.InventoryState.Items.Clear();

            if (playerInventoryItems != null)
            {
                agentState.InventoryState.Items.AddRange(playerInventoryItems.Where(item => item != null));
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
                        ? "Closed loot, AI resumed and the node can be revisited"
                        : "Paused loot search, AI resumed and the node can be revisited");
            }

            NotifyChanged();
        }

        /// <summary>
        /// 推进当前等待中的 loot 交互
        /// 返回 true 时表示本帧已被 loot 逻辑消费
        /// </summary>
        public bool TickAwaitingLootInteraction()
        {
            if (!_sessionState.IsAwaitingLootInteraction)
            {
                return false;
            }

            if (!_bagLayoutSettings.EnableBagSystem)
            {
                ResolveActiveLootWithoutBagSystem();
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
        private void ResolveActiveLootWithoutBagSystem()
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();
            BoardAgentState agentState = GetActiveInteractionAgentState();

            if (nodeState == null || agentState == null)
            {
                return;
            }

            List<BoardItemInstance> sourceItems = nodeState.LootContainerItems
                .Where(itemState => itemState?.ItemInstance != null)
                .Select(itemState => itemState.ItemInstance)
                .ToList();

            // 先让自动收取逻辑直接改写当前库存，再把节点 loot 清空并统一走关闭流程
            BoardAutoCollectResult autoCollectResult = _lootResolutionService.AutoCollect(
                agentState.InventoryState,
                sourceItems);

            int revealedItemCount = Mathf.Max(nodeState.LootTotalItemCount, nodeState.LootContainerItems.Count);
            CloseActiveLootNode(
                new List<BoardLootContainerItemState>(),
                agentState.InventoryState.Items.ToList(),
                revealedItemCount);
            string autoCollectMessage = string.IsNullOrEmpty(autoCollectResult.Summary)
                ? "Auto collected loot"
                : $"Auto collected loot {autoCollectResult.Summary}";
            _sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                agentState,
                nodeState,
                autoCollectMessage);
        }

        /// <summary>
        /// 将当前 loot 揭露进度同步到角色动作表现
        /// </summary>
        private void SyncLootActionProgress()
        {
            BoardNodeRuntimeState nodeState = GetActiveLootNodeState();
            BoardAgentState agentState = GetActiveInteractionAgentState();

            if (nodeState == null || agentState == null)
            {
                return;
            }

            agentState.CurrentActionType = BoardActionType.Searching;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();
        }

        /// <summary>
        /// 允许当前焦点 Agent 直接接管自己脚下节点上剩余的共享 loot
        /// 这样多人同节点时切换焦点后不需要先离开再回来才能继续 search
        /// </summary>
        private bool TryActivateFocusedLootInteraction()
        {
            if (_sessionState.IsAwaitingLootInteraction || _sessionState.IsLootInteractionOpen)
            {
                return false;
            }

            BoardAgentState focusedAgentState = _sessionState.GetFocusedAgentState();

            if (focusedAgentState == null || string.IsNullOrEmpty(focusedAgentState.CurrentNodeId))
            {
                return false;
            }

            if (!_nodeStatesById.TryGetValue(focusedAgentState.CurrentNodeId, out BoardNodeRuntimeState nodeState) ||
                nodeState == null ||
                !nodeState.HasPendingLootInteraction())
            {
                return false;
            }

            _sessionState.ActiveInteractionAgentId = focusedAgentState.AgentId;
            _sessionState.ActiveLootNodeId = nodeState.NodeId;
            _sessionState.IsLootInteractionOpen = false;
            focusedAgentState.CurrentActionType = BoardActionType.Searching;
            focusedAgentState.CurrentActionDuration = 1f;
            focusedAgentState.CurrentActionAccumulatorSeconds = 0f;
            focusedAgentState.CurrentActionProgress = nodeState.GetLootRevealProgress01();
            focusedAgentState.CombatJoinStepIndex = -1;

            if (nodeState.NodeType == BoardNodeType.Resource)
            {
                nodeState.ResourceState = nodeState.IsLootRevealComplete()
                    ? BoardResourceStateType.SearchCompleted
                    : BoardResourceStateType.Searching;
            }

            _sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                focusedAgentState,
                nodeState,
                nodeState.IsLootRevealComplete()
                    ? "Loot remains, press F to reopen"
                    : "Loot remains, press F to continue searching");
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// 完成当前 loot 节点的最终结算，并把角色动作重置回空闲态
        /// </summary>
        private void FinalizeActiveLootNode(BoardNodeRuntimeState nodeState)
        {
            BoardAgentState agentState = GetActiveInteractionAgentState();

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

            _sessionState.ActiveInteractionAgentId = string.Empty;
            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;
            nodeState.ResetLootContainer();

            if (agentState == null)
            {
                _sessionState.StatusMessage = BoardGameStatusMessageUtility.Node(nodeState, "Finished searching");
                return;
            }

            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;

            if (ShouldResumeRedirectPathAfterLoot(agentState))
            {
                agentState.AutonomousDecisionElapsedSeconds = 0f;
            }
            else
            {
                agentState.CurrentTargetNodeId = string.Empty;
                agentState.IntentSource = BoardIntentSource.Autonomous;
                agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            }

            _sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                agentState,
                nodeState,
                "Finished searching");
        }

        /// <summary>
        /// 暂停当前 loot 交互并恢复自动行动，保留节点剩余掉落供后续回访
        /// </summary>
        private void ResumeAfterLootInteraction(BoardNodeRuntimeState nodeState, string statusMessage)
        {
            BoardAgentState agentState = GetActiveInteractionAgentState();

            _sessionState.ActiveInteractionAgentId = string.Empty;
            _sessionState.ActiveLootNodeId = string.Empty;
            _sessionState.IsLootInteractionOpen = false;

            if (agentState == null)
            {
                _sessionState.StatusMessage = BoardGameStatusMessageUtility.Node(nodeState, statusMessage);
                return;
            }

            agentState.CurrentActionType = BoardActionType.Idle;
            agentState.CurrentActionProgress = 0f;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;

            if (ShouldResumeRedirectPathAfterLoot(agentState))
            {
                agentState.AutonomousDecisionElapsedSeconds = 0f;
            }
            else
            {
                agentState.CurrentTargetNodeId = string.Empty;
                agentState.IntentSource = BoardIntentSource.Autonomous;
                agentState.AutonomousDecisionElapsedSeconds = _ruleSet.AutonomousRules.ReevaluateIntervalSeconds;
            }

            _sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(agentState, nodeState, statusMessage);
        }

        /// <summary>
        /// 判断当前 loot 收口后是否应继续沿玩家指定的远点路径前进
        /// </summary>
        private static bool ShouldResumeRedirectPathAfterLoot(BoardAgentState agentState)
        {
            if (agentState == null)
            {
                return false;
            }

            return agentState.IntentSource == BoardIntentSource.PlayerRedirect &&
                   agentState.RemainingPathNodeIds.Count > 0 &&
                   !string.IsNullOrEmpty(agentState.CurrentTargetNodeId);
        }

        /// <summary>
        /// 获取当前 loot 交互归属的 Agent
        /// </summary>
        private BoardAgentState GetActiveInteractionAgentState()
        {
            return _sessionState.GetActiveInteractionAgentState() ?? _sessionState.GetFocusedAgentState();
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
