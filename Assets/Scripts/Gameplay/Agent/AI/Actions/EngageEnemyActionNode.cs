using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Animation;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// 执行对当前敌人目标的攻击
    /// </summary>
    public sealed class EngageEnemyActionNode : AgentActionNodeBase
    {
        private double _nextAttackTime;

        /// <summary>
        /// 创建敌人接战行为节点
        /// </summary>
        /// <param name="nodeName"></param>
        public EngageEnemyActionNode(string nodeName)
            : base(nodeName)
        {
        }

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _nextAttackTime = 0d;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, "Agent context is invalid");

            if (!TryGetDirective(context, AgentDirectiveType.Engage, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Engage);

            if (!TryResolveEnemyTarget(
                    directiveRequest,
                    agent,
                    out global::EnemyHealthController enemyHealthController))
            {
                // 目标丢失等价于本次战斗目标已结束，清理事实让状态机回落
                CompleteCombat(context);
                return Succeed();
            }

            if (enemyHealthController.GetCurrentHealthRatio() <= 0f)
            {
                // 敌人死亡结算交给 Enemy 系统，Agent 只收尾自身指令
                GameplayTargetRegistry.GetOrCreate().NotifyEnemyDefeated(enemyHealthController);
                CompleteCombat(context);
                return Succeed();
            }

            GameplayTargetRegistry.GetOrCreate().NotifyEnemyEngaged(enemyHealthController);

            Vector3 targetPosition = enemyHealthController.transform.position;
            float attackRange = GetFloat(context, AgentBlackboardKeys.AttackRange, 6f);
            float moveSpeed = GetFloat(context, AgentBlackboardKeys.MoveSpeed, 4f);
            // 攻击节点自己兜底靠近，避免目标移动后脱离射程
            if (!MoveAgentTowards(agent, targetPosition, attackRange, moveSpeed, context.DeltaTime))
                return Running();

            if (context.TimeSeconds < _nextAttackTime)
                return Running();

            if (TryCastReadySkill(agent, enemyHealthController, context.TimeSeconds, out float actionLockSeconds))
            {
                NotifyAttackAnimation(agent, actionLockSeconds);
                _nextAttackTime = context.TimeSeconds + Mathf.Max(0.05f, actionLockSeconds);
                if (enemyHealthController.GetCurrentHealthRatio() <= 0f)
                {
                    GameplayTargetRegistry.GetOrCreate().NotifyEnemyDefeated(enemyHealthController);
                    CompleteCombat(context);
                    return Succeed();
                }

                return Running();
            }

            // 攻击参数从黑板读取，便于按 Agent 类型替换配置
            float attackDamage = GetFloat(context, AgentBlackboardKeys.AttackDamage, 25f);
            float attackInterval = GetFloat(context, AgentBlackboardKeys.AttackInterval, 0.65f);

            if (!TryShootEnemy(agent, enemyHealthController, attackDamage))
            {
                // 子弹组件未配置时保留直接伤害兜底，避免 MVP 战斗链路被资产配置卡住
                enemyHealthController.TakeDamage(
                    attackDamage,
                    CreateAgentDamageContext(agent, enemyHealthController));
            }

            NotifyAttackAnimation(agent, attackInterval);
            _nextAttackTime = context.TimeSeconds + Mathf.Max(0.05f, attackInterval);

            if (enemyHealthController.GetCurrentHealthRatio() <= 0f)
            {
                GameplayTargetRegistry.GetOrCreate().NotifyEnemyDefeated(enemyHealthController);
                CompleteCombat(context);
                return Succeed();
            }

            return Running();
        }

        private void CompleteCombat(BehaviorTreeContext context)
        {
            SetFact(context, AgentBlackboardKeys.HasVisibleEnemy, false);
            ClearPendingDirective(context);
        }

        // 指令可以直接指向敌人，也可以指向活跃敌人群，节点只关心最终可攻击目标
        private bool TryResolveEnemyTarget(
            AgentDirectiveRequest directiveRequest,
            IAgentReadOnly agent,
            out global::EnemyHealthController enemyHealthController)
        {
            if (TryGetTargetComponent(
                    directiveRequest.TargetRef,
                    out ActiveEnemyClusterAuthoring enemyCluster))
            {
                return enemyCluster.TryGetNearestAliveEnemy(agent.Position, out enemyHealthController);
            }

            return TryGetTargetComponent(
                directiveRequest.TargetRef,
                out enemyHealthController);
        }

        private static bool TryShootEnemy(
            IAgentReadOnly agent,
            global::EnemyHealthController enemyHealthController,
            float attackDamage)
        {
            AgentCombatShooter shooter = agent.CachedTransform.GetComponent<AgentCombatShooter>();
            return shooter != null && shooter.TryShootAt(enemyHealthController, attackDamage);
        }

        private static bool TryCastReadySkill(
            IAgentReadOnly agent,
            global::EnemyHealthController enemyHealthController,
            double timeSeconds,
            out float actionLockSeconds)
        {
            actionLockSeconds = 0f;
            AgentCombatController combatController = agent.CachedTransform.GetComponent<AgentCombatController>();
            return combatController != null &&
                   combatController.TryCastReadySkill(enemyHealthController, timeSeconds, out actionLockSeconds);
        }

        private static void NotifyAttackAnimation(IAgentReadOnly agent, float suggestedDurationSeconds)
        {
            AgentPawnAnimationController animationController =
                agent.CachedTransform.GetComponent<AgentPawnAnimationController>();
            if (animationController != null)
                animationController.NotifyAttack(suggestedDurationSeconds);
        }

        private static global::EnemyDamageContext CreateAgentDamageContext(
            IAgentReadOnly agent,
            global::EnemyHealthController enemyHealthController)
        {
            Transform attacker = agent.CachedTransform;
            Vector3 hitPosition = enemyHealthController != null
                ? enemyHealthController.transform.position
                : attacker.position;
            Vector3 sourcePosition = attacker.position;
            Vector3 incomingDirection = hitPosition - sourcePosition;
            return global::EnemyDamageContext.FromAttacker(
                attacker,
                hitPosition,
                sourcePosition,
                incomingDirection,
                global::EnemyDamageSourceType.Projectile);
        }
    }
}
