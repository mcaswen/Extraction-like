using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：移向成员停靠点。未到达时清掉等待状态并上报"接近中"；刚离开交互范围时关掉本人为该资源打开的背包。到达后停步
    /// </summary>
    public sealed class ApproachMemberAction : AgentSearchActionBase
    {
        public ApproachMemberAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "靠近成员", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            float interactionDistance = GetFloat(context, AgentBlackboardKeys.InteractionDistance, 1.5f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            GameObject resourceObject = Memory.Member;
            if (!MoveAgentTowards(agent, Memory.MemberTargetPosition, interactionDistance, moveSpeed, context.DeltaTime))
            {
                bool wasInRange = Memory.HasReachedInteractionRange;
                Memory.HasReachedInteractionRange = false;
                Memory.ResetWaitState();
                Memory.ReportInteraction(context, resourceObject, AgentResourceInteractionStage.Approaching);
                if (wasInRange) CloseOwnedInventory(agent, resourceObject);
                return Running();
            }

            Memory.HasReachedInteractionRange = true;
            StopAgentMovement(agent);
            return Succeed();
        }

        private static void CloseOwnedInventory(IAgentReadOnly agent, GameObject resource)
        {
            var inventory = global::InventoryScreenController.Instance;
            if (inventory != null && inventory.IsInventoryOpen && inventory.ActiveInventoryAgentId == agent.AgentIdValue &&
                inventory.ActiveSessionContext?.SourceObject == resource)
                inventory.CloseInventory();
        }
    }
}
