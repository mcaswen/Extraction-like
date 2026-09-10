using System.Collections.Generic;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Agent.Targeting
{
    /// <summary>Produces observed member facts; selection policy stays with Discovery/Decision.</summary>
    public sealed class AgentTargetCandidateCollector
    {
        private readonly List<global::EnemyHealthController> _enemies = new List<global::EnemyHealthController>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        public void CollectVisibleEnemies(IAgentReadOnly agent, IReadOnlyList<GameplayTargetClusterAuthoringBase> clusters,
            float range, List<AgentTargetCandidate> results)
        {
            results.Clear(); _seen.Clear();
            if (agent == null || agent.CachedTransform == null) return;
            Vector3 origin=CombatAimPointResolver.Resolve(agent.CachedTransform);
            foreach(var cluster in clusters)
            {
                if (!(cluster is ActiveEnemyClusterAuthoring active) || !active.isActiveAndEnabled || active.HasBeenCompleted) continue;
                active.CopyAliveEnemiesTo(_enemies);
                foreach(var enemy in _enemies)
                {
                    if (!_seen.Add(enemy.GetInstanceID()) || TargetVisibilityQuery.Check(agent.CachedTransform,origin,enemy.transform,range) != TargetVisibilityResult.Visible) continue;
                    Vector3 position=enemy.transform.position;
                    float distanceSqr=(CombatAimPointResolver.Resolve(enemy.transform)-origin).sqrMagnitude;
                    agent.Blackboard.TryGetValue(AgentBlackboardKeys.AttackRange,out float attackRange);
                    bool canExecute=distanceSqr<=attackRange*attackRange || IsReachable(agent,position,out _);
                    results.Add(new AgentTargetCandidate(cluster,enemy.gameObject,position,position,
                        distanceSqr,AgentTargetKind.Enemy,enemy,canExecute));
                }
            }
            results.Sort((a,b)=>a.DistanceSqr != b.DistanceSqr ? a.DistanceSqr.CompareTo(b.DistanceSqr) : a.Member.GetInstanceID().CompareTo(b.Member.GetInstanceID()));
        }
        public void CollectWorldTargets(IAgentReadOnly agent, IReadOnlyList<GameplayTargetClusterAuthoringBase> clusters,
            float range, List<AgentTargetCandidate> results)
        {
            results.Clear();
            if (agent == null || !AgentNavigationQuery.IsReady(agent.NavMeshAgent)) return;
            foreach (var cluster in clusters)
            {
                if (cluster == null || !cluster.isActiveAndEnabled || cluster.HasBeenCompleted) continue;
                if (cluster is ResourceClusterAuthoring resource)
                {
                    if (resource.TryGetNearestReachableIncompleteResource(agent.Position,agent.NavMeshAgent,out GameObject member,out Vector3 navigation))
                    {
                        float distance=(member.transform.position-agent.Position).sqrMagnitude;
                        if (distance<=range*range && IsReachable(agent,navigation,out Vector3 destination))
                            results.Add(new AgentTargetCandidate(cluster,member,member.transform.position,destination,distance,AgentTargetKind.Resource));
                    }
                }
                else if (cluster is ExtractionClusterAuthoring extraction)
                {
                    // Exits remain known beyond perception range; policy decides when to use the fallback.
                    foreach (var member in extraction.ExtractionMembers)
                    {
                        if (member == null || member.HasBeenCompleted || !member.TryGetComponent(out global::ExtractionPointController point) ||
                            !point.isActiveAndEnabled || !IsReachable(agent,point.transform.position,out Vector3 destination)) continue;
                        results.Add(new AgentTargetCandidate(cluster,point.gameObject,point.transform.position,destination,
                            (point.transform.position-agent.Position).sqrMagnitude,AgentTargetKind.Extraction));
                    }
                }
                else if (cluster is EnemySourceClusterAuthoring source)
                {
                    AgentTargetCandidate? nearest=null;
                    foreach (var point in source.SpawnPoints)
                    {
                        if (point == null || !point.gameObject.activeInHierarchy) continue;
                        float distance=(point.position-agent.Position).sqrMagnitude;
                        if (distance>range*range || (nearest.HasValue && distance>=nearest.Value.DistanceSqr) ||
                            !IsReachable(agent,point.position,out Vector3 destination)) continue;
                        nearest=new AgentTargetCandidate(cluster,point.gameObject,point.position,destination,distance,AgentTargetKind.EnemySource);
                    }
                    if (nearest.HasValue) results.Add(nearest.Value);
                }
            }
            results.Sort((a,b)=>a.DistanceSqr != b.DistanceSqr ? a.DistanceSqr.CompareTo(b.DistanceSqr) : a.Member.GetInstanceID().CompareTo(b.Member.GetInstanceID()));
        }

        private static bool IsReachable(IAgentReadOnly agent, Vector3 point, out Vector3 destination)
        {
            var result=AgentNavigationQuery.Check(agent.NavMeshAgent,point,0);
            destination=result.Destination;
            return result.Status==AgentNavigationStatus.Arrived || result.Status==AgentNavigationStatus.Moving;
        }

    }
}
