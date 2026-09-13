#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>仅失败事件触发的只读诊断；每个 Agent/群最多一次，不参与实际候选接受或移动。</summary>
    public sealed class SceneRaidEnemyProcessingProbe
    {
        [Serializable] public sealed class EnemyRecord
        {
            public string identity, validation, visibility, directPathStatus;
            public Vector3 position, aim, navigation, approach, independentFiringPosition;
            public float health, aimDistance;
            public bool directCalculated, directQueryFailed, approachResolved, independentFiringFound;
            public long approachQueries;
            public Vector3[] pathCorners;
            public string[] aimOverlaps;
        }
        [Serializable] public sealed class Record
        {
            public string agent, node, cluster;
            public Vector3 position, groundOrigin, bodyAim;
            public float range;
            public EnemyRecord[] enemies;
            public SceneRaidNavigationEvidence.Record neighborhood;
        }
        private readonly HashSet<string> _captured = new HashSet<string>(StringComparer.Ordinal);
        private readonly SceneRaidIdentityMap _identity = new SceneRaidIdentityMap();

        public Record CaptureOnce(AgentPawnRoot pawn, MapGraphBindingAuthoring binding)
        {
            if (pawn == null || binding == null || pawn.IsDead || _captured.Count >= 8) return null;
            string node = pawn.RouteSnapshot.CurrentNodeId;
            var target = binding.TargetBindings.FirstOrDefault(x => x.NodeId == node)?.DirectTarget;
            var active = target is EnemySourceClusterAuthoring source ? source.ConfiguredActiveEnemyCluster : target as ActiveEnemyClusterAuthoring;
            if (active == null || !_captured.Add(pawn.AgentIdValue + ":" + node)) return null;
            var enemies = new List<global::EnemyHealthController>(); active.CopyAliveEnemiesTo(enemies);
            pawn.Blackboard.TryGetValue(AgentBlackboardKeys.AttackRange, out float range);
            var nav = pawn.NavMeshAgent;
            Vector3 ground = AgentNavigationQuery.IsReady(nav)
                ? nav.nextPosition - Vector3.up * nav.baseOffset * Mathf.Abs(pawn.transform.lossyScale.y) : pawn.Position;
            var record = new Record { agent = pawn.AgentIdValue, node = node, cluster = _identity.Get(target), position = pawn.Position,
                groundOrigin = ground, bodyAim = CombatAimPointResolver.Resolve(pawn.transform), range = range,
                neighborhood = SceneRaidNavigationEvidence.Capture(pawn, pawn.RouteSnapshot.CurrentStep.Anchor, _identity) };
            record.enemies = enemies.Select(enemy => CaptureEnemy(pawn, enemy, ground, range)).ToArray();
            return record;
        }

        private EnemyRecord CaptureEnemy(AgentPawnRoot pawn, global::EnemyHealthController enemy, Vector3 ground, float range)
        {
            Vector3 aim = CombatAimPointResolver.Resolve(enemy.transform), navigation = AgentCombatNavigationTarget.Resolve(enemy);
            var request = AgentDirectiveRequest.EngageConcreteEnemy(enemy.gameObject, "processing-probe", pawn.AgentId, "processing-probe", 0);
            var buffer = new AgentCombatApproachQuery.Buffer();
            var row = new EnemyRecord { identity = _identity.Get(enemy), position = enemy.transform.position, aim = aim, navigation = navigation,
                health = enemy.GetCurrentHealthRatio() * enemy.MaxHealth, aimDistance = Vector3.Distance(CombatAimPointResolver.Resolve(pawn.transform), aim),
                visibility = TargetVisibilityQuery.Check(pawn.transform, CombatAimPointResolver.Resolve(pawn.transform), enemy.transform, range).ToString(),
                validation = AgentDirectiveValidationService.Validate(pawn, request).ToString(),
                aimOverlaps = Physics.OverlapSphere(aim, .02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .Where(x => !TargetVisibilityQuery.BelongsTo(x.transform, enemy.transform)).Select(x => _identity.Get(x)).ToArray() };
            row.approachResolved = AgentCombatApproachQuery.TryResolve(pawn, enemy, range, buffer, out row.approach);
            row.approachQueries = buffer.CalculationCount;
            if (AgentNavigationQuery.IsReady(pawn.NavMeshAgent))
            {
                var path = new NavMeshPath(); row.directCalculated = pawn.NavMeshAgent.CalculatePath(navigation, path);
                row.directPathStatus = path.status.ToString(); row.pathCorners = path.corners;
                row.directQueryFailed = AgentNavigationQuery.Check(pawn.NavMeshAgent, navigation, 0).Failed;
                row.independentFiringFound = FindDiagnosticFiringPosition(pawn, enemy, ground, range, out row.independentFiringPosition);
            }
            return row;
        }

        // 独立扩大诊断取样，找不到不能证明不存在；找到也不写回正式路线。
        private static bool FindDiagnosticFiringPosition(AgentPawnRoot pawn, global::EnemyHealthController enemy, Vector3 ground,
            float range, out Vector3 found)
        {
            found = default; var nav = pawn.NavMeshAgent; var shooter = pawn.GetComponent<AgentCombatShooter>();
            if (shooter == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
            Vector3 navigation = AgentCombatNavigationTarget.Resolve(enemy);
            foreach (float height in new[] { ground.y, navigation.y })
                for (int ring = 0; ring < 4; ring++) for (int angle = 0; angle < 16; angle++)
                {
                    Vector3 probe = new Vector3(enemy.transform.position.x, height, enemy.transform.position.z) +
                        Quaternion.AngleAxis(angle * 22.5f, Vector3.up) * Vector3.forward * (range * ring / 3f);
                    if (!NavMesh.SamplePosition(probe, out var hit, 1, filter) || AgentNavigationQuery.Check(nav, hit.position, 0).Failed) continue;
                    Vector3 position = pawn.Position + hit.position - ground;
                    Vector3 bodyAim = CombatAimPointResolver.Resolve(pawn.transform) + hit.position - ground;
                    Vector3 facing = enemy.transform.position - position; facing.y = 0;
                    Quaternion rotation = facing.sqrMagnitude > .0001f ? Quaternion.LookRotation(facing) : pawn.transform.rotation;
                    if (TargetVisibilityQuery.Check(pawn.transform, bodyAim, enemy.transform, range) != TargetVisibilityResult.Visible ||
                        !shooter.CanShootFrom(enemy, range, position, rotation)) continue;
                    found = hit.position; return true;
                }
            return false;
        }
    }
}
#endif
