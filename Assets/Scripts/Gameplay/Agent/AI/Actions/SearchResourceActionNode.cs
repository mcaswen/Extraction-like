using System.Collections.Generic;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 搜索当前资源目标，发现战利品后等待玩家打开并关闭背包
    /// </summary>
    public sealed class SearchResourceActionNode : AgentActionNodeBase
    {
        private GameObject _waitingResourceObject;
        private GameObject _activeConcreteResourceObject;
        private string _activeResourceTargetId;
        private bool _hasReachedInteractionRange;
        private bool _hasObservedInventoryOpen;

        /// <summary>
        /// 创建资源搜索行为节点
        /// </summary>
        /// <param name="nodeName"></param>
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

            SyncActiveResourceTarget(directiveRequest);

            if (TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out ResourceClusterAuthoring resourceCluster))
            {
                return SearchResourceCluster(context, agent, resourceCluster);
            }

            if (directiveRequest.TargetObject == null)
            {
                return SearchAbstractResourcePoint(context, agent, directiveRequest);
            }

            return SearchResourceObject(context, agent, directiveRequest.TargetObject, true);
        }

        /// <summary>
        /// 搜索抽象资源点
        /// 当前只负责移动到点位，具体资源逻辑留给后续 Adapter
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agent"></param>
        /// <param name="directiveRequest"></param>
        /// <returns></returns>
        private BehaviorNodeResult SearchAbstractResourcePoint(
            BehaviorTreeContext context,
            IAgentReadOnly agent,
            AgentDirectiveRequest directiveRequest)
        {
            if (!TryResolveTargetPosition(directiveRequest.TargetRef, out Vector3 targetPosition))
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Resource target position is invalid");

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);

            if (!_hasReachedInteractionRange &&
                !MoveAgentTowards(agent, targetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                return Running();
            }

            _hasReachedInteractionRange = true;
            StopAgentMovement(agent);

            // 抽象资源点暂时视为搜索完成，后续由资源点 Adapter 接管
            CompleteResourceSearch(context);
            return Succeed();
        }

        /// <summary>
        /// 搜索资源群
        /// 直接选群内最近的未完成资源作为寻路目标，避免卡在抽象群中心
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agent"></param>
        /// <param name="resourceCluster"></param>
        /// <returns></returns>
        private BehaviorNodeResult SearchResourceCluster(
            BehaviorTreeContext context,
            IAgentReadOnly agent,
            ResourceClusterAuthoring resourceCluster)
        {
            if (!resourceCluster.TryGetNearestReachableIncompleteResource(
                    agent.Position,
                    agent.NavMeshAgent,
                    out GameObject resourceObject,
                    out Vector3 resourceNavigationPosition))
            {
                if (resourceCluster.HasBeenCompleted)
                {
                    CompleteResourceSearch(context);
                    return Succeed();
                }

                return Running();
            }

            if (_activeConcreteResourceObject != resourceObject)
            {
                _activeConcreteResourceObject = resourceObject;
                _hasReachedInteractionRange = false;
                ResetWaitState();
            }

            BehaviorNodeResult result = SearchResourceObject(
                context,
                agent,
                resourceObject,
                false,
                resourceNavigationPosition,
                true);
            if (result.Status != BehaviorNodeStatus.Success)
                return result;

            if (resourceCluster.HasBeenCompleted)
            {
                CompleteResourceSearch(context);
                return Succeed();
            }

            ResetConcreteResourceState();
            return Running();
        }

        /// <summary>
        /// 搜索单个具体资源对象
        /// 会根据对象类型分发到箱子或地面物品处理逻辑
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agent"></param>
        /// <param name="resourceObject"></param>
        /// <param name="clearDirectiveOnComplete"></param>
        /// <returns></returns>
        private BehaviorNodeResult SearchResourceObject(
            BehaviorTreeContext context,
            IAgentReadOnly agent,
            GameObject resourceObject,
            bool clearDirectiveOnComplete)
        {
            return SearchResourceObject(
                context,
                agent,
                resourceObject,
                clearDirectiveOnComplete,
                default,
                false);
        }

        private BehaviorNodeResult SearchResourceObject(
            BehaviorTreeContext context,
            IAgentReadOnly agent,
            GameObject resourceObject,
            bool clearDirectiveOnComplete,
            Vector3 navigationTargetPosition,
            bool hasNavigationTargetPosition)
        {
            if (resourceObject == null)
            {
                if (clearDirectiveOnComplete)
                    CompleteResourceSearch(context);

                return Succeed();
            }

            AgentTargetRef resourceTargetRef = AgentTargetRef.FromConcreteObject(
                AgentTargetKind.Resource,
                resourceObject,
                resourceObject.name);

            Vector3 targetPosition;
            bool resolvedTargetPosition = hasNavigationTargetPosition ||
                                          TryResolveInteractionTargetPosition(
                                              resourceTargetRef,
                                              agent.Position,
                                              out navigationTargetPosition);
            targetPosition = navigationTargetPosition;

            if (!resolvedTargetPosition)
            {
                return Fail(BehaviorFailureCode.MissingBlackboardValue, "Resource target position is invalid");
            }

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            // 搜索前先靠近具体资源，避免远距离直接收纳箱子
            if (!_hasReachedInteractionRange &&
                !MoveAgentTowards(agent, targetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                return Running();
            }

            _hasReachedInteractionRange = true;
            StopAgentMovement(agent);

            if (TryGetTargetComponent(
                    resourceTargetRef,
                    out global::LootBoxEntity lootBox))
            {
                return SearchLootBox(context, lootBox, clearDirectiveOnComplete);
            }

            if (TryGetTargetComponent(
                    resourceTargetRef,
                    out global::WorldLootItem worldItem))
            {
                return SearchWorldLootItem(context, worldItem, clearDirectiveOnComplete);
            }

            if (clearDirectiveOnComplete)
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

        /// <summary>
        /// 搜索箱子资源
        /// 有内容时等待玩家通过背包确认，空箱直接标记完成
        /// </summary>
        /// <param name="context"></param>
        /// <param name="lootBox"></param>
        /// <param name="clearDirectiveOnComplete"></param>
        /// <returns></returns>
        private BehaviorNodeResult SearchLootBox(
            BehaviorTreeContext context,
            global::LootBoxEntity lootBox,
            bool clearDirectiveOnComplete)
        {
            lootBox.EnsureLootGeneratedIfNeeded();
            if (lootBox.IsBoardGameResourcePoint)
            {
                lootBox.RefreshResourcePointState();
                if (lootBox.IsResourcePointLooted)
                {
                    AgentSearchedResourceRegistry.MarkSearched(lootBox.gameObject);
                    CompleteConcreteResourceSearch(context, lootBox.gameObject, clearDirectiveOnComplete);
                    return Succeed();
                }
            }
            else if (AgentSearchedResourceRegistry.IsSearched(lootBox.gameObject))
            {
                CompleteConcreteResourceSearch(context, lootBox.gameObject, clearDirectiveOnComplete);
                return Succeed();
            }

            List<global::ContainerItemSaveData> savedItems = lootBox.GetSavedItems();
            if (savedItems.Count <= 0)
            {
                // 空箱也算搜索完成，避免 Agent 卡在无收益资源点
                lootBox.MarkResourcePointLooted();
                AgentSearchedResourceRegistry.MarkSearched(lootBox.gameObject);
                CompleteConcreteResourceSearch(context, lootBox.gameObject, clearDirectiveOnComplete);
                return Succeed();
            }

            return WaitForPlayerInventoryClose(context, lootBox.gameObject, clearDirectiveOnComplete);
        }

        /// <summary>
        /// 搜索地面掉落资源
        /// 无效或数量为空时会直接标记为已处理
        /// </summary>
        /// <param name="context"></param>
        /// <param name="worldItem"></param>
        /// <param name="clearDirectiveOnComplete"></param>
        /// <returns></returns>
        private BehaviorNodeResult SearchWorldLootItem(
            BehaviorTreeContext context,
            global::WorldLootItem worldItem,
            bool clearDirectiveOnComplete)
        {
            if (AgentSearchedResourceRegistry.IsSearched(worldItem.gameObject))
            {
                CompleteConcreteResourceSearch(context, worldItem.gameObject, clearDirectiveOnComplete);
                return Succeed();
            }

            if (worldItem.ItemData == null || worldItem.CurrentAmount <= 0)
            {
                // 无效地面物品不再保留为搜索目标，避免 Agent 被空对象卡住
                AgentSearchedResourceRegistry.MarkSearched(worldItem.gameObject);
                CompleteConcreteResourceSearch(context, worldItem.gameObject, clearDirectiveOnComplete);
                return Succeed();
            }

            return WaitForPlayerInventoryClose(context, worldItem.gameObject, clearDirectiveOnComplete);
        }

        private void CompleteConcreteResourceSearch(
            BehaviorTreeContext context,
            GameObject resourceObject,
            bool clearDirectiveOnComplete)
        {
            GameplayTargetRegistry.GetOrCreate().NotifyResourceCompleted(resourceObject);
            if (clearDirectiveOnComplete)
                CompleteResourceSearch(context);
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
            _activeConcreteResourceObject = null;
            _hasReachedInteractionRange = false;
            ResetWaitState();
        }

        private static string GetStableTargetId(AgentDirectiveRequest directiveRequest)
        {
            if (!string.IsNullOrEmpty(directiveRequest.TargetId))
                return directiveRequest.TargetId;

            return directiveRequest.TargetRef.ToString();
        }

        /// <summary>
        /// 等待玩家打开并关闭背包后完成资源搜索
        /// 用玩家确认动作代替 Agent 自动拾取，避免直接改背包数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resourceObject"></param>
        /// <param name="clearDirectiveOnComplete"></param>
        /// <returns></returns>
        private BehaviorNodeResult WaitForPlayerInventoryClose(
            BehaviorTreeContext context,
            GameObject resourceObject,
            bool clearDirectiveOnComplete)
        {
            // 找到战利品后不再自动拾取，等待玩家完成一次背包开关确认
            global::InventoryScreenController inventoryController =
                global::InventoryScreenController.Instance;
            if (inventoryController == null)
            {
                // 没有背包控制器时不静默清目标，避免误判资源已处理
                return Running();
            }

            if (TryGetAgent(context, out IAgentReadOnly agent) &&
                !string.IsNullOrEmpty(inventoryController.ActiveInventoryAgentId) &&
                !string.Equals(inventoryController.ActiveInventoryAgentId, agent.AgentIdValue, System.StringComparison.Ordinal))
            {
                return Running();
            }

            if (_waitingResourceObject != resourceObject)
            {
                _waitingResourceObject = resourceObject;
                _hasObservedInventoryOpen = inventoryController.IsInventoryOpen;
                GameplayTargetRegistry.GetOrCreate().NotifyResourceTouched(resourceObject);
            }

            if (inventoryController.IsInventoryOpen)
            {
                _hasObservedInventoryOpen = true;
                return Running();
            }

            if (!_hasObservedInventoryOpen)
                return Running();

            if (resourceObject.TryGetComponent(out global::LootBoxEntity lootBox) &&
                lootBox.IsBoardGameResourcePoint)
            {
                lootBox.RefreshResourcePointState();
                if (lootBox.IsResourcePointLooted)
                {
                    AgentSearchedResourceRegistry.MarkSearched(resourceObject);
                    CompleteConcreteResourceSearch(context, resourceObject, clearDirectiveOnComplete);
                    return Succeed();
                }

                // 桌游资源点规则下，箱子仍有剩余 loot 就保持未完成，等待玩家再次打开处理。
                ResetWaitState();
                GameplayTargetRegistry.GetOrCreate().NotifyResourceTouched(resourceObject);
                return Running();
            }

            // 玩家打开过背包并关闭后，MVP 视为该资源点处理完毕
            AgentSearchedResourceRegistry.MarkSearched(resourceObject);
            CompleteConcreteResourceSearch(context, resourceObject, clearDirectiveOnComplete);
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
            _activeConcreteResourceObject = null;
            _hasReachedInteractionRange = false;
            ResetWaitState();
        }

        private void ResetConcreteResourceState()
        {
            _activeConcreteResourceObject = null;
            _hasReachedInteractionRange = false;
            ResetWaitState();
        }
    }
}
