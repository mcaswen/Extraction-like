using System;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>只读导航路段查询。既不修改 Agent 路径，也不决定动作是否完成。</summary>
    public static class AgentNavigationSegmentQuery
    {
        private static readonly Unity.Profiling.ProfilerMarker QueryMarker =
            new Unity.Profiling.ProfilerMarker("Anomaly.Navigation.Segment");

        /// <summary>每个调用方独占；原生 Path/角点只在下一次查询前有效，测量结果可独立保存。</summary>
        public sealed class Buffer
        {
            private NavMeshPath _path;
            internal NavMeshPath Path => _path ??= new NavMeshPath();
            internal Vector3[] Corners = new Vector3[32];
            public string LastFailure { get; internal set; }
            public int LastCornerCount { get; internal set; }
            public long CalculationCount { get; internal set; }
            internal void Reset() { LastFailure = null; LastCornerCount = 0; }
        }

        /// <summary>只采样地面锚点并验证高度，不计算路径。</summary>
        public static bool TrySampleAnchor(AgentNavigationProfile profile, Vector3 candidate, out Vector3 position, out string failure)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return TrySample(candidate, profile.SampleRadius, profile.HeightTolerance, profile.Filter, out position, out failure);
        }

        /// <summary>无需场景中的 Agent，按快照查询两个地面锚点间的完整路径。</summary>
        public static AgentNavigationSegmentResult Calculate(AgentNavigationProfile profile, Vector3 origin,
            Vector3 destination, Buffer buffer)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            using var scope = QueryMarker.Auto();
            buffer.Reset();
            var filter = profile.Filter;
            if (!TrySample(origin, profile.SampleRadius, profile.HeightTolerance, filter, out var start, out string failure))
                return Fail(buffer, "Origin" + failure);
            if (!TrySample(destination, profile.SampleRadius, profile.HeightTolerance, filter, out var end, out failure))
                return Fail(buffer, failure);
            buffer.CalculationCount++;
            if (!NavMesh.CalculatePath(start, end, filter, buffer.Path)) return Fail(buffer, "CalculatePathRejected");
            return Measure(buffer, start, end);
        }

        /// <summary>保留原生 Agent 的区域成本和路径起点，只查询，不调用 SetPath/SetDestination。</summary>
        public static AgentNavigationSegmentResult Calculate(NavMeshAgent agent, Vector3 destination, Buffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            using var scope = QueryMarker.Auto();
            buffer.Reset();
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return Fail(buffer, "NotReady");
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!TrySample(destination, Mathf.Max(0.5f, agent.radius * 2), Mathf.Max(0.5f, agent.height * 0.5f),
                    filter, out var end, out string failure)) return Fail(buffer, failure);
            buffer.CalculationCount++;
            if (!agent.CalculatePath(end, buffer.Path)) return Fail(buffer, "CalculatePathRejected");
            Vector3 ground = agent.nextPosition - Vector3.up * agent.baseOffset * Mathf.Abs(agent.transform.lossyScale.y);
            return Measure(buffer, ground, end);
        }

        private static bool TrySample(Vector3 point, float radius, float heightTolerance, NavMeshQueryFilter filter,
            out Vector3 sampled, out string failure)
        {
            sampled = default;
            failure = null;
            if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) { failure = "SampleInvalid"; return false; }
            if (!NavMesh.SamplePosition(point, out var hit, radius, filter)) { failure = "SampleMissing"; return false; }
            if (Mathf.Abs(hit.position.y - point.y) > heightTolerance) { failure = "SampleHeightMismatch"; return false; }
            sampled = hit.position;
            return true;
        }

        private static AgentNavigationSegmentResult Measure(Buffer buffer, Vector3 fallbackOrigin, Vector3 destination)
        {
            var path = buffer.Path;
            // 失败时保留原生部分路径，现有交战接近查询仍可读取其末端。
            if (path.status != NavMeshPathStatus.PathComplete)
                return Fail(buffer, path.status == NavMeshPathStatus.PathPartial ? "PathPartial" : "PathInvalid");
            int count = path.GetCornersNonAlloc(buffer.Corners);
            while (count == buffer.Corners.Length)
            {
                buffer.Corners = new Vector3[buffer.Corners.Length * 2];
                count = path.GetCornersNonAlloc(buffer.Corners);
            }
            buffer.LastCornerCount = count;
            Vector3 origin = count > 0 ? buffer.Corners[0] : fallbackOrigin;
            double length = 0;
            for (int i = 1; i < count; i++) length += Vector3.Distance(buffer.Corners[i - 1], buffer.Corners[i]);
            if (double.IsNaN(length) || double.IsInfinity(length) || length > float.MaxValue)
                return Fail(buffer, "PathLengthInvalid");
            return new AgentNavigationSegmentResult(origin, destination, (float)length, count);
        }

        private static AgentNavigationSegmentResult Fail(Buffer buffer, string failure)
        {
            buffer.LastFailure = failure;
            return new AgentNavigationSegmentResult(failure);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
