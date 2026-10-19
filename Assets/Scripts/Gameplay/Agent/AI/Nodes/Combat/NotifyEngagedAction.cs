using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Targets.Runtime;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：每帧向目标注册表通报正在与该敌人交战
    /// </summary>
    public sealed class NotifyEngagedAction : AgentEngageActionBase
    {
        public NotifyEngagedAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "通报交战", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            GameplayTargetRegistry.GetOrCreate().NotifyEnemyEngaged(Memory.Enemy);
            return Succeed();
        }
    }
}
