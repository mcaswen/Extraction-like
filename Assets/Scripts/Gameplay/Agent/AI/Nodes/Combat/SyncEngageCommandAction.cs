using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;

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

            return Succeed();
        }
    }
}
