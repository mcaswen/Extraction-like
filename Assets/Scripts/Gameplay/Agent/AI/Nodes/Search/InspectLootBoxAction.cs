using System.Collections.Generic;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：到达箱子后检查是否已经处理完（桌游资源点已搜空、已处理、空箱），是则登记并完成本成员，返回成功；
    /// 箱里还有东西时返回失败，交给"等待背包"。到达具体箱子的这一刻推进航天零件的搜索节奏。
    /// </summary>
    public sealed class InspectLootBoxAction : AgentSearchActionBase
    {
        private const string NeedsInventory = "Loot box needs the player inventory";

        public InspectLootBoxAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "检查箱子", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            global::LootBoxEntity lootBox = Memory.LootBox;
            // Preserve the shared opening gate when using the granular search tree.
            // Loot cadence and inventory-ready must wait for the same lid animation as the legacy tree.
            if (!ExtractionLike.Environment.RecoveryCaseOpeningFeedback.EnsureOpen(lootBox.gameObject))
            {
                Memory.ReportInteraction(context, lootBox.gameObject, AgentResourceInteractionStage.OpeningContainer);
                return Running();
            }
            ExtractionLike.Aerospace.AerospaceCollectionRuntime.Instance?.PrepareSearchedBox(lootBox);
            lootBox.EnsureLootGeneratedIfNeeded();
            if (lootBox.IsBoardGameResourcePoint)
            {
                lootBox.RefreshResourcePointState();
                if (lootBox.IsResourcePointLooted)
                {
                    AgentSearchedResourceRegistry.MarkSearched(lootBox.gameObject);
                    Memory.CompleteConcreteResourceSearch(context, lootBox.gameObject, Memory.ClearDirectiveOnComplete);
                    return Succeed();
                }
            }

            if (Memory.HasProcessed(lootBox.gameObject))
            {
                Memory.CompleteConcreteResourceSearch(context, lootBox.gameObject, Memory.ClearDirectiveOnComplete);
                return Succeed();
            }

            List<global::ContainerItemSaveData> savedItems = lootBox.GetSavedItems();
            if (savedItems.Count <= 0)
            {
                // 空箱也算搜索完成，避免 Agent 卡在无收益资源点
                lootBox.MarkResourcePointLooted();
                AgentSearchedResourceRegistry.MarkSearched(lootBox.gameObject);
                Memory.CompleteConcreteResourceSearch(context, lootBox.gameObject, Memory.ClearDirectiveOnComplete);
                return Succeed();
            }

            return Fail(BehaviorFailureCode.ConditionFailed, NeedsInventory);
        }
    }
}
