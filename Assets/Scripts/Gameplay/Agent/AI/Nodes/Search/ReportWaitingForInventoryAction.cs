using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：上报"等待背包"。到达事实不依赖玩家当前聚焦谁，搜索是否完成仍由背包会话决定
    /// </summary>
    public sealed class ReportWaitingForInventoryAction : AgentSearchActionBase
    {
        private readonly bool _lootBox;

        public ReportWaitingForInventoryAction(string nodeName, AgentSearchMemory memory, bool lootBox)
            : base(nodeName, "上报等待背包", memory)
        {
            _lootBox = lootBox;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            GameObject resourceObject = _lootBox ? Memory.LootBox.gameObject : Memory.WorldItem.gameObject;
            Memory.ReportInteraction(context, resourceObject, AgentResourceInteractionStage.WaitingForInventory);
            return Succeed();
        }
    }
}
