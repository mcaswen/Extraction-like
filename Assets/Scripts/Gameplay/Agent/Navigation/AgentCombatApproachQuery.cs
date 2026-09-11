using Gameplay.Agent.Combat;
using Gameplay.Agent.Interfaces;
using Gameplay.Perception;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>只返回完整可达的接近位置；目标不可直达时，验证路径端点和地面射击候选。</summary>
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
            if (buffer.CalculationCount != before && path.status == NavMeshPathStatus.PathPartial)
            {
                int count = ReadCorners(buffer);
                if (count >= 2 && TryFiringCandidate(agent, enemy, attackRange,
                        buffer.Navigation.Corners[count - 1], buffer, out destination)) return true;
            }
            // 静止高处敌人可能没有自身导航锚点，寻找我们能站立并真实开火的地面位置。
            var nav = agent.NavMeshAgent;
            float groundHeight = nav.nextPosition.y - nav.baseOffset * Mathf.Abs(agent.CachedTransform.lossyScale.y);
            Vector3 center = new Vector3(enemy.transform.position.x, groundHeight, enemy.transform.position.z);
            Vector3 towardAgent = agent.Position - center; towardAgent.y = 0;
            towardAgent = towardAgent.sqrMagnitude > 0.0001f ? towardAgent.normalized : Vector3.back;
            var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
            float sampleRadius = Mathf.Max(0.5f, nav.radius * 2f);
            for (int ring = 0; ring < 3; ring++)
            {
                int directions = ring == 0 ? 1 : 8;
                float radius = Mathf.Max(0, attackRange) * (ring == 0 ? 0 : ring == 1 ? 0.5f : 0.85f);
                for (int i = 0; i < directions; i++)
                {
                    Vector3 probe = center + Quaternion.AngleAxis(i * 45f, Vector3.up) * towardAgent * radius;
                    if (NavMesh.SamplePosition(probe, out var hit, sampleRadius, filter) &&
                        TryFiringCandidate(agent, enemy, attackRange, hit.position, buffer, out destination)) return true;
                }
            }
            destination = default;
            return false;
        }

        private static int ReadCorners(Buffer buffer)
        {
            int count = buffer.Navigation.Path.GetCornersNonAlloc(buffer.Navigation.Corners);
            while (count == buffer.Navigation.Corners.Length)
            {
                buffer.Navigation.Corners = new Vector3[count * 2];
                count = buffer.Navigation.Path.GetCornersNonAlloc(buffer.Navigation.Corners);
            }
            return count;
        }

        private static bool TryFiringCandidate(IAgentReadOnly agent, global::EnemyHealthController enemy, float attackRange,
            Vector3 candidate, Buffer buffer, out Vector3 destination)
        {
            destination = default;
            var approach = AgentNavigationQuery.Check(agent.NavMeshAgent, candidate, 0, buffer.Navigation);
            if (approach.Failed || Vector3.Distance(approach.Destination, candidate) > 0.05f || ReadCorners(buffer) < 1) return false;
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
            destination = approach.Destination;
            return true;
        }
    }
}
