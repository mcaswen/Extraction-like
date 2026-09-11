#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>按需捕获群成员和导航证据，不刷新群状态、不提交命令或改变角色路径。</summary>
    public sealed class SceneRaidClusterCatalog
    {
        [Serializable] public sealed class Snapshot
        {
            public int schemaVersion = 1, frame;
            public string scene, focusedAgent;
            public List<Actor> agents = new List<Actor>();
            public List<Cluster> clusters = new List<Cluster>();
        }
        [Serializable] public sealed class Actor
        {
            public string id, identity;
            public Vector3 position;
            public float discoveryRange, interactionRange, moveSpeed;
            public bool alive, navigationReady;
        }
        [Serializable] public sealed class Cluster
        {
            public string identity, targetId, kind;
            public bool active, completed;
            public List<Member> members = new List<Member>();
        }
        [Serializable] public sealed class Member
        {
            public string identity, sourceIdentity, sourceTargetId, parentIdentity;
            public Vector3 position;
            public bool active, completed;
            public List<Approach> approaches = new List<Approach>();
        }
        [Serializable] public sealed class Approach
        {
            public string agent, distanceClass, visibility, pathStatus, pathMeaning;
            public float distancePlanar, distance3D, pathLength = -1;
            public Vector3 queryPosition, sampledPosition;
            public bool sampled, completePath;
        }

        private readonly SceneRaidIdentityMap _identity;
        private readonly List<GameplayTargetClusterAuthoringBase> _clusters = new List<GameplayTargetClusterAuthoringBase>();
        private readonly List<global::EnemyHealthController> _enemies = new List<global::EnemyHealthController>();
        private readonly NavMeshPath _path = new NavMeshPath();
        private Vector3[] _corners = new Vector3[32];

        public SceneRaidClusterCatalog(SceneRaidIdentityMap identity) { _identity = identity; }

        public GameplayTargetClusterAuthoringBase Resolve(string identity) => _clusters.FirstOrDefault(x => x != null && _identity.Get(x) == identity);

        public static Cluster Select(Snapshot snapshot, SceneRaidCommandScenario.Selector selector, string agent, string excludedIdentity)
        {
            string kind = selector.kind == "ActiveEnemy" ? "Enemy" : selector.kind;
            return snapshot.clusters.Where(x => x.active && !x.completed && x.kind == kind && x.identity != excludedIdentity &&
                    (!selector.singleton || x.members.Count(m => m.active && !m.completed) == 1))
                .Select(x => new { cluster = x, nearest = x.members.Where(m => m.active && !m.completed)
                    .SelectMany(m => m.approaches).Where(a => a.agent == agent).OrderBy(a => a.distancePlanar).FirstOrDefault() })
                .Where(x => x.nearest != null && (selector.distance == "Any" || x.nearest.distanceClass == selector.distance))
                .OrderBy(x => x.nearest.distancePlanar).ThenBy(x => x.cluster.identity, StringComparer.Ordinal)
                .Select(x => x.cluster).FirstOrDefault();
        }

        public Snapshot Capture()
        {
            var registry = AgentRuntimeRegistry.ActiveInstance;
            var targets = GameplayTargetRegistry.ActiveInstance;
            if (registry == null || targets == null) throw new InvalidOperationException("Cluster catalog registries are not ready.");
            var handles = registry.RegisteredAgents.Where(x => x.IsValid)
                .OrderBy(x => x.AgentId.Value, StringComparer.Ordinal).ToArray();
            var result = new Snapshot { frame = Time.frameCount,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, focusedAgent = registry.FocusedAgentId.Value };
            foreach (var handle in handles)
            {
                var pawn = handle.PawnRoot;
                result.agents.Add(new Actor { id = handle.AgentId.Value, identity = _identity.Get(pawn),
                    position = pawn.Position, discoveryRange = pawn.TargetDiscoveryRange,
                    interactionRange = pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.InteractionDistance),
                    moveSpeed = pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.MoveSpeed),
                    alive = handle.IsAlive, navigationReady = AgentNavigationQuery.IsReady(pawn.NavMeshAgent) });
            }
            targets.CopyClustersTo(_clusters);
            foreach (var cluster in _clusters.Where(IsPlayerCluster).OrderBy(x => _identity.Get(x), StringComparer.Ordinal))
            {
                var row = new Cluster { identity = _identity.Get(cluster), targetId = cluster.TargetId,
                    kind = cluster.TargetKind.ToString(), active = cluster.isActiveAndEnabled, completed = cluster.HasBeenCompleted };
                if (cluster is ResourceClusterAuthoring resource)
                {
                    foreach (var member in resource.ResourceMembers)
                        if (member?.EntityObject != null) row.members.Add(CaptureMember(member.EntityObject,
                            member.HasBeenCompleted, handles, null, "SampledPivotOnly"));
                }
                else if (cluster is ExtractionClusterAuthoring extraction)
                {
                    foreach (var member in extraction.ExtractionMembers)
                        if (member?.EntityObject != null) row.members.Add(CaptureMember(member.EntityObject,
                            member.HasBeenCompleted, handles, null, "ExtractionPointPath"));
                }
                else if (cluster is ActiveEnemyClusterAuthoring active)
                {
                    active.CopyAliveEnemiesTo(_enemies);
                    foreach (var enemy in _enemies)
                    {
                        var member = CaptureMember(enemy.gameObject, false, handles, enemy, "EnemySurfacePathOnly");
                        if (active.TryGetSourceTargetIdForEnemy(enemy, out string sourceId))
                        {
                            member.sourceTargetId = sourceId;
                            if (targets.TryGetTarget(sourceId, out var source)) member.sourceIdentity = _identity.Get(source);
                        }
                        row.members.Add(member);
                    }
                }
                row.members.Sort((a, b) => StringComparer.Ordinal.Compare(a.identity, b.identity));
                result.clusters.Add(row);
            }
            return result;
        }

        public static bool IsPlayerCluster(GameplayTargetClusterAuthoringBase cluster) =>
            cluster is ResourceClusterAuthoring || cluster is ActiveEnemyClusterAuthoring || cluster is ExtractionClusterAuthoring;

        public static string ClassifyDistance(float distance, float discoveryRange, float interactionRange)
        {
            if (discoveryRange <= 0) return "UndefinedRange";
            if (distance <= interactionRange) return "Interaction";
            if (distance <= discoveryRange) return "Near";
            return distance >= 2 * discoveryRange ? "Far" : "Middle";
        }

        private Member CaptureMember(GameObject obj, bool completed, AgentRuntimeHandle[] handles,
            global::EnemyHealthController enemy, string meaning)
        {
            var row = new Member { identity = _identity.Get(obj), position = obj.transform.position,
                parentIdentity = _identity.Get(obj.transform.parent), active = obj.activeInHierarchy, completed = completed };
            foreach (var handle in handles)
            {
                var pawn = handle.PawnRoot;
                Vector3 delta = row.position - pawn.Position;
                var approach = new Approach { agent = handle.AgentId.Value,
                    distancePlanar = new Vector2(delta.x, delta.z).magnitude, distance3D = delta.magnitude,
                    pathMeaning = meaning, visibility = "NotEnemy", pathStatus = "NavigationNotReady",
                    queryPosition = enemy != null ? AgentCombatNavigationTarget.Resolve(enemy) : row.position };
                approach.distanceClass = ClassifyDistance(approach.distancePlanar, pawn.TargetDiscoveryRange,
                    pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.InteractionDistance));
                if (enemy != null) approach.visibility = TargetVisibilityQuery.Check(pawn.transform,
                    CombatAimPointResolver.Resolve(pawn.transform), enemy.transform, pawn.TargetDiscoveryRange).ToString();
                var nav = pawn.NavMeshAgent;
                if (AgentNavigationQuery.IsReady(nav))
                {
                    var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
                    // 资源 pivot 采样仅用于场景调查，不代替正式箱边候选、占位或命令校验。
                    float radius = meaning == "SampledPivotOnly" ? 4f : Mathf.Max(0.5f, nav.radius * 2f);
                    approach.sampled = NavMesh.SamplePosition(approach.queryPosition, out var hit, radius, filter);
                    approach.pathStatus = "NoSample";
                    if (approach.sampled)
                    {
                        approach.sampledPosition = hit.position;
                        _path.ClearCorners();
                        bool found = nav.CalculatePath(hit.position, _path);
                        approach.pathStatus = found ? _path.status.ToString() : "CalculationFailed";
                        approach.completePath = found && _path.status == NavMeshPathStatus.PathComplete;
                        int count = _path.GetCornersNonAlloc(_corners);
                        while (count == _corners.Length)
                        { _corners = new Vector3[count * 2]; count = _path.GetCornersNonAlloc(_corners); }
                        if (count > 0)
                        {
                            approach.pathLength = 0;
                            for (int i = 1; i < count; i++) approach.pathLength += Vector3.Distance(_corners[i - 1], _corners[i]);
                        }
                    }
                }
                row.approaches.Add(approach);
            }
            return row;
        }
    }
}
#endif
