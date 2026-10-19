using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：背包会话关闭后收尾。容量不足：登记已处理、通报完成、标记需要撤离、上报容量不足并完成整条搜索；
    /// 正常关闭：登记已处理并完成本成员（箱内余物和揭示进度保持原样，供玩家重访）
    /// </summary>
    public sealed class FinishInventorySessionAction : AgentSearchActionBase
    {
        private readonly bool _lootBox;
        private readonly bool _capacityBlocked;

        public FinishInventorySessionAction(string nodeName, AgentSearchMemory memory, bool lootBox, bool capacityBlocked)
            : base(nodeName, capacityBlocked ? "容量不足收尾" : "关闭后收尾", memory)
        {
            _lootBox = lootBox;
            _capacityBlocked = capacityBlocked;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            GameObject resourceObject = _lootBox ? Memory.LootBox.gameObject : Memory.WorldItem.gameObject;
            AgentSearchedResourceRegistry.MarkSearched(resourceObject);
            if (_capacityBlocked)
            {
                GameplayTargetRegistry.GetOrCreate().NotifyResourceCompleted(resourceObject);
                SetFact(context, AgentBlackboardKeys.InventoryRequiresExtraction, true);
                Memory.EndReportedInteraction(AgentResourceInteractionStage.CapacityBlocked);
                Memory.CompleteResourceSearch(context);
                return Succeed();
            }

            Memory.CompleteConcreteResourceSearch(context, resourceObject, Memory.ClearDirectiveOnComplete);
            return Succeed();
        }
    }
}
