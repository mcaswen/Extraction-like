#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine.AI;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidReadModel
    {
        [Serializable] public sealed class AgentState
        {
            public string id, identity, state, commandId, directive, target, targetId, suspendedCommand;
            public Vector3 position, velocity, destination;
            public int health;
            public bool onNavMesh, hasPath, pathPending, stopped;
            public float remainingDistance;
            public string pathStatus;
        }
        [Serializable] public sealed class Snapshot
        {
            public int frame, zones, clusters;
            public float timeScale;
            public string scene, raidOwner, mission;
            public bool missionCompleted, missionFailed, requiredCaptured;
            public string[] requiredAgents, extractedAgents, settledAgents;
            public AgentState[] agents;
        }
        private static readonly Dictionary<string, FieldInfo> RaidFields = new Dictionary<string, FieldInfo>();
        private readonly SceneRaidIdentityMap _identity;
        public SceneRaidReadModel(SceneRaidIdentityMap identity) { _identity = identity; }

        [Serializable] public sealed class TargetState
        { public string identity, targetId, kind, zone; public bool active, completed; }
        [Serializable] public sealed class ExitPath
        { public string agent, point, cluster, status; public float length; public Vector3 start, end; }
        [Serializable] public sealed class WorldState
        {
            public List<TargetState> targets = new List<TargetState>();
            public List<ExitPath> extractionPaths = new List<ExitPath>();
            public List<string> raidInstances = new List<string>();
        }
        // 就绪后的单次查询，明确标为诊断成本。不会给 NavMeshAgent 设置路径。
        public WorldState CaptureWorld()
        {
            var world = new WorldState();
            var clusters = new List<GameplayTargetClusterAuthoringBase>();
            GameplayTargetRegistry.ActiveInstance?.CopyClustersTo(clusters);
            foreach (var cluster in clusters)
            {
                world.targets.Add(new TargetState { identity = _identity.Get(cluster), targetId = cluster.TargetId,
                    kind = cluster.TargetKind.ToString(), active = cluster.isActiveAndEnabled, completed = cluster.HasBeenCompleted,
                    zone = _identity.Get(cluster.GetComponentInParent<TargetZoneAuthoring>()) });
                if (!(cluster is ExtractionClusterAuthoring extraction)) continue;
                var registry = AgentRuntimeRegistry.ActiveInstance;
                if (registry == null) continue;
                foreach (var member in extraction.ExtractionMembers)
                {
                    if (member?.EntityObject == null) continue;
                    foreach (var handle in registry.RegisteredAgents)
                    {
                        if (!handle.IsValid) continue;
                        var nav = handle.PawnRoot.NavMeshAgent;
                        var path = new NavMeshPath();
                        var exit = new ExitPath { agent = handle.AgentId.Value, point = _identity.Get(member.EntityObject),
                            cluster = _identity.Get(extraction), status = "NotOnNavMesh", start = handle.ReadOnly.Position,
                            end = member.Position, length = -1 };
                        if (nav != null && nav.isOnNavMesh)
                        {
                            bool found = nav.CalculatePath(member.Position, path);
                            exit.status = found ? path.status.ToString() : "CalculationFailed";
                            var corners = path.corners;
                            exit.length = 0;
                            for (int i = 1; i < corners.Length; i++) exit.length += Vector3.Distance(corners[i - 1], corners[i]);
                        }
                        world.extractionPaths.Add(exit);
                    }
                }
            }
            foreach (var raid in UnityEngine.Object.FindObjectsOfType<RaidFlowController>(true))
                world.raidInstances.Add(_identity.Get(raid) + " | " + raid.MissionName + " | owner=" + (raid == RaidFlowController.Instance));
            return world;
        }

        // 唯一的私有状态适配处；只读取，字段漂移直接失败，不使用默认值掩盖缺失。
        private static T RaidField<T>(RaidFlowController raid, string name)
        {
            if (raid == null) return default;
            if (!RaidFields.TryGetValue(name, out var field))
            {
                field = typeof(RaidFlowController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) throw new MissingFieldException(typeof(RaidFlowController).FullName, name);
                RaidFields.Add(name, field);
            }
            return (T)field.GetValue(raid);
        }
        public Snapshot Capture()
        {
            var registry = AgentRuntimeRegistry.ActiveInstance;
            var targets = GameplayTargetRegistry.ActiveInstance;
            var raid = RaidFlowController.Instance;
            var states = new List<AgentState>();
            if (registry != null)
                foreach (var handle in registry.RegisteredAgents)
                    if (handle.IsValid) states.Add(Capture(handle.PawnRoot));
            return new Snapshot
            {
                frame = Time.frameCount, timeScale = Time.timeScale,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                zones = targets != null ? targets.ZoneCount : 0, clusters = targets != null ? targets.ClusterCount : 0,
                raidOwner = _identity.Get(raid), mission = raid != null ? raid.MissionName : "",
                missionCompleted = RaidField<bool>(raid, "_isMissionCompleted"),
                missionFailed = RaidField<bool>(raid, "_isMissionFailed"),
                requiredCaptured = RaidField<bool>(raid, "_requiredExtractionAgentsCaptured"),
                requiredAgents = Sorted(RaidField<HashSet<string>>(raid, "_requiredExtractionAgentIds")),
                extractedAgents = Sorted(RaidField<HashSet<string>>(raid, "_extractedAgentIds")),
                settledAgents = Sorted(RaidField<HashSet<string>>(raid, "_settledExtractionAgentIds")),
                agents = states.ToArray()
            };
        }
        private static string[] Sorted(HashSet<string> values) => values == null ? Array.Empty<string>() : values.OrderBy(x => x).ToArray();
        private AgentState Capture(AgentPawnRoot pawn)
        {
            var active = pawn.DirectiveLifecycle?.Active;
            var nav = pawn.NavMeshAgent;
            bool onMesh = nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh;
            return new AgentState
            {
                id = pawn.AgentIdValue, identity = _identity.Get(pawn), state = pawn.CurrentMacroStateName,
                health = pawn.CurrentHealth, position = pawn.Position, onNavMesh = onMesh,
                hasPath = onMesh && nav.hasPath, pathPending = onMesh && nav.pathPending,
                stopped = onMesh && nav.isStopped, velocity = onMesh ? nav.velocity : Vector3.zero,
                destination = onMesh && nav.hasPath ? nav.destination : Vector3.zero,
                remainingDistance = onMesh && nav.hasPath && !float.IsInfinity(nav.remainingDistance) ? nav.remainingDistance : -1,
                pathStatus = onMesh ? nav.pathStatus.ToString() : "NotOnNavMesh",
                commandId = active?.CommandId, directive = active?.DirectiveType.ToString(),
                targetId = active?.TargetId, target = active.HasValue ? _identity.Get(active.Value.TargetObject) : "",
                suspendedCommand = pawn.DirectiveLifecycle?.SuspendedExtraction?.CommandId
            };
        }
    }
}
#endif
