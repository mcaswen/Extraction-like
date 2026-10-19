using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Targets.Runtime;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：目标丢失或已阵亡时收尾。阵亡先通报击败，再清事实并释放指令
    /// </summary>
    public sealed class ConcludeCombatAction : AgentEngageActionBase
    {
        public ConcludeCombatAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "收尾战斗", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (Memory.Enemy != null)
                GameplayTargetRegistry.GetOrCreate().NotifyEnemyDefeated(Memory.Enemy);
            CompleteCombat(context);
            return Succeed();
        }
    }
}
