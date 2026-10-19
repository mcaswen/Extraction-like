using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：交战命令切换时清零失视计时、接近点缓存和初次接近预算
    /// </summary>
    public sealed class SyncEngageCommandAction : AgentEngageActionBase
    {
        public SyncEngageCommandAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "同步交战命令", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetDirective(context, AgentDirectiveType.Engage, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Engage);

            if (Memory.CommandId != directiveRequest.CommandId)
            {
                Memory.CommandId = directiveRequest.CommandId;
                Memory.ResetTimers();
            }

            // 帧首读到的指令决定本帧是否按玩家手动交战处理，后面的节点不再重读
            Memory.IsManualDirective = AgentManualDirectiveLock.IsManualDirective(directiveRequest);
            return Succeed();
        }
    }
}
