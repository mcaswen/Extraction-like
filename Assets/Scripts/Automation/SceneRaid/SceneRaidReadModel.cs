#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Combat;
using Gameplay.Perception;
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
            public Vector3 position, velocity, desiredVelocity, destination, progressAnchor;
            public Vector3 eulerAngles, localScale, worldScale, nextPosition, steeringTarget, directivePosition;
            public Vector3 bodyBoundsCenter, bodyBoundsSize;
            public Vector3 lastMovementTarget;
            public string lastNavigationStatus;
            public int health;
            public bool onNavMesh, hasPath, pathPending, stopped;
            public float remainingDistance;
            public float radius, height, baseOffset, stoppingDistance, progressAge, progressTimeout, secondsSinceSetPath;
            public int avoidancePriority;
            public string avoidance;
            public string pathStatus;
            public float targetDistance3D, targetDistancePlanar, destinationDistancePlanar, transformToNavDistance, attackRange;
            public string lastShotResult, lastShotFailure;
            public bool attackReady, hasEnemy, hasResource, hasDirectivePosition, inventoryRequiresExtraction;
            public EnemyState enemy;
            public ResourceState resource;
        }
        [Serializable] public sealed class EnemyState
        {
            public string identity, bodyVisibility;
            public Vector3 position, eulerAngles, scale, aimPosition, navigationPosition, navNextPosition, bodyCenter, bodySize;
            public float navBaseOffset;
            public bool navReady;
            public float health, maximumHealth, shield, distance3D, distancePlanar, aimDistance;
            public bool active, died;
        }
        [Serializable] public sealed class ResourceState
        {
            public string identity, phase, lootState;
            public Vector3 position, navigationPosition, colliderCenter, colliderSize;
            public float distancePlanar, navigationDistancePlanar;
            public bool active, looted;
        }
        [Serializable] public sealed class Snapshot
        {
            public int frame, zones, clusters;
            public float timeScale;
            public string scene, raidOwner, mission;
            public string inventoryAgent;
            public bool inventoryOpen;
            public bool missionCompleted, missionFailed, requiredCaptured;
            public string[] requiredAgents, extractedAgents, settledAgents;
            public AgentState[] agents;
            public RangeState[] ranges;
            public ExtractionState[] extractionProgress;
        }
        [Serializable] public sealed class ExtractionState
        {
            public string agent, point;
            public float progressSeconds, durationSeconds;
        }
        [Serializable] public sealed class RangeState
        {
            public string identity, kind;
            public int inputs, points;
            public long builds, projections, lineWrites;
        }
        private readonly List<GameplayTargetClusterAuthoringBase> _rangeClusters = new List<GameplayTargetClusterAuthoringBase>();
        private RangeState[] _rangeStates = Array.Empty<RangeState>();
        private float _nextRangeSample;
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private readonly SceneRaidIdentityMap _identity;
        private readonly Func<string, AgentResourceInteractionEvent?> _resourceFact;
        public SceneRaidReadModel(SceneRaidIdentityMap identity, Func<string, AgentResourceInteractionEvent?> resourceFact)
        { _identity = identity; _resourceFact = resourceFact; }

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
            return ReadField<T>(raid, name);
        }
        private static T ReadField<T>(object owner, string name)
        {
            string key = owner.GetType().FullName + "." + name;
            if (!Fields.TryGetValue(key, out var field))
            {
                field = owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field == null) throw new MissingFieldException(owner.GetType().FullName, name);
                Fields.Add(key, field);
            }
            return (T)field.GetValue(owner);
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
                inventoryAgent = InventoryScreenController.Instance != null ? InventoryScreenController.Instance.ActiveInventoryAgentId : "",
                inventoryOpen = InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen,
                missionCompleted = RaidField<bool>(raid, "_isMissionCompleted"),
                missionFailed = RaidField<bool>(raid, "_isMissionFailed"),
                requiredCaptured = RaidField<bool>(raid, "_requiredExtractionAgentsCaptured"),
                requiredAgents = Sorted(RaidField<HashSet<string>>(raid, "_requiredExtractionAgentIds")),
                extractedAgents = Sorted(RaidField<HashSet<string>>(raid, "_extractedAgentIds")),
                settledAgents = Sorted(RaidField<HashSet<string>>(raid, "_settledExtractionAgentIds")),
                agents = states.ToArray(), ranges = CaptureRanges(targets), extractionProgress = CaptureExtractionProgress(raid)
            };
        }

        private ExtractionState[] CaptureExtractionProgress(RaidFlowController raid)
        {
            var progress = RaidField<IDictionary>(raid, "_activeExtractionProgressByAgentId");
            if (progress == null || progress.Count == 0) return Array.Empty<ExtractionState>();
            var result = new ExtractionState[progress.Count];
            int index = 0;
            foreach (DictionaryEntry pair in progress)
            {
                var point = ReadField<ExtractionPointController>(pair.Value, "ExtractionPoint");
                result[index++] = new ExtractionState { agent = (string)pair.Key, point = _identity.Get(point),
                    progressSeconds = ReadField<float>(pair.Value, "ProgressSeconds"),
                    durationSeconds = point != null ? point.ExtractionDurationSeconds : 0 };
            }
            return result;
        }

        private RangeState[] CaptureRanges(GameplayTargetRegistry registry)
        {
            if (Time.realtimeSinceStartup < _nextRangeSample) return _rangeStates;
            _nextRangeSample = Time.realtimeSinceStartup + 1f;
            _rangeClusters.Clear();
            registry?.CopyClustersTo(_rangeClusters);
            _rangeStates = new RangeState[_rangeClusters.Count];
            for (int i = 0; i < _rangeClusters.Count; i++)
            {
                var cluster = _rangeClusters[i];
                _rangeStates[i] = new RangeState { identity = _identity.Get(cluster), kind = cluster.GetType().Name,
                    inputs = cluster.RangeInputPointCount, points = cluster.RangePoints.Count,
                    builds = cluster.RangeGeometryBuildCount, projections = cluster.RangeGroundProjectionCount,
                    lineWrites = cluster.RangeLineWriteCount };
            }
            return _rangeStates;
        }
        private static string[] Sorted(HashSet<string> values) => values == null ? Array.Empty<string>() : values.OrderBy(x => x).ToArray();
        public AgentState CaptureDirective(AgentDirectiveRequest request)
        {
            var registry = AgentRuntimeRegistry.ActiveInstance;
            return registry != null && registry.TryGetHandle(request.TargetAgentId, out var handle) && handle.IsValid
                ? Capture(handle.PawnRoot, request) : null;
        }
        public SceneRaidNavigationEvidence.Record CaptureNavigation(AgentDirectiveRequest request)
        {
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null || !registry.TryGetHandle(request.TargetAgentId, out var handle) || !handle.IsValid) return null;
            var motor = ReadField<object>(handle.PawnRoot, "_navigationMotor");
            var destination = motor != null ? ReadField<Vector3>(motor, "_target") : request.TargetPosition;
            // 接近位置查询可能在调用 Motor 前失败；反击证据应查询当前敌人脚下，而不是上一条撤离路径。
            if (request.DirectiveType == AgentDirectiveType.Engage && request.TargetObject != null)
            {
                var enemy = request.TargetObject.GetComponentInParent<EnemyHealthController>();
                if (enemy != null) destination = Gameplay.Agent.Navigation.AgentCombatNavigationTarget.Resolve(enemy);
            }
            return SceneRaidNavigationEvidence.Capture(handle.PawnRoot, destination, _identity);
        }
        private AgentState Capture(AgentPawnRoot pawn, AgentDirectiveRequest? request = null)
        {
            var active = request ?? pawn.DirectiveLifecycle?.Active;
            var nav = pawn.NavMeshAgent;
            var motor = ReadField<object>(pawn, "_navigationMotor");
            bool onMesh = nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh;
            var body = pawn.GetComponent<Collider>();
            var shooter = pawn.GetComponent<AgentCombatShooter>();
            var combat = pawn.GetComponent<AgentCombatController>();
            float attackRange = pawn.Blackboard != null ? pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.AttackRange) : 0;
            Vector3 targetPosition = active.HasValue && active.Value.HasTargetPosition ? active.Value.TargetPosition : pawn.Position;
            EnemyHealthController enemy = active.HasValue && active.Value.TargetObject != null && active.Value.DirectiveType == AgentDirectiveType.Engage
                ? active.Value.TargetObject.GetComponentInParent<EnemyHealthController>() : null;
            var resourceState = CaptureResource(pawn, active?.CommandId);
            bool hasPosition = active.HasValue && active.Value.HasTargetPosition;
            return new AgentState
            {
                id = pawn.AgentIdValue, identity = _identity.Get(pawn), state = pawn.CurrentMacroStateName,
                health = pawn.CurrentHealth, position = pawn.Position, onNavMesh = onMesh,
                hasPath = onMesh && nav.hasPath, pathPending = onMesh && nav.pathPending,
                stopped = onMesh && nav.isStopped, velocity = onMesh ? nav.velocity : Vector3.zero,
                desiredVelocity = onMesh ? nav.desiredVelocity : Vector3.zero,
                eulerAngles = pawn.transform.eulerAngles, localScale = pawn.transform.localScale, worldScale = pawn.transform.lossyScale,
                nextPosition = onMesh ? nav.nextPosition : Vector3.zero,
                steeringTarget = onMesh && nav.hasPath ? nav.steeringTarget : Vector3.zero,
                bodyBoundsCenter = body != null ? body.bounds.center : pawn.Position,
                bodyBoundsSize = body != null ? body.bounds.size : Vector3.zero,
                lastMovementTarget = motor != null ? ReadField<Vector3>(motor, "_target") : Vector3.zero,
                lastNavigationStatus = motor != null ? ReadField<Gameplay.Agent.Navigation.AgentNavigationResult>(motor, "_lastResult").Status.ToString() : "",
                directivePosition = targetPosition,
                hasDirectivePosition = hasPosition, hasEnemy = enemy != null, hasResource = resourceState != null,
                inventoryRequiresExtraction = pawn.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.InventoryRequiresExtraction),
                targetDistance3D = hasPosition ? Vector3.Distance(pawn.Position, targetPosition) : -1,
                targetDistancePlanar = hasPosition ? PlanarDistance(pawn.Position, targetPosition) : -1,
                destinationDistancePlanar = onMesh && nav.hasPath ? PlanarDistance(pawn.Position, nav.destination) : -1,
                transformToNavDistance = onMesh ? Vector3.Distance(pawn.Position, nav.nextPosition) : -1,
                attackRange = attackRange, attackReady = combat != null && combat.IsAttackReady(Time.timeAsDouble),
                lastShotResult = shooter != null ? shooter.LastShotResult.ToString() : "NoShooter",
                lastShotFailure = shooter != null ? shooter.LastShotFailure.ToString() : "NoShooter",
                enemy = CaptureEnemy(pawn, enemy, attackRange), resource = resourceState,
                radius = nav != null ? nav.radius : 0, height = nav != null ? nav.height : 0,
                baseOffset = nav != null ? nav.baseOffset : 0, stoppingDistance = nav != null ? nav.stoppingDistance : 0,
                avoidancePriority = nav != null ? nav.avoidancePriority : -1,
                avoidance = nav != null ? nav.obstacleAvoidanceType.ToString() : "",
                progressAge = motor != null ? Time.time - ReadField<float>(motor, "_progressTime") : -1,
                progressTimeout = motor != null ? ReadField<float>(motor, "_progressTimeout") : -1,
                progressAnchor = motor != null ? ReadField<Vector3>(motor, "_progressPosition") : Vector3.zero,
                secondsSinceSetPath = motor != null ? Time.time - ReadField<float>(motor, "_lastPathTime") : -1,
                destination = onMesh && nav.hasPath ? nav.destination : Vector3.zero,
                remainingDistance = onMesh && nav.hasPath && !float.IsInfinity(nav.remainingDistance) ? nav.remainingDistance : -1,
                pathStatus = onMesh ? nav.pathStatus.ToString() : "NotOnNavMesh",
                commandId = active?.CommandId, directive = active?.DirectiveType.ToString(),
                targetId = active?.TargetId, target = active.HasValue ? _identity.Get(active.Value.TargetObject) : "",
                suspendedCommand = pawn.DirectiveLifecycle?.SuspendedExtraction?.CommandId
            };
        }
        private EnemyState CaptureEnemy(AgentPawnRoot pawn, EnemyHealthController enemy, float attackRange)
        {
            if (enemy == null) return null; // 群指令不猜测其内部选择，当前自主发现提交的是具体敌人。
            Vector3 aim = CombatAimPointResolver.Resolve(enemy.transform);
            Vector3 origin = CombatAimPointResolver.Resolve(pawn.transform);
            var navigation = enemy.GetComponent<NavMeshAgent>();
            var body = enemy.GetComponent<Collider>();
            bool navReady = Gameplay.Agent.Navigation.AgentNavigationQuery.IsReady(navigation);
            return new EnemyState
            {
                identity = _identity.Get(enemy), position = enemy.transform.position, eulerAngles = enemy.transform.eulerAngles,
                scale = enemy.transform.lossyScale, health = ReadField<float>(enemy, "_currentHealth"), maximumHealth = enemy.MaxHealth,
                shield = enemy.CurrentShield, died = ReadField<bool>(enemy, "_hasDied"), active = enemy.isActiveAndEnabled,
                distance3D = Vector3.Distance(pawn.Position, enemy.transform.position), distancePlanar = PlanarDistance(pawn.Position, enemy.transform.position),
                aimPosition = aim, aimDistance = Vector3.Distance(origin, aim),
                navigationPosition = Gameplay.Agent.Navigation.AgentCombatNavigationTarget.Resolve(enemy),
                navReady = navReady, navNextPosition = navReady ? navigation.nextPosition : Vector3.zero,
                navBaseOffset = navigation != null ? navigation.baseOffset : 0,
                bodyCenter = body != null ? body.bounds.center : enemy.transform.position,
                bodySize = body != null ? body.bounds.size : Vector3.zero,
                bodyVisibility = TargetVisibilityQuery.Check(pawn.transform, origin, enemy.transform, attackRange).ToString()
            };
        }
        private ResourceState CaptureResource(AgentPawnRoot pawn, string commandId)
        {
            var fact = _resourceFact?.Invoke(pawn.AgentIdValue);
            if (!fact.HasValue || fact.Value.CommandId != commandId || fact.Value.Resource == null) return null;
            var value = fact.Value;
            var collider = value.Resource.GetComponentInChildren<Collider>();
            var box = value.Resource.GetComponent<LootBoxEntity>();
            return new ResourceState
            {
                identity = _identity.Get(value.Resource), phase = value.Stage.ToString(), active = value.Resource.activeInHierarchy,
                position = value.Resource.transform.position, navigationPosition = value.NavigationPosition,
                distancePlanar = PlanarDistance(pawn.Position, value.Resource.transform.position),
                navigationDistancePlanar = PlanarDistance(pawn.Position, value.NavigationPosition),
                colliderCenter = collider != null ? collider.bounds.center : Vector3.zero,
                colliderSize = collider != null ? collider.bounds.size : Vector3.zero,
                lootState = box != null ? box.ResourceState.ToString() : "NotLootBox", looted = box != null && box.IsResourcePointLooted
            };
        }
        private static float PlanarDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
    }
}
#endif
