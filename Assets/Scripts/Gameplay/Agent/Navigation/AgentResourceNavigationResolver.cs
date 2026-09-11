using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>One action node's short-lived resource query. Selection remains with the cluster.</summary>
    public sealed class AgentResourceNavigationResolver
    {
        private ResourceClusterAuthoring _cluster;
        private NavMeshAgent _agent;
        private GameObject _resource;
        private Vector3 _origin, _memberPosition, _destination;
        private float _queryTime = -999f;
        private int _areaMask;

        public bool TryResolve(ResourceClusterAuthoring cluster, Vector3 origin, NavMeshAgent agent,
            out GameObject resource, out Vector3 destination)
        {
            bool valid = cluster != null && cluster.isActiveAndEnabled && cluster == _cluster && agent == _agent &&
                AgentNavigationQuery.IsReady(agent) && _areaMask == agent.areaMask &&
                Time.time >= _queryTime && Time.time - _queryTime < 0.1f &&
                (origin - _origin).sqrMagnitude < 0.25f && _resource != null && _resource.activeInHierarchy &&
                (_resource.transform.position - _memberPosition).sqrMagnitude < 0.0001f;
            if (valid)
            {
                valid = false;
                foreach (var member in cluster.ResourceMembers)
                    if (member != null && member.EntityObject == _resource && !member.HasBeenCompleted)
                    { valid = true; break; }
            }
            if (!valid)
            {
                _cluster = cluster; _agent = agent; _origin = origin; _queryTime = Time.time;
                _areaMask = agent != null ? agent.areaMask : 0;
                _resource = null; _destination = default;
                if (cluster != null && cluster.TryGetNearestReachableIncompleteResource(origin, agent, out _resource, out _destination))
                    _memberPosition = _resource.transform.position;
            }
            resource = _resource; destination = _destination;
            return resource != null;
        }
    }
}
