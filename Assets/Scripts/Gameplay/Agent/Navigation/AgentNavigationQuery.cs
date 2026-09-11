using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>Side-effect-free path checks shared by command acceptance and movement.</summary>
    public static class AgentNavigationQuery
    {
        private static readonly Unity.Profiling.ProfilerMarker QueryMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Navigation.Check");
        public const float ArrivalTolerance = 0.1f;
        /// <summary>Owned by one caller. A returned Path is valid until this buffer's next query.</summary>
        public sealed class Buffer
        {
            private NavMeshPath _path;
            internal NavMeshPath Path => _path ??= new NavMeshPath();
            internal Vector3[] Corners = new Vector3[32];
            internal string LastFailure;
            public long CalculationCount { get; internal set; }
        }
        public static bool IsReady(NavMeshAgent agent) => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        public static AgentNavigationResult Check(NavMeshAgent agent, Vector3 target, float stoppingDistance)
            => Check(agent, target, stoppingDistance, new Buffer());

        public static AgentNavigationResult Check(NavMeshAgent agent, Vector3 target, float stoppingDistance, Buffer buffer)
        {
            using var markerScope = QueryMarker.Auto();
            buffer.LastFailure = null;
            if (!IsReady(agent)) { buffer.LastFailure = "NotReady"; return new AgentNavigationResult(AgentNavigationStatus.NotReady); }
            float radius = Mathf.Max(0.5f, agent.radius * 2f);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, radius, filter))
            { buffer.LastFailure = "SampleMissing"; return new AgentNavigationResult(AgentNavigationStatus.Unreachable); }
            if (Mathf.Abs(hit.position.y - target.y) > Mathf.Max(0.5f, agent.height * 0.5f))
            { buffer.LastFailure = "SampleHeightMismatch"; return new AgentNavigationResult(AgentNavigationStatus.Unreachable); }
            var path = buffer.Path;
            buffer.CalculationCount++;
            if (!agent.CalculatePath(hit.position, path))
            { buffer.LastFailure = "CalculatePathRejected"; return new AgentNavigationResult(AgentNavigationStatus.Unreachable); }
            if (path.status != NavMeshPathStatus.PathComplete)
            { buffer.LastFailure = path.status == NavMeshPathStatus.PathPartial ? "PathPartial" : "PathInvalid"; return new AgentNavigationResult(AgentNavigationStatus.Unreachable); }
            float distance = 0f;
            // Serialized pawns use baseOffset=1. Path corners are on the surface;
            // comparing them with the elevated transform makes zero-distance arrival impossible.
            int count = path.GetCornersNonAlloc(buffer.Corners);
            while (count == buffer.Corners.Length)
            {
                buffer.Corners = new Vector3[buffer.Corners.Length * 2];
                count = path.GetCornersNonAlloc(buffer.Corners);
            }
            Vector3 current = count > 0 ? buffer.Corners[0] : agent.nextPosition - Vector3.up * agent.baseOffset;
            Vector3 previous = current;
            for (int i = 0; i < count; i++) { Vector3 corner = buffer.Corners[i]; distance += Vector3.Distance(previous, corner); previous = corner; }
            float tolerance = Mathf.Max(ArrivalTolerance, stoppingDistance);
            bool arrived = Vector3.Distance(current, hit.position) <= tolerance + 0.02f && distance <= tolerance + 0.05f;
            return new AgentNavigationResult(arrived ? AgentNavigationStatus.Arrived : AgentNavigationStatus.Moving, hit.position, path);
        }
    }
}
