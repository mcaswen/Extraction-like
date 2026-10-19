using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Nodes;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Memory
{
    public enum AgentSearchTargetForm { None, Cluster, AbstractPoint, ConcreteObject }

    public enum AgentSearchMemberResolution { Member, ClusterDone, Unreachable }

    /// <summary>
    /// 搜索子树的执行记忆和交互上报。跨帧字段与重构前搜索节点一一对应；作用域进入时重置，
    /// 退出时按结果上报"完成"或"离开"，中断时上报"离开"。交互事件的发布顺序与重构前完全相同。
    /// </summary>
    public sealed class AgentSearchMemory : IBehaviorScope
    {
        // 跨帧
        public GameObject WaitingResourceObject;
        public GameObject ActiveConcreteResourceObject;
        public string ActiveResourceTargetId = string.Empty;
        public bool HasReachedInteractionRange;
        public global::InventoryScreenSessionContext ObservedInventorySession;
        public bool HasPlayedSearchCompleteSfx;
        public AgentResourceInteractionEvent? ReportedInteraction;
        public Vector3 ObservedNavigationPosition;
        public ResourceSearchPass ResourceSearch;

        // 帧内
        public AgentSearchTargetForm Form;
        public ResourceClusterAuthoring Cluster;
        public AgentSearchMemberResolution Resolution;
        public GameObject Member;
        public Vector3 MemberNavigationPosition;
        public bool HasMemberNavigationPosition;
        public bool ClearDirectiveOnComplete;
        public Vector3 MemberTargetPosition;
        public global::LootBoxEntity LootBox;
        public global::WorldLootItem WorldItem;
        public bool SessionCapacityBlocked;

        public void OnScopeEnter(BehaviorTreeContext context) => ResetSearchState();

        public void OnScopeExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            EndReportedInteraction(result.Status == BehaviorNodeStatus.Success
                ? AgentResourceInteractionStage.Completed : AgentResourceInteractionStage.Left);
            ResetSearchState();
        }

        public void OnScopeAbort(BehaviorTreeContext context)
        {
            EndReportedInteraction(AgentResourceInteractionStage.Left);
            ResetSearchState();
        }

        // 目标切换时清掉上一资源的等待/到达缓存，避免直接沿用旧资源的停靠状态
        public void SyncActiveResourceTarget(AgentDirectiveRequest directiveRequest)
        {
            string targetId = GetStableTargetId(directiveRequest);
            if (ActiveResourceTargetId == targetId)
                return;

            ActiveResourceTargetId = targetId;
            ActiveConcreteResourceObject = null;
            HasReachedInteractionRange = false;
            HasPlayedSearchCompleteSfx = false;
            ResetWaitState();
        }

        public void ResetWaitState()
        {
            if (ReportedInteraction?.Stage == AgentResourceInteractionStage.WaitingForInventory)
                EndReportedInteraction(AgentResourceInteractionStage.Left);
            WaitingResourceObject = null;
            ObservedInventorySession = null;
        }

        public void ResetSearchState()
        {
            EndReportedInteraction(AgentResourceInteractionStage.Left);
            ResourceSearch = null;
            ActiveResourceTargetId = string.Empty;
            ActiveConcreteResourceObject = null;
            HasReachedInteractionRange = false;
            ResetWaitState();
        }

        public void ResetConcreteResourceState()
        {
            ActiveConcreteResourceObject = null;
            HasReachedInteractionRange = false;
            ResetWaitState();
        }

        public void ReportInteraction(BehaviorTreeContext context, GameObject resource, AgentResourceInteractionStage stage)
        {
            if (!AgentTreeQueries.TryGetAgent(context, out IAgentReadOnly agent) ||
                !AgentTreeQueries.TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest request)) return;

            if (ReportedInteraction.HasValue)
            {
                var old = ReportedInteraction.Value;
                if (old.CommandId == request.CommandId && old.Resource == resource && old.Stage == stage &&
                    (old.NavigationPosition - ObservedNavigationPosition).sqrMagnitude < 0.0625f) return;
                if (old.CommandId != request.CommandId || old.Resource != resource)
                    EndReportedInteraction(AgentResourceInteractionStage.Left);
            }

            var value = new AgentResourceInteractionEvent(agent.AgentIdValue, request.CommandId, resource, ObservedNavigationPosition, stage);
            ReportedInteraction = value;
            AgentResourceInteractionChannel.Publish(value);
        }

        public void EndReportedInteraction(AgentResourceInteractionStage stage)
        {
            if (!ReportedInteraction.HasValue) return;

            var old = ReportedInteraction.Value;
            ReportedInteraction = null;
            AgentResourceInteractionChannel.Publish(new AgentResourceInteractionEvent(old.AgentId, old.CommandId,
                old.Resource, old.NavigationPosition, stage));
        }

        public void CompleteConcreteResourceSearch(BehaviorTreeContext context, GameObject resourceObject, bool clearDirectiveOnComplete)
        {
            if (ReportedInteraction?.Resource == resourceObject)
                EndReportedInteraction(AgentResourceInteractionStage.Completed);
            GameplayTargetRegistry.GetOrCreate().NotifyResourceCompleted(resourceObject);
            if (clearDirectiveOnComplete)
                CompleteResourceSearch(context);
        }

        public void CompleteResourceSearch(BehaviorTreeContext context)
        {
            PlaySearchCompleteSfx(context);
            context.Blackboard.SetValue(AgentBlackboardKeys.HasResourceTarget, false, context.TimeSeconds);
            ClearPendingDirective(context);
        }

        public static void ClearPendingDirective(BehaviorTreeContext context)
        {
            if (context.UserContext is IAgentCommandReceiver receiver &&
                context.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest, out AgentDirectiveRequest request))
                receiver.FinishDirective(request.CommandId);
        }

        public bool HasProcessed(GameObject resource)
            => ResourceSearch != null ? ResourceSearch.HasProcessed(resource) : AgentSearchedResourceRegistry.IsSearched(resource);

        private void PlaySearchCompleteSfx(BehaviorTreeContext context)
        {
            if (HasPlayedSearchCompleteSfx)
                return;

            if (AgentTreeQueries.TryGetAgent(context, out IAgentReadOnly agent))
            {
                HasPlayedSearchCompleteSfx = true;
                global::AgentSfxEmitter sfxEmitter = agent.CachedTransform != null
                    ? agent.CachedTransform.GetComponent<global::AgentSfxEmitter>()
                    : null;
                if (sfxEmitter != null)
                    sfxEmitter.PlaySearch();
                else
                    global::GameSfxPlayer.PlaySearchShort(agent.Position);
            }
        }

        private static string GetStableTargetId(AgentDirectiveRequest directiveRequest)
        {
            if (!string.IsNullOrEmpty(directiveRequest.CommandId)) return directiveRequest.CommandId;
            if (!string.IsNullOrEmpty(directiveRequest.TargetId))
                return directiveRequest.TargetId;
            return directiveRequest.TargetRef.ToString();
        }
    }
}
