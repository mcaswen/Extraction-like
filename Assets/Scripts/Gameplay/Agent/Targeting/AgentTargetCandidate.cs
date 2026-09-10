using Gameplay.Agent.Data;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Agent.Targeting
{
    public readonly struct AgentTargetCandidate
    {
        public GameplayTargetClusterAuthoringBase Cluster { get; }
        public GameObject Member { get; }
        public Vector3 Position { get; }
        public Vector3 NavigationPosition { get; }
        public float DistanceSqr { get; }
        public global::EnemyHealthController Enemy { get; }
        public AgentTargetKind Kind { get; }
        public AgentTargetCandidate(GameplayTargetClusterAuthoringBase cluster, GameObject member, Vector3 position,
            Vector3 navigationPosition, float distanceSqr, AgentTargetKind kind, global::EnemyHealthController enemy = null)
        { Cluster=cluster; Member=member; Position=position; NavigationPosition=navigationPosition; DistanceSqr=distanceSqr; Kind=kind; Enemy=enemy; }
    }
}
