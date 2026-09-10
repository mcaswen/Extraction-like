using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>Side-effect-free path checks shared by command acceptance and movement.</summary>
    public static class AgentNavigationQuery
    {
        public const float ArrivalTolerance = 0.1f;
        public static bool IsReady(NavMeshAgent agent) => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        public static AgentNavigationResult Check(NavMeshAgent agent, Vector3 target, float stoppingDistance)
        {
            if (!IsReady(agent)) return new AgentNavigationResult(AgentNavigationStatus.NotReady);
            float radius = Mathf.Max(0.5f, agent.radius * 2f);
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, radius, agent.areaMask) ||
                Mathf.Abs(hit.position.y - target.y) > Mathf.Max(0.5f, agent.height * 0.5f))
                return new AgentNavigationResult(AgentNavigationStatus.Unreachable);
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return new AgentNavigationResult(AgentNavigationStatus.Unreachable);
            float distance = 0f;
            // Serialized pawns use baseOffset=1. Path corners are on the surface;
            // comparing them with the elevated transform makes zero-distance arrival impossible.
            Vector3 current = path.corners.Length > 0 ? path.corners[0] : agent.nextPosition - Vector3.up * agent.baseOffset;
            Vector3 previous = current;
            foreach (Vector3 corner in path.corners) { distance += Vector3.Distance(previous, corner); previous = corner; }
            float tolerance = Mathf.Max(ArrivalTolerance, stoppingDistance);
            bool arrived = Vector3.Distance(current, hit.position) <= tolerance + 0.02f && distance <= tolerance + 0.05f;
            return new AgentNavigationResult(arrived ? AgentNavigationStatus.Arrived : AgentNavigationStatus.Moving, hit.position, path);
        }
    }
}
