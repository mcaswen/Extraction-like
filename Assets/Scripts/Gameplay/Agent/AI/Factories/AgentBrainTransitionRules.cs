using Core.BehaviorTree.Blackboard;
using Core.StateMachine.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.AI.Factories
{
    /// <summary>
    /// Agent Brain 宏状态转移规则集合
    /// 将状态转移条件从 Controller 中剥离出来，避免状态机构建逻辑与规则判断逻辑缠在一起
    /// </summary>
    public sealed class AgentBrainTransitionRules
    {
        private const double CombatDamageInterruptWindowSeconds = 1.25d;

        /// <summary>
        /// 判断是否可以进入战斗状态
        /// 条件：有可见敌人且未死亡；手动资源指令生效时不被敌人事实打断
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterCombat(StateMachineContext context)
        {
            if (HasManualResourceDirective(context))
                return false;

            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);
            return !isDead && hasVisibleEnemy && HasPendingEnemyEngageDirective(context);
        }

        /// <summary>
        /// 判断是否可以进入搜索资源状态
        /// 条件：有资源目标 + 未死亡；非手动资源指令时还要求没有敌人事实且不应该撤离
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
            bool hasManualResourceDirective = HasManualResourceDirective(context);

            return !isDead &&
                   hasResourceTarget &&
                   (hasManualResourceDirective ||
                    (!hasVisibleEnemy && !hasEnemySourceTarget && !shouldExtract));
        }

        /// <summary>
        /// 判断手动资源指令是否可以抢回资源搜索
        /// 条件：手动资源目标仍存在 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanManualResourceOverrideCurrentState(StateMachineContext context)
        {
            bool hasResourceTarget = GetBool(context, AgentBlackboardKeys.HasResourceTarget);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasResourceTarget && HasManualResourceDirective(context);
        }

        /// <summary>
        /// 判断资源搜索/拾取是否可以被战斗打断
        /// 条件：最近被战斗伤害命中 + 已生成接战指令 + 未死亡；
        /// 或资源目标已清空后，旧目标发现路径明确切换到了敌人接战指令
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanResourceWorkBeInterruptedByCombatDamage(StateMachineContext context)
        {
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);
            if (isDead || HasManualResourceDirective(context))
                return false;

            if (HasRecentCombatDamageInterrupt(context) &&
                HasPendingCombatDamageDirective(context))
            {
                return true;
            }

            bool hasResourceTarget = GetBool(context, AgentBlackboardKeys.HasResourceTarget);
            bool hasInteractableTarget = GetBool(context, AgentBlackboardKeys.HasInteractableTarget);
            return !hasResourceTarget &&
                   !hasInteractableTarget &&
                   HasPendingEnemyEngageDirective(context);
        }

        /// <summary>
        /// 判断是否可以进入敌人来源侦查状态
        /// 条件：有敌人来源目标 + 没有可见敌人 + 不应该撤离 + 未死亡
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanEnterInvestigateEnemySource(StateMachineContext context)
        {
            if (HasManualResourceDirective(context))
                return false;

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

            return !isDead && shouldExtract && context.Blackboard.TryGetValue(
                AgentBlackboardKeys.PendingDirectiveRequest, out AgentDirectiveRequest request) && request.DirectiveType == AgentDirectiveType.Extract;
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
        /// 条件：有可见敌人且未死亡；手动资源指令生效时不被敌人事实打断
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool CanExtractionBeInterruptedByCombat(StateMachineContext context)
        {
            if (HasManualResourceDirective(context))
                return false;

            bool hasVisibleEnemy = GetBool(context, AgentBlackboardKeys.HasVisibleEnemy);
            bool isDead = GetBool(context, AgentBlackboardKeys.AgentIsDead);

            return !isDead && hasVisibleEnemy && HasPendingEnemyEngageDirective(context);
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

        private static bool HasManualResourceDirective(StateMachineContext context)
        {
            return context.Blackboard.TryGetValue(
                       AgentBlackboardKeys.PendingDirectiveRequest,
                       out AgentDirectiveRequest directiveRequest) &&
                   AgentManualDirectiveLock.ShouldHoldManualResourceDirective(directiveRequest);
        }

        private static bool HasRecentCombatDamageInterrupt(StateMachineContext context)
        {
            return context.Blackboard.TryGetValue(
                       AgentBlackboardKeys.LastCombatDamageTime,
                       out double lastDamageTime) &&
                   lastDamageTime > double.NegativeInfinity &&
                   context.TimeSeconds >= lastDamageTime &&
                   context.TimeSeconds - lastDamageTime <= CombatDamageInterruptWindowSeconds;
        }

        private static bool HasPendingCombatDamageDirective(StateMachineContext context)
        {
            return context.Blackboard.TryGetValue(
                       AgentBlackboardKeys.PendingDirectiveRequest,
                       out AgentDirectiveRequest directiveRequest) &&
                   AgentManualDirectiveLock.IsCombatDamageDirective(directiveRequest);
        }

        private static bool HasPendingEnemyEngageDirective(StateMachineContext context)
        {
            return context.Blackboard.TryGetValue(
                       AgentBlackboardKeys.PendingDirectiveRequest,
                       out AgentDirectiveRequest directiveRequest) &&
                   directiveRequest.DirectiveType == AgentDirectiveType.Engage &&
                   directiveRequest.TargetRef.Kind == AgentTargetKind.Enemy;
        }
    }
}
