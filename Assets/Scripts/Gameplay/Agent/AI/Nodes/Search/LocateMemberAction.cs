using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：确定当前成员的停靠点（群成员用导航解析出的点，直接目标取离智能体最近的碰撞体表面点），
    /// 识别成员是箱子还是地面物品；换了资源时上报"接近中"
    /// </summary>
    public sealed class LocateMemberAction : AgentSearchActionBase
    {
        private const string InvalidResourcePosition = "Resource target position is invalid";

        public LocateMemberAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "确定成员停靠点", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            GameObject resourceObject = Memory.Member;
            AgentTargetRef resourceTargetRef = AgentTargetRef.FromConcreteObject(
                AgentTargetKind.Resource,
                resourceObject,
                resourceObject.name);

            Vector3 navigationTargetPosition = Memory.MemberNavigationPosition;
            bool resolvedTargetPosition = Memory.HasMemberNavigationPosition ||
                                          TryResolveInteractionTargetPosition(
                                              resourceTargetRef,
                                              agent.Position,
                                              out navigationTargetPosition);
            if (!resolvedTargetPosition)
                return Fail(BehaviorFailureCode.MissingBlackboardValue, InvalidResourcePosition);

            // 搜索前先靠近具体资源，避免远距离直接收纳箱子
            Memory.MemberTargetPosition = navigationTargetPosition;
            Memory.ObservedNavigationPosition = navigationTargetPosition;
            if (!Memory.ReportedInteraction.HasValue || Memory.ReportedInteraction.Value.Resource != resourceObject)
                Memory.ReportInteraction(context, resourceObject, AgentResourceInteractionStage.Approaching);

            Memory.LootBox = TryGetTargetComponent(resourceTargetRef, out global::LootBoxEntity lootBox) ? lootBox : null;
            Memory.WorldItem = Memory.LootBox == null && TryGetTargetComponent(resourceTargetRef, out global::WorldLootItem worldItem) ? worldItem : null;
            return Succeed();
        }
    }
}
