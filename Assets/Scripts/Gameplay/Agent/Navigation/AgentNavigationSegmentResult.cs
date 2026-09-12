using UnityEngine;

namespace Gameplay.Agent.Navigation
{
    /// <summary>路段的只读测量事实；长度是导航角点的三维路程，不是加权成本或地图线长。</summary>
    public readonly struct AgentNavigationSegmentResult
    {
        public bool IsComplete { get; }
        public Vector3 Origin { get; }
        public Vector3 Destination { get; }
        public float Length { get; }
        public int CornerCount { get; }
        public string Failure { get; }

        internal AgentNavigationSegmentResult(Vector3 origin, Vector3 destination, float length, int cornerCount)
        {
            IsComplete = true; Origin = origin; Destination = destination;
            Length = length; CornerCount = cornerCount; Failure = null;
        }

        internal AgentNavigationSegmentResult(string failure)
        {
            IsComplete = false; Origin = default; Destination = default;
            Length = float.PositiveInfinity; CornerCount = 0; Failure = failure;
        }
    }
}
