using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：在资源群里找最近的可达未处理成员。找不到时区分"群已处理完"和"还有成员但走不到"
    /// </summary>
    public sealed class ResolveNextMemberAction : AgentSearchActionBase
    {
        public ResolveNextMemberAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "解析下一成员", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);

            if (!ResourceNavigation.TryResolve(Memory.Cluster,
                    agent.Position,
                    agent.NavMeshAgent,
                    out GameObject resourceObject,
                    out Vector3 resourceNavigationPosition))
            {
                Memory.Resolution = Memory.Cluster.HasPendingResources()
                    ? AgentSearchMemberResolution.Unreachable
                    : AgentSearchMemberResolution.ClusterDone;
                return Succeed();
            }

            Memory.Resolution = AgentSearchMemberResolution.Member;
            Memory.Member = resourceObject;
            Memory.MemberNavigationPosition = resourceNavigationPosition;
            Memory.HasMemberNavigationPosition = true;
            Memory.ClearDirectiveOnComplete = false;
            return Succeed();
        }
    }
}
