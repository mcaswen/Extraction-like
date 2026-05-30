using Core.BehaviorTree.Blackboard;
using Core.StateMachine.Runtime;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Factories
{
    /// <summary>
    /// Agent Brain 宏状态转移规则集合
    /// 将状态转移条件从 Controller 中剥离出来，避免状态机构建逻辑与规则判断逻辑缠在一起
    /// </summary>
    public sealed class AgentBrainTransitionRules
    {
        /// <summary>
        /// 判断是否可以进入战斗状态
        /// 条件：有可见敌人且未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterCombat(StateMachineContext context)
        {
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);
            return !isDead && hasVisibleEnemy;
        }

        /// <summary>
        /// 判断是否可以进入搜索资源状态
        /// 条件：有资源目标 + 未死亡 + 没有可见敌人 + 不应该撤离
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterSearchResource(StateMachineContext context)
        {
            bool hasResourceTarget = GetBool(context, AgentBlackboardKeys.HasResourceTarget);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool hasEnemySourceTarget = GetBool(context, AgentBlackboardKeys.HasEnemySourceTarget);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasResourceTarget && !hasVisibleEnemy && !hasEnemySourceTarget && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以进入敌人来源侦查状态
        /// 条件：有敌人来源目标 + 没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterInvestigateEnemySource(StateMachineContext context)
        {
            bool hasEnemySourceTarget = GetBool(context, AgentBlackboardKeys.HasEnemySourceTarget);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasEnemySourceTarget && !hasVisibleEnemy && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以进入交互/拾取状态
        /// 条件：有可交互目标 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterInteractLoot(StateMachineContext context)
        {
            bool hasInteractableTarget = GetBool(context, AgentBlackboardKeys.HasInteractableTarget);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasInteractableTarget;
        }

        /// <summary>
        /// 判断是否可以进入撤离状态
        /// 条件：应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterExtraction(StateMachineContext context)
        {
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && shouldExtract;
        }

        /// <summary>
        /// 判断是否可以从战斗状态离开进入探索状态
        /// 条件：没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanLeaveCombatToExplore(StateMachineContext context)
        {
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool hasEnemySourceTarget = GetBool(context, AgentBlackboardKeys.HasEnemySourceTarget);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && !hasVisibleEnemy && !hasEnemySourceTarget && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以从敌人来源侦查状态离开进入探索状态
        /// 条件：没有敌人来源目标 + 没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanLeaveInvestigateEnemySourceToExplore(StateMachineContext context)
        {
            bool hasEnemySourceTarget = GetBool(context, AgentBlackboardKeys.HasEnemySourceTarget);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && !hasEnemySourceTarget && !hasVisibleEnemy && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以从搜索资源状态离开进入探索状态
        /// 条件：没有资源目标 + 没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanLeaveSearchResourceToExplore(StateMachineContext context)
        {
            bool hasResourceTarget = GetBool(context, AgentBlackboardKeys.HasResourceTarget);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && !hasResourceTarget && !hasVisibleEnemy && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以从交互/拾取状态离开进入探索状态
        /// 条件：没有可交互目标 + 没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanLeaveInteractLootToExplore(StateMachineContext context)
        {
            bool hasInteractableTarget = GetBool(context, AgentBlackboardKeys.HasInteractableTarget);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && !hasInteractableTarget && !hasVisibleEnemy && !shouldExtract;
        }

        /// <summary>
        /// 判断是否可以从撤离状态被战斗打断
        /// 条件：有可见敌人且未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanExtractionBeInterruptedByCombat(StateMachineContext context)
        {
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasVisibleEnemy;
        }

        /// <summary>
        /// 判断是否可以从撤离状态离开进入探索状态
        /// 条件：没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanLeaveExtractionToExplore(StateMachineContext context)
        {
            bool shouldExtract = GetBool(context, AgentBlackboardKeys.ShouldExtract);
            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool hasEnemySourceTarget = GetBool(context, AgentBlackboardKeys.HasEnemySourceTarget);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && !shouldExtract && !hasVisibleEnemy && !hasEnemySourceTarget;
        }

        private static bool GetBool(StateMachineContext context, BlackboardKey key, bool defaultValue = false)
        {
            return context.Blackboard.TryGetValue(key, out bool value) ? value : defaultValue;
        }
    }
}
