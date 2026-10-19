using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：到达地面物品后检查是否已处理或已无效，是则登记并完成本成员，返回成功；否则返回失败，交给"等待背包"
    /// </summary>
    public sealed class InspectWorldItemAction : AgentSearchActionBase
    {
        private const string NeedsInventory = "World item needs the player inventory";

        public InspectWorldItemAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "检查地面物品", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            global::WorldLootItem worldItem = Memory.WorldItem;
            if (Memory.HasProcessed(worldItem.gameObject))
            {
                Memory.CompleteConcreteResourceSearch(context, worldItem.gameObject, Memory.ClearDirectiveOnComplete);
                return Succeed();
            }

            if (worldItem.ItemData == null || worldItem.CurrentAmount <= 0)
            {
                // 无效地面物品不再保留为搜索目标，避免 Agent 被空对象卡住
                AgentSearchedResourceRegistry.MarkSearched(worldItem.gameObject);
                Memory.CompleteConcreteResourceSearch(context, worldItem.gameObject, Memory.ClearDirectiveOnComplete);
                return Succeed();
            }

            return Fail(BehaviorFailureCode.ConditionFailed, NeedsInventory);
        }
    }
}
