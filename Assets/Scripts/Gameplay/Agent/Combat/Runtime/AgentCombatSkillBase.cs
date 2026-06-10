using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public abstract class AgentCombatSkillBase
    {
        private double _nextReadyTime;

        protected AgentCombatSkillBase(AgentCombatSkillConfigBase config)
        {
            Config = config;
        }

        public AgentCombatSkillConfigBase Config { get; }

        public bool IsReady(double timeSeconds)
        {
            return Config != null && timeSeconds >= _nextReadyTime;
        }

        public bool TryCast(
            AgentCombatSkillContext context,
            AgentCombatSkillTarget target,
            double timeSeconds,
            out float actionLockSeconds)
        {
            actionLockSeconds = 0f;
            if (!IsReady(timeSeconds) || context.CasterTransform == null)
                return false;

            if (!CanCast(context, target))
                return false;

            if (!Execute(context, target))
                return false;

            _nextReadyTime = timeSeconds + Config.CooldownSeconds;
            actionLockSeconds = context.ActionLockSeconds;
            return true;
        }

        protected virtual bool CanCast(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            return target.EnemyTarget != null || target.HasPosition;
        }

        protected abstract bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target);
    }
}
