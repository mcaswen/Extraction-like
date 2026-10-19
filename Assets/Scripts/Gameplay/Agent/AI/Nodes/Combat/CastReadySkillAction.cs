using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：有就绪技能就释放并锁定攻击；击杀则收尾返回成功，否则运行中。没有可放的技能时失败，交给普通射击。
    /// </summary>
    public sealed class CastReadySkillAction : AgentEngageActionBase
    {
        private const string NoReadySkill = "No ready skill";

        public CastReadySkillAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "释放就绪技能", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            AgentCombatController combatController = Memory.Combat;
            if (combatController == null ||
                !combatController.TryCastReadySkill(Memory.Enemy, context.TimeSeconds, out float actionLockSeconds))
            {
                return Fail(BehaviorFailureCode.ConditionFailed, NoReadySkill);
            }

            NotifyAttackAnimation(agent, actionLockSeconds);
            Memory.Combat?.LockAttack(context.TimeSeconds, actionLockSeconds);
            return TryConcludeDefeated(context) ? Succeed() : Running();
        }
    }
}
