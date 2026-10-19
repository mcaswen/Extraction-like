using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：玩家手动交战、还没看见目标时，按第一次求出的路径长度计算一次性接近预算
    /// （2 × 路径长度 ÷ 移动速度 + 失视时限）。只算一次，追动态目标不能无限续期。
    /// 初次路径不可达或预算耗尽都记入记忆，由后面的分支结束指令。
    /// </summary>
    public sealed class CheckApproachBudgetAction : AgentEngageActionBase
    {
        public CheckApproachBudgetAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "检查初次接近预算", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            Memory.BudgetPathFailed = false;
            Memory.BudgetExpired = false;
            if (Memory.ApproachUnreachable || !Memory.InitialManualApproach)
                return Succeed();
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            if (Memory.InitialApproachDeadline < 0d)
            {
                var initialPath = AgentNavigationQuery.Check(agent.NavMeshAgent, Memory.ApproachPosition, 0f);
                if (initialPath.Failed || initialPath.Path == null)
                {
                    Memory.BudgetPathFailed = true;
                    return Succeed();
                }

                var corners = initialPath.Path.corners;
                float length = 0;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                Memory.InitialApproachDeadline = context.TimeSeconds + 2d * length / Mathf.Max(0.1f, Memory.MoveSpeed) + Memory.LostSightTimeout;
            }

            Memory.BudgetExpired = context.TimeSeconds >= Memory.InitialApproachDeadline;
            return Succeed();
        }
    }
}
