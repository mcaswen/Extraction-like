using System.Collections.Generic;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 搜索当前资源目标，发现战利品后等待玩家打开并关闭背包
    /// </summary>
    public sealed class SearchResourceActionNode : AgentActionNodeBase
    {
        private GameObject _waitingResourceObject;
        private string _activeResourceTargetId;
        private bool _hasReachedInteractionRange;
        private bool _hasObservedInventoryOpen;

        public SearchResourceActionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected override void OnEnter(BehaviorTreeContext context)
        {
            ResetSearchState();
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, "Agent context is invalid");

            if (!TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Search);

            if (!TryResolveInteractionTargetPosition(
                    directiveRequest.TargetRef,
                    agent.Position,
                    out Vector3 targetPosition))
            {
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Resource target position is invalid");
            }

            SyncActiveResourceTarget(directiveRequest);

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            // 搜索前先靠近目标，避免远距离直接收纳箱子
            if (!_hasReachedInteractionRange &&
                !MoveAgentTowards(agent, targetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                return Running();
            }

            _hasReachedInteractionRange = true;
            StopAgentMovement(agent);

            if (TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out global::LootBoxEntity lootBox))
            {
                return SearchLootBox(context, lootBox);
            }

            if (TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out global::WorldLootItem worldItem))
            {
                return SearchWorldLootItem(context, worldItem);
            }

            // 抽象资源点暂时视为搜索完成，后续由资源点 Adapter 接管
            CompleteResourceSearch(context);
            return Succeed();
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            ResetSearchState();
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            ResetSearchState();
        }

        private BehaviorNodeResult SearchLootBox(
            BehaviorTreeContext context,
            global::LootBoxEntity lootBox)
        {
            lootBox.PrecalculateLootIfNeeded();
            if (AgentTargetDiscoveryController.IsResourceMarkedSearched(lootBox.gameObject))
            {
                CompleteResourceSearch(context);
                return Succeed();
            }

            List<global::ContainerItemSaveData> savedItems = lootBox.GetSavedItems();
            if (savedItems.Count <= 0)
            {
                // 空箱也算搜索完成，避免 Agent 卡在无收益资源点
                CompleteResourceSearch(context);
                return Succeed();
            }

            return WaitForPlayerInventoryClose(context, lootBox.gameObject);
        }

        private BehaviorNodeResult SearchWorldLootItem(
            BehaviorTreeContext context,
            global::WorldLootItem worldItem)
        {
            if (AgentTargetDiscoveryController.IsResourceMarkedSearched(worldItem.gameObject))
            {
                CompleteResourceSearch(context);
                return Succeed();
            }

            if (worldItem.ItemData == null || worldItem.CurrentAmount <= 0)
            {
                // 无效地面物品不再保留为搜索目标，避免 Agent 被空对象卡住
                AgentTargetDiscoveryController.MarkResourceSearched(worldItem.gameObject);
                CompleteResourceSearch(context);
                return Succeed();
            }

            return WaitForPlayerInventoryClose(context, worldItem.gameObject);
        }

        private void CompleteResourceSearch(BehaviorTreeContext context)
        {
            SetFact(context, AgentBlackboardKeys.HasResourceTarget, false);
            SetFact(context, AgentBlackboardKeys.HasInteractableTarget, false);
            ClearPendingDirective(context);
        }

        private void SyncActiveResourceTarget(AgentDirectiveRequest directiveRequest)
        {
            string targetId = GetStableTargetId(directiveRequest);
            if (_activeResourceTargetId == targetId)
                return;

            // 目标切换时清掉上一资源的等待/到达缓存，避免直接沿用旧资源的停靠状态
            _activeResourceTargetId = targetId;
            _hasReachedInteractionRange = false;
            ResetWaitState();
        }

        private static string GetStableTargetId(AgentDirectiveRequest directiveRequest)
        {
            if (!string.IsNullOrEmpty(directiveRequest.TargetId))
                return directiveRequest.TargetId;

            return directiveRequest.TargetRef.ToString();
        }

        private BehaviorNodeResult WaitForPlayerInventoryClose(
            BehaviorTreeContext context,
            GameObject resourceObject)
        {
            // 找到战利品后不再自动拾取，等待玩家完成一次背包开关确认
            global::InventoryScreenController inventoryController =
                global::InventoryScreenController.Instance;
            if (inventoryController == null)
            {
                // 没有背包控制器时不静默清目标，避免误判资源已处理
                return Running();
            }

            if (_waitingResourceObject != resourceObject)
            {
                _waitingResourceObject = resourceObject;
                _hasObservedInventoryOpen = inventoryController.IsInventoryOpen;
            }

            if (inventoryController.IsInventoryOpen)
            {
                _hasObservedInventoryOpen = true;
                return Running();
            }

            if (!_hasObservedInventoryOpen)
                return Running();

            // 玩家打开过背包并关闭后，MVP 视为该资源点处理完毕
            AgentTargetDiscoveryController.MarkResourceSearched(resourceObject);
            CompleteResourceSearch(context);
            return Succeed();
        }

        private void ResetWaitState()
        {
            _waitingResourceObject = null;
            _hasObservedInventoryOpen = false;
        }

        private void ResetSearchState()
        {
            _activeResourceTargetId = string.Empty;
            _hasReachedInteractionRange = false;
            ResetWaitState();
        }
    }
}
