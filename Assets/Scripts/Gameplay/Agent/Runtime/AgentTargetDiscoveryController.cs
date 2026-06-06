using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 保底目标发现系统。
    /// 当 AgentTargetDecisionController 未启用时，按固定优先级选择目标并投递给 Brain。
    /// </summary>
    public sealed class AgentTargetDiscoveryController : MonoBehaviour
    {
        private static AgentTargetDiscoveryController _activeInstance;

        [SerializeField] private AgentRuntimeRegistry _registry;

        private readonly List<AgentRuntimeHandle> _agentBuffer =
            new List<AgentRuntimeHandle>();

        private readonly List<GameplayTargetClusterAuthoringBase> _clusterBuffer =
            new List<GameplayTargetClusterAuthoringBase>();

        private readonly Dictionary<AgentId, double> _nextScanTimeByAgentId =
            new Dictionary<AgentId, double>();

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
            double timeSeconds = Time.timeAsDouble;

            for (int i = 0; i < _agentBuffer.Count; i++)
            {
                AgentRuntimeHandle handle = _agentBuffer[i];
                if (!ShouldScanAgent(handle, timeSeconds))
                {
                    continue;
                }

                ScheduleNextScan(handle, timeSeconds);
                RefreshAgentTarget(handle);
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

            return !_nextScanTimeByAgentId.TryGetValue(handle.AgentId, out double nextScanTime) ||
                   timeSeconds >= nextScanTime;
        }

        private void ScheduleNextScan(AgentRuntimeHandle handle, double timeSeconds)
        {
            float interval = Mathf.Max(0.05f, handle.PawnRoot.TargetDiscoveryInterval);
            _nextScanTimeByAgentId[handle.AgentId] = timeSeconds + interval;
        }

        // 旧 MVP 优先级：活跃敌人群 > 敌人来源群 > 资源群 > 撤离点群。
        private void RefreshAgentTarget(AgentRuntimeHandle handle)
        {
            IAgentReadOnly agent = handle.ReadOnly;
            IAgentCommandReceiver commandReceiver = handle.CommandReceiver;
            float range = Mathf.Max(0f, handle.PawnRoot.TargetDiscoveryRange);
            float rangeSqr = range * range;

            if (range <= 0f)
            {
                ClearTargetFacts(commandReceiver);
                return;
            }

            GameplayTargetRegistry targetRegistry = GameplayTargetRegistry.GetOrCreate();

            if (TryFindNearestEnemyCluster(
                    targetRegistry,
                    agent.Position,
                    rangeSqr,
                    out ActiveEnemyClusterAuthoring enemyCluster,
                    out global::EnemyHealthController enemy))
            {
                ApplyEnemyClusterTarget(targetRegistry, handle, commandReceiver, enemyCluster, enemy);
                return;
            }

            if (TryFindNearestEnemySourceCluster(
                    targetRegistry,
                    agent.Position,
                    rangeSqr,
                    out EnemySourceClusterAuthoring enemySourceCluster))
            {
                ApplyEnemySourceClusterTarget(handle, commandReceiver, enemySourceCluster);
                return;
            }

            if (TryKeepCurrentResourceClusterTarget(agent, rangeSqr, out ResourceClusterAuthoring resourceCluster) ||
                TryFindNearestResourceCluster(targetRegistry, agent, rangeSqr, out resourceCluster))
            {
                ApplyResourceClusterTarget(handle, commandReceiver, resourceCluster);
                return;
            }

            if (TryFindNearestExtractionCluster(
                    targetRegistry,
                    agent.Position,
                    rangeSqr,
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
            commandReceiver.SetVisibleEnemy(true);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);

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
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(true);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);

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
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(true);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);

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
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(true);

            string targetId = extractionCluster.TargetId;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.Extract,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Extraction,
                    extractionCluster.gameObject,
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

        private bool TryFindNearestEnemyCluster(
            GameplayTargetRegistry targetRegistry,
            Vector3 agentPosition,
            float rangeSqr,
            out ActiveEnemyClusterAuthoring nearestEnemyCluster,
            out global::EnemyHealthController nearestEnemy)
        {
            nearestEnemyCluster = null;
            nearestEnemy = null;
            float nearestDistanceSqr = float.MaxValue;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ActiveEnemyClusterAuthoring enemyCluster) ||
                    enemyCluster.HasBeenCompleted ||
                    !enemyCluster.TryGetNearestAliveEnemy(agentPosition, out global::EnemyHealthController enemy))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, enemyCluster.CenterPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestEnemyCluster = enemyCluster;
                nearestEnemy = enemy;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemyCluster != null;
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
            GameplayTargetRegistry targetRegistry,
            IAgentReadOnly agent,
            float rangeSqr,
            out ResourceClusterAuthoring nearestResourceCluster)
        {
            nearestResourceCluster = null;
            float nearestDistanceSqr = float.MaxValue;
            Vector3 agentPosition = agent.Position;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ResourceClusterAuthoring resourceCluster) ||
                    resourceCluster.HasBeenCompleted ||
                    !resourceCluster.TryGetNearestReachableIncompleteResource(agentPosition, agent.NavMeshAgent, out _))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, resourceCluster.CenterPosition);
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
            out ResourceClusterAuthoring resourceCluster)
        {
            resourceCluster = null;
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

            float distanceSqr = GetPlanarDistanceSqr(agent.Position, resourceCluster.CenterPosition);
            if (distanceSqr > rangeSqr)
            {
                resourceCluster = null;
                return false;
            }

            return true;
        }

        private bool TryFindNearestExtractionCluster(
            GameplayTargetRegistry targetRegistry,
            Vector3 agentPosition,
            float rangeSqr,
            out ExtractionClusterAuthoring nearestExtractionCluster)
        {
            nearestExtractionCluster = null;
            float nearestDistanceSqr = float.MaxValue;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ExtractionClusterAuthoring extractionCluster) ||
                    extractionCluster.HasBeenCompleted ||
                    !extractionCluster.TryGetNearestExtractionPoint(agentPosition, out _))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, extractionCluster.CenterPosition);
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
