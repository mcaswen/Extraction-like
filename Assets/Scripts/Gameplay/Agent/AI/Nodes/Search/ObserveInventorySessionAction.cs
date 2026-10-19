using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：找到战利品后不自动拾取，等玩家为这个智能体打开并关闭一次该资源的背包。
    /// 会话关闭前保持运行中；关闭后记录是否因容量不足而关闭，返回成功
    /// </summary>
    public sealed class ObserveInventorySessionAction : AgentSearchActionBase
    {
        private readonly bool _lootBox;

        public ObserveInventorySessionAction(string nodeName, AgentSearchMemory memory, bool lootBox)
            : base(nodeName, "等待玩家开关背包", memory)
        {
            _lootBox = lootBox;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            GameObject resourceObject = _lootBox ? Memory.LootBox.gameObject : Memory.WorldItem.gameObject;
            global::InventoryScreenController inventoryController = global::InventoryScreenController.Instance;
            if (inventoryController == null)
            {
                // 没有背包控制器时不静默清目标，避免误判资源已处理
                return Running();
            }

            if (!TryGetAgent(context, out IAgentReadOnly agent)) return Running();
            if (Memory.WaitingResourceObject != resourceObject)
            {
                Memory.WaitingResourceObject = resourceObject;
                Memory.ObservedInventorySession = null;
                GameplayTargetRegistry.GetOrCreate().NotifyResourceTouched(resourceObject);
            }

            var activeSession = inventoryController.ActiveSessionContext;
            if (Memory.ObservedInventorySession == null && inventoryController.IsInventoryOpen &&
                activeSession != null && activeSession.SourceObject == resourceObject &&
                activeSession.AgentId == agent.AgentIdValue && !activeSession.UseCustomPlayerInventory)
            {
                Memory.ObservedInventorySession = activeSession;
            }

            if (Memory.ObservedInventorySession == null || !Memory.ObservedInventorySession.IsClosed)
                return Running();

            Memory.SessionCapacityBlocked =
                Memory.ObservedInventorySession.CloseResult?.LootCapacity == global::InventoryLootCapacity.CapacityBlocked;
            return Succeed();
        }
    }
}
