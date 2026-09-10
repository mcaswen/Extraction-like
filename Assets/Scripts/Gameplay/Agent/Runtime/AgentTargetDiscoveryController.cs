using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Targeting;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 保底目标发现系统。
    /// 当 AgentTargetDecisionController 未启用时，按旧路径临时规则选择目标并投递给 Brain。
    /// </summary>
    public sealed class AgentTargetDiscoveryController : MonoBehaviour
    {
        private static AgentTargetDiscoveryController _activeInstance;

        [SerializeField] private AgentRuntimeRegistry _registry;
        [SerializeField, Min(1)] private int _maxAgentScansPerFrame = 4;

        private readonly List<AgentRuntimeHandle> _agentBuffer =
            new List<AgentRuntimeHandle>();

        private readonly List<GameplayTargetClusterAuthoringBase> _clusterBuffer =
            new List<GameplayTargetClusterAuthoringBase>();

        private readonly Dictionary<AgentId, double> _nextScanTimeByAgentId =
            new Dictionary<AgentId, double>();

        private int _scanCursor;

        public static AgentTargetDiscoveryController ActiveInstance => _activeInstance;

        public static AgentTargetDiscoveryController GetOrCreate()
        {
            if (_activeInstance != null)
            {
                return _activeInstance;
            }

            _activeInstance = FindObjectOfType<AgentTargetDiscoveryController>();
            if (_activeInstance != null)
            {
                return _activeInstance;
            }

            GameObject controllerObject = new GameObject("[AgentTargetDiscoveryController]");
            _activeInstance = controllerObject.AddComponent<AgentTargetDiscoveryController>();
            return _activeInstance;
        }

        private AgentRuntimeRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = AgentRuntimeRegistry.GetOrCreate();
                }

                return _registry;
            }
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning("场景中存在多个 AgentTargetDiscoveryController，后创建的实例将被停用。", this);
                enabled = false;
                return;
            }

            _activeInstance = this;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        private void Update()
        {
            AgentRuntimeRegistry registry = Registry;
            if (registry == null || registry.AgentCount <= 0)
            {
                return;
            }

            registry.CopyHandlesTo(_agentBuffer);
            int agentCount = _agentBuffer.Count;
            if (agentCount <= 0)
            {
                return;
            }

            if (_scanCursor >= agentCount)
            {
                _scanCursor = 0;
            }

            double timeSeconds = Time.timeAsDouble;
            int maxScansThisFrame = Mathf.Max(1, _maxAgentScansPerFrame);
            int completedScans = 0;
            int examinedAgents = 0;

            while (examinedAgents < agentCount && completedScans < maxScansThisFrame)
            {
                AgentRuntimeHandle handle = _agentBuffer[_scanCursor];
                _scanCursor = (_scanCursor + 1) % agentCount;
                examinedAgents++;

                if (!ShouldScanAgent(handle, timeSeconds))
                {
                    continue;
                }

                ScheduleNextScan(handle, timeSeconds);
                RefreshAgentTarget(handle);
                completedScans++;
            }
        }

        private bool ShouldScanAgent(AgentRuntimeHandle handle, double timeSeconds)
        {
            if (!handle.IsValid || handle.ReadOnly == null || handle.CommandReceiver == null)
            {
                return false;
            }

            AgentPawnRoot pawnRoot = handle.PawnRoot;
            if (pawnRoot == null ||
                !pawnRoot.EnableTargetDiscovery ||
                pawnRoot.IsDead ||
                HasActiveDecisionController(pawnRoot))
            {
                return false;
            }

            if (AgentManualDirectiveLock.ShouldHoldManualDirective(handle.ReadOnly) ||
                AgentManualDirectiveLock.ShouldHoldCombatDamageDirective(handle.ReadOnly))
            {
                return false;
            }

            if (!_nextScanTimeByAgentId.TryGetValue(handle.AgentId, out double nextScanTime))
            {
                ScheduleInitialScan(handle, timeSeconds);
                return false;
            }

            return timeSeconds >= nextScanTime;
        }

        private void ScheduleInitialScan(AgentRuntimeHandle handle, double timeSeconds)
        {
            float interval = Mathf.Max(0.05f, handle.PawnRoot.TargetDiscoveryInterval);
            _nextScanTimeByAgentId[handle.AgentId] =
                timeSeconds + interval * GetStableScanOffset01(handle.AgentId);
        }

        private void ScheduleNextScan(AgentRuntimeHandle handle, double timeSeconds)
        {
            float interval = Mathf.Max(0.05f, handle.PawnRoot.TargetDiscoveryInterval);
            _nextScanTimeByAgentId[handle.AgentId] = timeSeconds + interval;
        }

        private static float GetStableScanOffset01(AgentId agentId)
        {
            unchecked
            {
                int hash = agentId.GetHashCode();
                uint positiveHash = (uint)hash;
                return (positiveHash % 997u) / 997f;
            }
        }

        // 临时旧路径规则：ActiveEnemy 和 Resource 里选最近；EnemySource 不自动选择；都没有时撤离。
        private void RefreshAgentTarget(AgentRuntimeHandle handle)
        {
            IAgentReadOnly agent = handle.ReadOnly;
            IAgentCommandReceiver commandReceiver = handle.CommandReceiver;
            float range = Mathf.Max(0f, handle.PawnRoot.TargetDiscoveryRange);
            float rangeSqr = range * range;

            if (AgentManualDirectiveLock.ShouldHoldManualDirective(agent) ||
                AgentManualDirectiveLock.ShouldHoldCombatDamageDirective(agent))
                return;

            if (range <= 0f)
            {
                ClearTargetFacts(commandReceiver);
                return;
            }

            GameplayTargetRegistry targetRegistry = GameplayTargetRegistry.GetOrCreate();
            targetRegistry.CopyClustersTo(_clusterBuffer);

            bool hasEnemyTarget = TryFindNearestEnemyCluster(
                    agent,
                    rangeSqr,
                    out ActiveEnemyClusterAuthoring enemyCluster,
                    out global::EnemyHealthController enemy,
                    out float enemyDistanceSqr);

            bool hasResourceTarget = TryKeepCurrentResourceClusterTarget(
                    agent,
                    rangeSqr,
                    out ResourceClusterAuthoring resourceCluster,
                    out float resourceDistanceSqr);
            if (!hasResourceTarget)
            {
                hasResourceTarget = TryFindNearestResourceCluster(
                    agent,
                    rangeSqr,
                    out resourceCluster,
                    out resourceDistanceSqr);
            }

            if (hasEnemyTarget &&
                (!hasResourceTarget || enemyDistanceSqr <= resourceDistanceSqr))
            {
                ApplyEnemyClusterTarget(targetRegistry, handle, commandReceiver, enemyCluster, enemy);
                return;
            }

            if (hasResourceTarget)
            {
                ApplyResourceClusterTarget(handle, commandReceiver, resourceCluster);
                return;
            }

            if (TryFindNearestExtractionCluster(
                    agent.Position,
                    float.PositiveInfinity,
                    out ExtractionClusterAuthoring extractionCluster))
            {
                ApplyExtractionClusterTarget(handle, commandReceiver, extractionCluster);
                return;
            }

            ClearTargetFacts(commandReceiver);
        }

        private static void ApplyEnemyClusterTarget(
            GameplayTargetRegistry targetRegistry,
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            ActiveEnemyClusterAuthoring enemyCluster,
            global::EnemyHealthController enemy)
        {
            string targetId = ResolveActiveEnemyTargetId(targetRegistry, enemyCluster, enemy);
            GameObject targetObject = enemy != null ? enemy.gameObject : enemyCluster.gameObject;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.Engage,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Enemy,
                    targetObject,
                    targetId),
                targetId,
                handle.AgentId));
        }

        private static void ApplyEnemySourceClusterTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            EnemySourceClusterAuthoring enemySourceCluster)
        {
            string targetId = enemySourceCluster.TargetId;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.MoveTo,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.EnemySource,
                    enemySourceCluster.gameObject,
                    targetId),
                targetId,
                handle.AgentId));
        }

        private static void ApplyResourceClusterTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            ResourceClusterAuthoring resourceCluster)
        {
            string targetId = resourceCluster.TargetId;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.Search,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Resource,
                    resourceCluster.gameObject,
                    targetId),
                targetId,
                handle.AgentId));
        }

        private static void ApplyExtractionClusterTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            ExtractionClusterAuthoring extractionCluster)
        {
            if (!extractionCluster.TryGetNearestExtractionPoint(
                    handle.ReadOnly.Position,
                    out global::ExtractionPointController extractionPoint) ||
                extractionPoint == null)
            {
                return;
            }

            string targetId = extractionCluster.TargetId;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.Extract,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Extraction,
                    extractionPoint.gameObject,
                    targetId),
                targetId,
                handle.AgentId));
        }

        private static void ClearTargetFacts(IAgentCommandReceiver commandReceiver)
        {
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.ClearDirective();
        }

        private readonly AgentTargetCandidateCollector _candidateCollector = new AgentTargetCandidateCollector();
        private readonly List<AgentTargetCandidate> _enemyCandidates = new List<AgentTargetCandidate>();
        private bool TryFindNearestEnemyCluster(IAgentReadOnly agent, float rangeSqr,
            out ActiveEnemyClusterAuthoring nearestEnemyCluster, out global::EnemyHealthController nearestEnemy, out float nearestDistanceSqr)
        {
            _candidateCollector.CollectVisibleEnemies(agent, _clusterBuffer, Mathf.Sqrt(rangeSqr), _enemyCandidates);
            nearestEnemyCluster = null; nearestEnemy = null; nearestDistanceSqr = float.PositiveInfinity;
            if (_enemyCandidates.Count == 0) return false;
            var candidate = _enemyCandidates[0];
            nearestEnemyCluster = (ActiveEnemyClusterAuthoring)candidate.Cluster;
            nearestEnemy = candidate.Enemy; nearestDistanceSqr = candidate.DistanceSqr;
            return true;
        }

        private static string ResolveActiveEnemyTargetId(
            GameplayTargetRegistry targetRegistry,
            ActiveEnemyClusterAuthoring enemyCluster,
            global::EnemyHealthController enemy)
        {
            if (targetRegistry != null &&
                targetRegistry.TryFindEnemySourceTargetIdByEnemy(enemy, out string sourceTargetId))
            {
                return sourceTargetId;
            }

            return enemyCluster != null ? enemyCluster.TargetId : string.Empty;
        }

        private bool TryFindNearestEnemySourceCluster(
            GameplayTargetRegistry targetRegistry,
            Vector3 agentPosition,
            float rangeSqr,
            out EnemySourceClusterAuthoring nearestEnemySourceCluster)
        {
            nearestEnemySourceCluster = null;
            float nearestDistanceSqr = float.MaxValue;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is EnemySourceClusterAuthoring enemySourceCluster) ||
                    enemySourceCluster.HasBeenCompleted ||
                    !enemySourceCluster.TryGetNearestSpawnPoint(agentPosition, out _))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, enemySourceCluster.CenterPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestEnemySourceCluster = enemySourceCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemySourceCluster != null;
        }

        private bool TryFindNearestResourceCluster(
            IAgentReadOnly agent,
            float rangeSqr,
            out ResourceClusterAuthoring nearestResourceCluster,
            out float nearestDistanceSqr)
        {
            nearestResourceCluster = null;
            nearestDistanceSqr = float.MaxValue;
            Vector3 agentPosition = agent.Position;

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ResourceClusterAuthoring resourceCluster) ||
                    resourceCluster.HasBeenCompleted)
                {
                    continue;
                }

                if (!resourceCluster.TryGetNearestIncompleteResource(
                        agentPosition,
                        out GameObject candidateResourceObject))
                {
                    continue;
                }

                Vector3 candidatePosition = candidateResourceObject != null
                    ? candidateResourceObject.transform.position
                    : resourceCluster.CenterPosition;
                float candidateDistanceSqr = GetPlanarDistanceSqr(agentPosition, candidatePosition);
                if (candidateDistanceSqr > rangeSqr || candidateDistanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                if (!resourceCluster.TryGetNearestReachableIncompleteResource(
                        agentPosition,
                        agent.NavMeshAgent,
                        out GameObject resourceObject))
                {
                    continue;
                }

                Vector3 targetPosition = resourceObject != null
                    ? resourceObject.transform.position
                    : resourceCluster.CenterPosition;
                float distanceSqr = GetPlanarDistanceSqr(agentPosition, targetPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestResourceCluster = resourceCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestResourceCluster != null;
        }

        private static bool TryKeepCurrentResourceClusterTarget(
            IAgentReadOnly agent,
            float rangeSqr,
            out ResourceClusterAuthoring resourceCluster,
            out float distanceSqr)
        {
            resourceCluster = null;
            distanceSqr = float.MaxValue;
            if (agent == null || agent.Blackboard == null)
            {
                return false;
            }

            if (!agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest))
            {
                return false;
            }

            if (directiveRequest.DirectiveType != AgentDirectiveType.Search ||
                directiveRequest.TargetRef.Kind != AgentTargetKind.Resource)
            {
                return false;
            }

            if (directiveRequest.TargetObject == null ||
                !directiveRequest.TargetObject.TryGetComponent(out resourceCluster) ||
                resourceCluster.HasBeenCompleted ||
                !resourceCluster.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _))
            {
                resourceCluster = null;
                return false;
            }

            distanceSqr = GetPlanarDistanceSqr(agent.Position, resourceCluster.CenterPosition);
            if (distanceSqr > rangeSqr)
            {
                resourceCluster = null;
                distanceSqr = float.MaxValue;
                return false;
            }

            return true;
        }

        private bool TryFindNearestExtractionCluster(
            Vector3 agentPosition,
            float rangeSqr,
            out ExtractionClusterAuthoring nearestExtractionCluster)
        {
            nearestExtractionCluster = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ExtractionClusterAuthoring extractionCluster) ||
                    extractionCluster.HasBeenCompleted ||
                    !extractionCluster.TryGetNearestExtractionPoint(
                        agentPosition,
                        out global::ExtractionPointController extractionPoint))
                {
                    continue;
                }

                Vector3 targetPosition = extractionPoint != null
                    ? extractionPoint.transform.position
                    : extractionCluster.CenterPosition;
                float distanceSqr = GetPlanarDistanceSqr(agentPosition, targetPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestExtractionCluster = extractionCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestExtractionCluster != null;
        }

        private static bool HasActiveDecisionController(AgentPawnRoot pawnRoot)
        {
            AgentTargetDecisionController decisionController =
                pawnRoot != null ? pawnRoot.GetComponent<AgentTargetDecisionController>() : null;

            return decisionController != null && decisionController.IsDecisionModuleActive;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}
