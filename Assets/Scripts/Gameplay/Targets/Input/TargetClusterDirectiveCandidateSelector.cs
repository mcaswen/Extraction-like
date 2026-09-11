using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Targets.Input
{
    /// <summary>按成员距离尝试正式校验，不提交任务；全部失败仍交还首项供入口报告拒绝。</summary>
    public static class TargetClusterDirectiveCandidateSelector
    {
        public static AgentDirectiveRequest Select(IAgentReadOnly agent, List<AgentDirectiveRequest> candidates)
        {
            candidates.Sort((a, b) => Compare(agent.Position, a, b));
            foreach (var candidate in candidates)
                if (AgentDirectiveValidationService.Validate(agent, candidate) == AgentDirectiveFailure.None) return candidate;
            return candidates[0];
        }

        private static int Compare(Vector3 origin, AgentDirectiveRequest a, AgentDirectiveRequest b)
        {
            Vector3 da = a.TargetObject.transform.position - origin, db = b.TargetObject.transform.position - origin;
            int distance = (da.x * da.x + da.z * da.z).CompareTo(db.x * db.x + db.z * db.z);
            return distance != 0 ? distance : a.TargetObject.GetInstanceID().CompareTo(b.TargetObject.GetInstanceID());
        }
    }
}
