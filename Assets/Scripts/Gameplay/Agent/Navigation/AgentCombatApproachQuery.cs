using Gameplay.Agent.Combat;
using Gameplay.Agent.Interfaces;
using Gameplay.Perception;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>只返回完整可达的接近位置，部分路径仅提供一个待验证的射击候选。</summary>
    public static class AgentCombatApproachQuery
    {
        public sealed class Buffer
        {
            internal readonly AgentNavigationQuery.Buffer Navigation = new AgentNavigationQuery.Buffer();
            public long CalculationCount => Navigation.CalculationCount;
        }

        public static bool TryResolve(IAgentReadOnly agent, global::EnemyHealthController enemy, float attackRange,
            Buffer buffer, out Vector3 destination)
        {
            destination = AgentCombatNavigationTarget.Resolve(enemy);
            if (agent == null || enemy == null || !enemy.IsAlive || !AgentNavigationQuery.IsReady(agent.NavMeshAgent)) return false;
            long before = buffer.CalculationCount;
            var direct = AgentNavigationQuery.Check(agent.NavMeshAgent, destination, 0, buffer.Navigation);
            if (!direct.Failed) { destination = direct.Destination; return true; }
            var path = buffer.Navigation.Path;
            if (buffer.CalculationCount == before || path.status != NavMeshPathStatus.PathPartial) return false;
            int count = path.GetCornersNonAlloc(buffer.Navigation.Corners);
            while (count == buffer.Navigation.Corners.Length)
            {
                buffer.Navigation.Corners = new Vector3[count * 2];
                count = path.GetCornersNonAlloc(buffer.Navigation.Corners);
            }
            if (count < 2) return false;
            Vector3 candidate = buffer.Navigation.Corners[count - 1];
            Vector3 surfaceOrigin = buffer.Navigation.Corners[0];
            // 身体和枪口保留实际缩放、baseOffset 和动画偏移，不把脚下点当射线起点。
            Vector3 prospectivePosition = agent.CachedTransform.position + candidate - surfaceOrigin;
            Vector3 bodyAim = CombatAimPointResolver.Resolve(agent.CachedTransform) + candidate - surfaceOrigin;
            if (TargetVisibilityQuery.Check(agent.CachedTransform, bodyAim, enemy.transform, attackRange) != TargetVisibilityResult.Visible)
                return false;
            var shooter = agent.CachedTransform.GetComponent<AgentCombatShooter>();
            Vector3 facing = enemy.transform.position - prospectivePosition; facing.y = 0;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing, Vector3.up) : agent.CachedTransform.rotation;
            if (shooter == null || !shooter.CanShootFrom(enemy, attackRange, prospectivePosition, rotation)) return false;
            var approach = AgentNavigationQuery.Check(agent.NavMeshAgent, candidate, 0, buffer.Navigation);
            if (approach.Failed || Vector3.Distance(approach.Destination, candidate) > 0.05f) return false;
            destination = approach.Destination;
            return true;
        }
    }
}
