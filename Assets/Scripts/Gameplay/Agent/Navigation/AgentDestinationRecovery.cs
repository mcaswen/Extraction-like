using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>One native destination request after a verified path could not be assigned.
    /// The motor owns deadlines, cancellation of movement and the original command.</summary>
    public sealed class AgentDestinationRecovery
    {
        private readonly NavMeshAgent _agent;
        private Vector3 _destination;
        public bool HasRequest { get; private set; }

        public AgentDestinationRecovery(NavMeshAgent agent) { _agent = agent; }

        // The caller must first validate a complete path to this sampled destination.
        public bool Begin(Vector3 destination)
        {
            _destination = destination;
            HasRequest = AgentNavigationQuery.IsReady(_agent) && _agent.SetDestination(destination);
            return HasRequest;
        }

        public AgentNavigationStatus Poll()
        {
            if (!HasRequest || !AgentNavigationQuery.IsReady(_agent)) return AgentNavigationStatus.Unreachable;
            if (_agent.pathPending) return AgentNavigationStatus.NotReady;
            return _agent.hasPath && !_agent.isPathStale && _agent.pathStatus == NavMeshPathStatus.PathComplete &&
                (_agent.pathEndPosition - _destination).sqrMagnitude <= 0.0025f
                ? AgentNavigationStatus.Moving : AgentNavigationStatus.Unreachable;
        }

        public void Reset() { HasRequest = false; }
    }
}
