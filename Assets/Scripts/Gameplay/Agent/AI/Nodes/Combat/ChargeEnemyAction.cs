using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：导航还没就绪时直接朝敌人地表点移动，并作废接近点缓存；保持运行中
    /// </summary>
    public sealed class ChargeEnemyAction : AgentEngageActionBase
    {
        public ChargeEnemyAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "直奔敌人", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            Memory.ApproachTime = -1d;
            MoveAgentTowards(agent, AgentCombatNavigationTarget.Resolve(Memory.Enemy), 0.1f, Memory.MoveSpeed, context.DeltaTime);
            return Running();
        }
    }
}
