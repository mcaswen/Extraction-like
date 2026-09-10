using System.Collections.Generic;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
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
                    results.Add(new AgentTargetCandidate(cluster,enemy.gameObject,position,position,
                        (CombatAimPointResolver.Resolve(enemy.transform)-origin).sqrMagnitude,AgentTargetKind.Enemy,enemy));
                }
            }
            results.Sort((a,b)=>a.DistanceSqr != b.DistanceSqr ? a.DistanceSqr.CompareTo(b.DistanceSqr) : a.Member.GetInstanceID().CompareTo(b.Member.GetInstanceID()));
        }
    }
}
