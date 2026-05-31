using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent MVP 目标发现系统
    /// 按发现范围为每个已注册 Agent 选择一个最高优先级群目标，并写入现有命令接口
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

        private readonly HashSet<int> _searchedResourceInstanceIds =
            new HashSet<int>();

        /// <summary>
        /// 当前场景中的 Agent 目标发现系统实例
        /// </summary>
        public static AgentTargetDiscoveryController ActiveInstance => _activeInstance;

        /// <summary>
        /// 获取或创建 Agent 目标发现系统
        /// Agent 注册成功后会确保该系统存在
        /// </summary>
        /// <returns></returns>
        public static AgentTargetDiscoveryController GetOrCreate()
        {
            if (_activeInstance != null)
                return _activeInstance;

            _activeInstance = FindObjectOfType<AgentTargetDiscoveryController>();
            if (_activeInstance != null)
                return _activeInstance;

            GameObject controllerObject = new GameObject("[AgentTargetDiscoveryController]");
            _activeInstance = controllerObject.AddComponent<AgentTargetDiscoveryController>();
            return _activeInstance;
        }

        /// <summary>
        /// 标记一个资源对象已经被 Agent 搜索过
        /// 用于避免目标发现反复选择同一个已处理资源
        /// </summary>
        /// <param name="resourceObject"></param>
        public static void MarkResourceSearched(GameObject resourceObject)
        {
            if (_activeInstance == null || resourceObject == null)
                return;

            _activeInstance._searchedResourceInstanceIds.Add(resourceObject.GetInstanceID());
        }

        /// <summary>
        /// 判断资源对象是否已经被 Agent 搜索过
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public static bool IsResourceMarkedSearched(GameObject resourceObject)
        {
            return _activeInstance != null &&
                   resourceObject != null &&
                   _activeInstance._searchedResourceInstanceIds.Contains(resourceObject.GetInstanceID());
        }

        private AgentRuntimeRegistry Registry
        {
            get
            {
                if (_registry == null)
                    _registry = AgentRuntimeRegistry.GetOrCreate();

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
                _activeInstance = null;
        }

        private void Update()
        {
            AgentRuntimeRegistry registry = Registry;
            if (registry == null || registry.AgentCount <= 0)
                return;

            registry.CopyHandlesTo(_agentBuffer);
            double timeSeconds = Time.timeAsDouble;

            for (int i = 0; i < _agentBuffer.Count; i++)
            {
                AgentRuntimeHandle handle = _agentBuffer[i];
                if (!ShouldScanAgent(handle, timeSeconds))
                    continue;

                ScheduleNextScan(handle, timeSeconds);
                RefreshAgentTarget(handle);
            }
        }

        private bool ShouldScanAgent(AgentRuntimeHandle handle, double timeSeconds)
        {
            if (!handle.IsValid || handle.ReadOnly == null || handle.CommandReceiver == null)
                return false;

            AgentPawnRoot pawnRoot = handle.PawnRoot;
            if (pawnRoot == null || !pawnRoot.EnableTargetDiscovery || pawnRoot.IsDead)
                return false;

            double nextScanTime;
            return !_nextScanTimeByAgentId.TryGetValue(handle.AgentId, out nextScanTime) ||
                   timeSeconds >= nextScanTime;
        }

        private void ScheduleNextScan(AgentRuntimeHandle handle, double timeSeconds)
        {
            float interval = Mathf.Max(0.05f, handle.PawnRoot.TargetDiscoveryInterval);
            _nextScanTimeByAgentId[handle.AgentId] = timeSeconds + interval;
        }

        // 按固定优先级选择目标，保证战斗目标不会被资源或撤离点抢占
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

            // 优先级由判断顺序表达：活跃敌人群 > 敌人来源群 > 资源群 > 撤离点群
            if (TryFindNearestEnemyCluster(
                    targetRegistry,
                    agent.Position,
                    rangeSqr,
                    out ActiveEnemyClusterAuthoring enemyCluster))
            {
                ApplyEnemyClusterTarget(handle, commandReceiver, enemyCluster);
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
                TryFindNearestResourceCluster(targetRegistry, agent.Position, rangeSqr, out resourceCluster))
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

        /// <summary>
        /// 将敌人群写入 Agent 指令
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="commandReceiver"></param>
        /// <param name="enemyCluster"></param>
        private static void ApplyEnemyClusterTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            ActiveEnemyClusterAuthoring enemyCluster)
        {
            // 发现层只选择目标，具体攻击流程仍交给 Combat 行为树
            commandReceiver.SetVisibleEnemy(true);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);

            string targetId = enemyCluster.TargetId;
            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                AgentDirectiveType.Engage,
                AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Enemy,
                    enemyCluster.gameObject,
                    targetId),
                targetId,
                handle.AgentId));
        }

        /// <summary>
        /// 将敌人来源群写入 Agent 侦查指令
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="commandReceiver"></param>
        /// <param name="enemySourceCluster"></param>
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

        /// <summary>
        /// 将资源群写入 Agent 指令
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="commandReceiver"></param>
        /// <param name="resourceCluster"></param>
        private static void ApplyResourceClusterTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            ResourceClusterAuthoring resourceCluster)
        {
            // 资源点同时标记为可交互目标，兼容当前 SearchResource / InteractLoot 状态拆分
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(true);
            commandReceiver.SetHasInteractableTarget(true);
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

        /// <summary>
        /// 将撤离点群写入 Agent 指令
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="commandReceiver"></param>
        /// <param name="extractionCluster"></param>
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

        // 清空目标事实时也清除指令，避免状态机继续执行上一帧的目标
        private static void ClearTargetFacts(IAgentCommandReceiver commandReceiver)
        {
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.ClearDirective();
        }

        /// <summary>
        /// 在发现范围内寻找最近的可接战敌人群
        /// </summary>
        /// <param name="targetRegistry"></param>
        /// <param name="agentPosition"></param>
        /// <param name="rangeSqr"></param>
        /// <param name="nearestEnemyCluster"></param>
        /// <returns></returns>
        private bool TryFindNearestEnemyCluster(
            GameplayTargetRegistry targetRegistry,
            Vector3 agentPosition,
            float rangeSqr,
            out ActiveEnemyClusterAuthoring nearestEnemyCluster)
        {
            nearestEnemyCluster = null;
            float nearestDistanceSqr = float.MaxValue;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ActiveEnemyClusterAuthoring enemyCluster) ||
                    enemyCluster.HasBeenCompleted ||
                    !enemyCluster.TryGetNearestAliveEnemy(agentPosition, out _))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, enemyCluster.CenterPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestEnemyCluster = enemyCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemyCluster != null;
        }

        /// <summary>
        /// 在发现范围内寻找最近的未完成敌人来源群
        /// </summary>
        /// <param name="targetRegistry"></param>
        /// <param name="agentPosition"></param>
        /// <param name="rangeSqr"></param>
        /// <param name="nearestEnemySourceCluster"></param>
        /// <returns></returns>
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
                    continue;

                nearestEnemySourceCluster = enemySourceCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemySourceCluster != null;
        }

        /// <summary>
        /// 在发现范围内寻找最近的未完成资源群
        /// </summary>
        /// <param name="targetRegistry"></param>
        /// <param name="agentPosition"></param>
        /// <param name="rangeSqr"></param>
        /// <param name="nearestResourceCluster"></param>
        /// <returns></returns>
        private bool TryFindNearestResourceCluster(
            GameplayTargetRegistry targetRegistry,
            Vector3 agentPosition,
            float rangeSqr,
            out ResourceClusterAuthoring nearestResourceCluster)
        {
            nearestResourceCluster = null;
            float nearestDistanceSqr = float.MaxValue;
            targetRegistry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                if (!(_clusterBuffer[i] is ResourceClusterAuthoring resourceCluster) ||
                    resourceCluster.HasBeenCompleted ||
                    !resourceCluster.TryGetNearestIncompleteResource(agentPosition, out _))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, resourceCluster.CenterPosition);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestResourceCluster = resourceCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestResourceCluster != null;
        }

        /// <summary>
        /// 当前资源群仍可处理时，继续保持该目标
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="rangeSqr"></param>
        /// <param name="resourceCluster"></param>
        /// <returns></returns>
        private static bool TryKeepCurrentResourceClusterTarget(
            IAgentReadOnly agent,
            float rangeSqr,
            out ResourceClusterAuthoring resourceCluster)
        {
            resourceCluster = null;
            if (agent == null || agent.Blackboard == null)
                return false;

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
                !resourceCluster.TryGetNearestIncompleteResource(agent.Position, out _))
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

        /// <summary>
        /// 在发现范围内寻找最近的可用撤离点群
        /// </summary>
        /// <param name="targetRegistry"></param>
        /// <param name="agentPosition"></param>
        /// <param name="rangeSqr"></param>
        /// <param name="nearestExtractionCluster"></param>
        /// <returns></returns>
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
                    continue;

                nearestExtractionCluster = extractionCluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestExtractionCluster != null;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            // 目标发现只关心水平距离，避免地形高度差影响优先级
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

    }
}
