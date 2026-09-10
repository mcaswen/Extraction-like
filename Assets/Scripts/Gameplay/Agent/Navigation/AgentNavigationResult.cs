using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    public enum AgentNavigationStatus { Moving, Arrived, NotReady, Unreachable, Stalled }

    public readonly struct AgentNavigationResult
    {
        public AgentNavigationStatus Status { get; }
        public Vector3 Destination { get; }
        public NavMeshPath Path { get; }
        public bool Failed => Status == AgentNavigationStatus.Unreachable || Status == AgentNavigationStatus.Stalled;
        public AgentNavigationResult(AgentNavigationStatus status, Vector3 destination = default, NavMeshPath path = null)
        { Status = status; Destination = destination; Path = path; }
    }
}
