using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>指令接受和移动共用的只读查询；负责角色就绪检查和地面到达容差。</summary>
    public static class AgentNavigationQuery
    {
        private static readonly Unity.Profiling.ProfilerMarker QueryMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Navigation.Check");
        public const float ArrivalTolerance = 0.1f;
        /// <summary>由一个调用方独占。返回的 Path 在该缓冲下一次查询之前有效。</summary>
        public sealed class Buffer
        {
            internal readonly AgentNavigationSegmentQuery.Buffer Segment = new AgentNavigationSegmentQuery.Buffer();
            internal NavMeshPath Path => Segment.Path;
            internal Vector3[] Corners { get => Segment.Corners; set => Segment.Corners = value; }
            internal string LastFailure => Segment.LastFailure;
            internal int LastCornerCount => Segment.LastCornerCount;
            public long CalculationCount => Segment.CalculationCount;
        }
        public static bool IsReady(NavMeshAgent agent) => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        public static AgentNavigationResult Check(NavMeshAgent agent, Vector3 target, float stoppingDistance)
            => Check(agent, target, stoppingDistance, new Buffer());

        public static AgentNavigationResult Check(NavMeshAgent agent, Vector3 target, float stoppingDistance, Buffer buffer)
        {
            using var markerScope = QueryMarker.Auto();
            if (buffer == null) throw new System.ArgumentNullException(nameof(buffer));
            var segment = AgentNavigationSegmentQuery.Calculate(agent, target, buffer.Segment);
            if (!segment.IsComplete) return new AgentNavigationResult(segment.Failure == "NotReady"
                ? AgentNavigationStatus.NotReady : AgentNavigationStatus.Unreachable);
            float tolerance = Mathf.Max(ArrivalTolerance, stoppingDistance);
            bool arrived = Vector3.Distance(segment.Origin, segment.Destination) <= tolerance + 0.02f && segment.Length <= tolerance + 0.05f;
            return new AgentNavigationResult(arrived ? AgentNavigationStatus.Arrived : AgentNavigationStatus.Moving,
                segment.Destination, buffer.Path);
        }
    }
}
