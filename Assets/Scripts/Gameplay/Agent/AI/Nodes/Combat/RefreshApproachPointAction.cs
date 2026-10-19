using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：需要时重新求"站在那里能开火"的接近点（每 0.1 秒，或敌人移动、导航参数变化、路径失效时）。
    /// 求不出时记为不可达，由后面的分支结束指令。
    /// </summary>
    public sealed class RefreshApproachPointAction : AgentEngageActionBase
    {
        public RefreshApproachPointAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "求接近点", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            Memory.ApproachUnreachable = false;
            var nav = agent.NavMeshAgent;
            global::EnemyHealthController enemy = Memory.Enemy;
            if (Memory.ApproachTime < 0 || context.TimeSeconds - Memory.ApproachTime >= 0.1d ||
                (Memory.ApproachEnemyPosition - enemy.transform.position).sqrMagnitude > 0.01f ||
                Memory.ApproachRange != Memory.AttackRange || Memory.ApproachAreaMask != nav.areaMask || Memory.ApproachAgentType != nav.agentTypeID ||
                Memory.ApproachRadius != nav.radius || Memory.ApproachHeight != nav.height || Memory.ApproachBaseOffset != nav.baseOffset ||
                Memory.ApproachScale != agent.CachedTransform.lossyScale || !nav.hasPath || nav.isPathStale)
            {
                if (!AgentCombatApproachQuery.TryResolve(agent, enemy, Memory.AttackRange, Memory.ApproachBuffer, out Memory.ApproachPosition))
                {
                    Memory.ApproachUnreachable = true;
                    return Succeed();
                }

                Memory.ApproachTime = context.TimeSeconds; Memory.ApproachEnemyPosition = enemy.transform.position;
                Memory.ApproachRange = Memory.AttackRange; Memory.ApproachAreaMask = nav.areaMask; Memory.ApproachAgentType = nav.agentTypeID;
                Memory.ApproachRadius = nav.radius; Memory.ApproachHeight = nav.height; Memory.ApproachBaseOffset = nav.baseOffset;
                Memory.ApproachScale = agent.CachedTransform.lossyScale;
            }

            return Succeed();
        }
    }
}
