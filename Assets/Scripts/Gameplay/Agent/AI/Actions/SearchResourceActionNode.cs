using System.Collections.Generic;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 搜索当前资源目标，并尝试把第一件可用战利品放入角色容器
    /// </summary>
    public sealed class SearchResourceActionNode : AgentActionNodeBase
    {
        public SearchResourceActionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, "Agent context is invalid");

            if (!TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Search);

            if (!TryResolveTargetPosition(directiveRequest.TargetRef, out Vector3 targetPosition))
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Resource target position is invalid");

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            // 搜索前先靠近目标，避免远距离直接收纳箱子
            if (!MoveAgentTowards(agent, targetPosition, interactionDistance, moveSpeed, context.DeltaTime))
                return Running();

            if (!TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out global::LootBoxEntity lootBox))
            {
                // 抽象资源点暂时视为搜索完成，后续由资源点 Adapter 接管
                CompleteResourceSearch(context);
                return Succeed();
            }

            lootBox.PrecalculateLootIfNeeded();
            // MVP 先自动收纳第一件可放入角色容器的战利品
            if (TryCollectFirstAvailableLoot(lootBox))
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

            return Running();
        }

        private void CompleteResourceSearch(BehaviorTreeContext context)
        {
            SetFact(context, AgentBlackboardKeys.HasResourceTarget, false);
            SetFact(context, AgentBlackboardKeys.HasInteractableTarget, false);
            ClearPendingDirective(context);
        }

        private static bool TryCollectFirstAvailableLoot(global::LootBoxEntity lootBox)
        {
            // 当前背包主要由 UI 驱动，这里只使用公开接口做最小自动收纳
            global::InventoryScreenController inventoryController =
                global::InventoryScreenController.Instance;
            if (inventoryController == null || global::InventoryItemFactory.Instance == null)
                return false;

            List<global::ContainerItemSaveData> savedItems = lootBox.GetSavedItems();
            for (int index = 0; index < savedItems.Count; index++)
            {
                global::ContainerItemSaveData item = savedItems[index];
                if (item == null || item.ItemData == null || item.Amount <= 0)
                    continue;

                if (!inventoryController.TryPickupItem(item.ItemData, item.Amount))
                    continue;

                // 成功放入角色容器后再改箱子快照，保证失败时不吞物品
                savedItems.RemoveAt(index);
                lootBox.SaveItems(savedItems);
                global::RaidFlowController.Instance?.NotifyLootCollected(item.ItemData.ItemName);
                return true;
            }

            return false;
        }
    }
}
