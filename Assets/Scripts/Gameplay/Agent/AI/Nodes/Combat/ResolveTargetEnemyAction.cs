using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：解析本帧要攻击的具体敌人写入交战记忆；指令可指向敌人本身或活跃敌人群（取群内最近的存活敌人）
    /// </summary>
    public sealed class ResolveTargetEnemyAction : AgentEngageActionBase
    {
        public ResolveTargetEnemyAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "解析目标敌人", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);
            if (!TryGetDirective(context, AgentDirectiveType.Engage, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Engage);

            Memory.Enemy = TryResolveEnemyTarget(directiveRequest, agent, out global::EnemyHealthController enemy) ? enemy : null;
            return Succeed();
        }

        private bool TryResolveEnemyTarget(
            AgentDirectiveRequest directiveRequest,
            IAgentReadOnly agent,
            out global::EnemyHealthController enemyHealthController)
        {
            // 明确指定的成员（或其碰撞体子物体）不能被祖先群重新选成另一只敌人。
            enemyHealthController = directiveRequest.TargetObject != null
                ? directiveRequest.TargetObject.GetComponentInParent<global::EnemyHealthController>() : null;
            if (enemyHealthController != null) return true;

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
    }
}
