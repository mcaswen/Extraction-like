using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗技能运行时基类
    /// 统一处理冷却、施法前置检查和动作锁定时长返回
    /// </summary>
    public abstract class AgentCombatSkillBase
    {
        private double _nextReadyTime;

        /// <summary>
        /// 创建技能运行时实例
        /// </summary>
        /// <param name="config"></param>
        protected AgentCombatSkillBase(AgentCombatSkillConfigBase config)
        {
            Config = config;
        }

        /// <summary>
        /// 当前技能对应的配置资产
        /// </summary>
        public AgentCombatSkillConfigBase Config { get; }

        /// <summary>
        /// 判断指定时间点技能是否已冷却完成
        /// </summary>
        /// <param name="timeSeconds"></param>
        /// <returns></returns>
        public bool IsReady(double timeSeconds)
        {
            return Config != null && timeSeconds >= _nextReadyTime;
        }

        /// <summary>
        /// 尝试释放技能，成功后会刷新冷却时间
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <param name="timeSeconds"></param>
        /// <param name="actionLockSeconds"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 判断技能在当前上下文和目标下是否允许释放
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected virtual bool CanCast(AgentCombatSkillContext context, AgentCombatSkillTarget target)
        {
            return target.EnemyTarget != null || target.HasPosition;
        }

        /// <summary>
        /// 执行具体技能效果
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        protected abstract bool Execute(AgentCombatSkillContext context, AgentCombatSkillTarget target);
    }
}
