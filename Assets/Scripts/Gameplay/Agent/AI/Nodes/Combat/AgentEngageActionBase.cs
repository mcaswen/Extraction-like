using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Animation;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Runtime;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 交战子树动作的公共基类：持有交战记忆，提供收尾战斗等共用操作
    /// </summary>
    public abstract class AgentEngageActionBase : AgentActionNodeBase
    {
        protected const string InvalidAgent = "Agent context is invalid";

        protected readonly AgentEngageMemory Memory;

        protected AgentEngageActionBase(string nodeName, string displayName, AgentEngageMemory memory)
            : base(nodeName)
        {
            Memory = memory;
            DisplayName = displayName;
        }

        // 目标结束：清可见敌人事实并释放指令，让状态机回落
        protected void CompleteCombat(BehaviorTreeContext context)
        {
            SetFact(context, AgentBlackboardKeys.HasVisibleEnemy, false);
            ClearPendingDirective(context);
        }

        // 敌人死亡结算交给 Enemy 系统，Agent 只收尾自身指令
        protected bool TryConcludeDefeated(BehaviorTreeContext context)
        {
            if (Memory.Enemy.GetCurrentHealthRatio() > 0f)
                return false;

            GameplayTargetRegistry.GetOrCreate().NotifyEnemyDefeated(Memory.Enemy);
            CompleteCombat(context);
            return true;
        }

        protected static void NotifyAttackAnimation(IAgentReadOnly agent, float suggestedDurationSeconds)
        {
            AgentPawnAnimationController animationController =
                agent.CachedTransform.GetComponent<AgentPawnAnimationController>();
            if (animationController != null)
                animationController.NotifyAttack(suggestedDurationSeconds);
        }
    }
}
