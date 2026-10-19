using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Perception;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：评估能否开火。顺序与重构前一致：先查身体到目标的视线，再确认射手可用，最后查枪口和弹道。
    /// 枪口检查会覆盖射手的"上次射击结果"，后面的失视计时读的就是这次结果。
    /// </summary>
    public sealed class AssessFiringLineAction : AgentEngageActionBase
    {
        public AssessFiringLineAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "评估射界", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            Memory.AttackRange = GetFloat(context, AgentBlackboardKeys.AttackRange, 6f);
            Memory.MoveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            bool visible = TargetVisibilityQuery.Check(agent.CachedTransform, CombatAimPointResolver.Resolve(agent.CachedTransform),
                Memory.Enemy.transform, Memory.AttackRange) == TargetVisibilityResult.Visible;
            Memory.Shooter = agent.CachedTransform.GetComponent<AgentCombatShooter>();
            Memory.Combat = agent.CachedTransform.GetComponent<AgentCombatController>();
            Memory.ShooterUnavailable = Memory.Shooter == null || !Memory.Shooter.IsConfigured;
            if (Memory.ShooterUnavailable)
            {
                Memory.CanFire = false;
                return Succeed();
            }

            bool muzzleClear = Memory.Shooter.CanShootAt(Memory.Enemy, Memory.AttackRange);
            Memory.CanFire = visible && muzzleClear;
            return Succeed();
        }
    }
}
