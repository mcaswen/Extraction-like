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

            _candidateCollector.CollectVisibleEnemies(agent,_clusterBuffer,range,_enemyCandidates);
            _candidateCollector.CollectWorldTargets(agent,_clusterBuffer,range,_worldCandidates);
            AgentTargetCandidate? enemy=null,resource=null,exit=null;
            foreach (var candidate in _enemyCandidates)
                if (candidate.CanExecute) { enemy=candidate; break; }
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest,out AgentDirectiveRequest current);
            foreach (var candidate in _worldCandidates)
            {
                if (candidate.Kind==AgentTargetKind.Resource && (!resource.HasValue || candidate.Cluster.gameObject==current.TargetObject)) resource=candidate;
                if (candidate.Kind==AgentTargetKind.Extraction && !exit.HasValue) exit=candidate;
            }
            if (enemy.HasValue && (!resource.HasValue || enemy.Value.DistanceSqr<=resource.Value.DistanceSqr))
            {
                ApplyEnemyClusterTarget(targetRegistry,handle,commandReceiver,(ActiveEnemyClusterAuthoring)enemy.Value.Cluster,enemy.Value.Enemy);
                return;
            }
            if (resource.HasValue)
            {
                ApplyResourceClusterTarget(handle,commandReceiver,(ResourceClusterAuthoring)resource.Value.Cluster);
                return;
            }
            if (exit.HasValue)
            {
                commandReceiver.SubmitDirective(new AgentDirectiveRequest(AgentDirectiveType.Extract,
                    AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction,exit.Value.Member,exit.Value.Cluster.TargetId),
                    exit.Value.Cluster.TargetId,handle.AgentId));
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
        private readonly List<AgentTargetCandidate> _worldCandidates = new List<AgentTargetCandidate>();

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

        private static bool HasActiveDecisionController(AgentPawnRoot pawnRoot)
        {
            AgentTargetDecisionController decisionController =
                pawnRoot != null ? pawnRoot.GetComponent<AgentTargetDecisionController>() : null;

            return decisionController != null && decisionController.IsDecisionModuleActive;
        }

    }
}
