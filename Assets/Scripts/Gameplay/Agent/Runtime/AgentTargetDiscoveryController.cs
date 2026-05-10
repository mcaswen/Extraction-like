using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent MVP 目标发现系统
    /// 按发现范围为每个已注册 Agent 选择一个最高优先级目标，并写入现有命令接口
    /// </summary>
    public sealed class AgentTargetDiscoveryController : MonoBehaviour
    {
        private static AgentTargetDiscoveryController _activeInstance;

        [SerializeField] private AgentRuntimeRegistry _registry;

        private readonly List<AgentRuntimeHandle> _agentBuffer =
            new List<AgentRuntimeHandle>();

        private readonly Dictionary<AgentId, double> _nextScanTimeByAgentId =
            new Dictionary<AgentId, double>();

        private readonly HashSet<int> _searchedResourceInstanceIds =
            new HashSet<int>();

        public static AgentTargetDiscoveryController ActiveInstance => _activeInstance;

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

        public static void MarkResourceSearched(GameObject resourceObject)
        {
            if (_activeInstance == null || resourceObject == null)
                return;

            _activeInstance._searchedResourceInstanceIds.Add(resourceObject.GetInstanceID());
        }

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

            // 优先级由判断顺序表达：敌人 > 资源点 > 撤离点
            if (TryFindNearestEnemy(agent.Position, rangeSqr, out global::EnemyHealthController enemy))
            {
                ApplyEnemyTarget(handle, commandReceiver, enemy);
                return;
            }

            if (TryKeepCurrentResourceTarget(agent, rangeSqr, out GameObject resourceObject) ||
                TryFindNearestResource(agent.Position, rangeSqr, out resourceObject))
            {
                ApplyResourceTarget(handle, commandReceiver, resourceObject);
                return;
            }

            if (TryFindNearestExtractionPoint(
                    agent.Position,
                    rangeSqr,
                    out global::ExtractionPointController extractionPoint))
            {
                ApplyExtractionTarget(handle, commandReceiver, extractionPoint);
                return;
            }

            ClearTargetFacts(commandReceiver);
        }

        private static void ApplyEnemyTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            global::EnemyHealthController enemy)
        {
            // 发现层只选择目标，具体攻击流程仍交给 Combat 行为树
            commandReceiver.SetVisibleEnemy(true);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.SubmitDirective(AgentDirectiveRequest.EngageConcreteEnemy(
                enemy.gameObject,
                BuildTargetId("Enemy", enemy.gameObject),
                handle.AgentId));
        }

        private static void ApplyResourceTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            GameObject resourceObject)
        {
            // 资源点同时标记为可交互目标，兼容当前 SearchResource / InteractLoot 状态拆分
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasResourceTarget(true);
            commandReceiver.SetHasInteractableTarget(true);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.SubmitDirective(AgentDirectiveRequest.SearchConcreteResource(
                resourceObject,
                BuildTargetId("Resource", resourceObject),
                handle.AgentId));
        }

        private static void ApplyExtractionTarget(
            AgentRuntimeHandle handle,
            IAgentCommandReceiver commandReceiver,
            global::ExtractionPointController extractionPoint)
        {
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(true);

            string targetId = BuildTargetId("Extraction", extractionPoint.gameObject);
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
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.ClearDirective();
        }

        private static bool TryFindNearestEnemy(
            Vector3 agentPosition,
            float rangeSqr,
            out global::EnemyHealthController nearestEnemy)
        {
            nearestEnemy = null;
            float nearestDistanceSqr = float.MaxValue;
            global::EnemyHealthController[] enemies = FindObjectsOfType<global::EnemyHealthController>(false);

            for (int i = 0; i < enemies.Length; i++)
            {
                global::EnemyHealthController enemy = enemies[i];
                if (enemy == null ||
                    !enemy.gameObject.activeInHierarchy ||
                    enemy.GetCurrentHealthRatio() <= 0f)
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, enemy.transform.position);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestEnemy = enemy;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestEnemy != null;
        }

        private static bool TryFindNearestResource(
            Vector3 agentPosition,
            float rangeSqr,
            out GameObject nearestResourceObject)
        {
            nearestResourceObject = null;
            float nearestDistanceSqr = float.MaxValue;

            FindNearestLootBoxResource(
                agentPosition,
                rangeSqr,
                ref nearestDistanceSqr,
                ref nearestResourceObject);

            FindNearestWorldLootResource(
                agentPosition,
                rangeSqr,
                ref nearestDistanceSqr,
                ref nearestResourceObject);

            return nearestResourceObject != null;
        }

        private static bool TryKeepCurrentResourceTarget(
            IAgentReadOnly agent,
            float rangeSqr,
            out GameObject resourceObject)
        {
            resourceObject = null;
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

            resourceObject = directiveRequest.TargetObject;
            if (!IsResourceObjectAvailable(resourceObject))
            {
                resourceObject = null;
                return false;
            }

            float distanceSqr = GetPlanarDistanceSqr(agent.Position, resourceObject.transform.position);
            if (distanceSqr > rangeSqr)
            {
                resourceObject = null;
                return false;
            }

            return true;
        }

        // 同一资源优先级下按水平距离挑最近目标，箱子和地面物品共享这套比较规则
        private static void FindNearestLootBoxResource(
            Vector3 agentPosition,
            float rangeSqr,
            ref float nearestDistanceSqr,
            ref GameObject nearestResourceObject)
        {
            global::LootBoxEntity[] lootBoxes = FindObjectsOfType<global::LootBoxEntity>(false);

            for (int i = 0; i < lootBoxes.Length; i++)
            {
                global::LootBoxEntity lootBox = lootBoxes[i];
                if (!IsLootBoxResourceAvailable(lootBox))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, lootBox.transform.position);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestResourceObject = lootBox.gameObject;
                nearestDistanceSqr = distanceSqr;
            }
        }

        // 地面掉落物也属于当前 MVP 的资源目标，但执行层仍由 SearchResource 统一接管
        private static void FindNearestWorldLootResource(
            Vector3 agentPosition,
            float rangeSqr,
            ref float nearestDistanceSqr,
            ref GameObject nearestResourceObject)
        {
            global::WorldLootItem[] worldItems = FindObjectsOfType<global::WorldLootItem>(false);

            for (int i = 0; i < worldItems.Length; i++)
            {
                global::WorldLootItem worldItem = worldItems[i];
                if (!IsWorldLootResourceAvailable(worldItem))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, worldItem.transform.position);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestResourceObject = worldItem.gameObject;
                nearestDistanceSqr = distanceSqr;
            }
        }

        private static bool IsLootBoxResourceAvailable(global::LootBoxEntity lootBox)
        {
            return lootBox != null &&
                   lootBox.gameObject.activeInHierarchy &&
                   !IsResourceMarkedSearched(lootBox.gameObject) &&
                   lootBox.GetSavedItems().Count > 0;
        }

        private static bool IsWorldLootResourceAvailable(global::WorldLootItem worldItem)
        {
            return worldItem != null &&
                   worldItem.gameObject.activeInHierarchy &&
                   !IsResourceMarkedSearched(worldItem.gameObject) &&
                   worldItem.ItemData != null &&
                   worldItem.CurrentAmount > 0;
        }

        private static bool IsResourceObjectAvailable(GameObject resourceObject)
        {
            if (resourceObject == null || !resourceObject.activeInHierarchy)
                return false;

            if (TryGetTargetComponent(resourceObject, out global::LootBoxEntity lootBox))
                return IsLootBoxResourceAvailable(lootBox);

            if (TryGetTargetComponent(resourceObject, out global::WorldLootItem worldItem))
                return IsWorldLootResourceAvailable(worldItem);

            return false;
        }

        private static bool TryGetTargetComponent<TComponent>(
            GameObject targetObject,
            out TComponent component)
            where TComponent : Component
        {
            component = targetObject.GetComponent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInParent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInChildren<TComponent>();
            return component != null;
        }

        private static bool TryFindNearestExtractionPoint(
            Vector3 agentPosition,
            float rangeSqr,
            out global::ExtractionPointController nearestExtractionPoint)
        {
            nearestExtractionPoint = null;
            float nearestDistanceSqr = float.MaxValue;
            global::ExtractionPointController[] extractionPoints =
                FindObjectsOfType<global::ExtractionPointController>(false);

            for (int i = 0; i < extractionPoints.Length; i++)
            {
                global::ExtractionPointController extractionPoint = extractionPoints[i];
                if (extractionPoint == null || !extractionPoint.gameObject.activeInHierarchy)
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, extractionPoint.transform.position);
                if (distanceSqr > rangeSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestExtractionPoint = extractionPoint;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestExtractionPoint != null;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            // 目标发现只关心水平距离，避免地形高度差影响优先级
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private static string BuildTargetId(string prefix, GameObject targetObject)
        {
            if (targetObject == null)
                return string.Empty;

            return $"{prefix}_{targetObject.name}_{targetObject.GetInstanceID()}";
        }
    }
}
