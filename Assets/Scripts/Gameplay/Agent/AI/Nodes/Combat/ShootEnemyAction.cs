using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：普通射击。射击失败且是弹药不可用时按攻击不可用结束指令；命中后锁定攻击间隔，击杀则收尾。
    /// </summary>
    public sealed class ShootEnemyAction : AgentEngageActionBase
    {
        public ShootEnemyAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "普通射击", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            // 攻击参数从黑板读取，便于按 Agent 类型替换配置
            float attackDamage = GetFloat(context, AgentBlackboardKeys.AttackDamage, 25f);
            float attackInterval = GetFloat(context, AgentBlackboardKeys.AttackInterval, 0.65f);
            AgentCombatShooter shooter = agent.CachedTransform.GetComponent<AgentCombatShooter>();
            if (shooter == null || !shooter.TryShootAt(Memory.Enemy, attackDamage))
            {
                if (Memory.Shooter.LastShotFailure == AgentShotFailure.ProjectileUnavailable)
                    FailPendingDirective(context, AgentDirectiveFailure.AttackUnavailable);
                return Running();
            }

            NotifyAttackAnimation(agent, attackInterval);
            Memory.Combat?.LockAttack(context.TimeSeconds, attackInterval);
            return TryConcludeDefeated(context) ? Succeed() : Running();
        }
    }
}
