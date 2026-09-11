using System.Collections.Generic;
using System.Text;
using Gameplay.Targets.Data;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 资源群目标配置
    /// 通过手动拖拽资源实体，把多个箱子或地面掉落抽象成一个群目标
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceClusterAuthoring : GameplayTargetClusterAuthoringBase
    {
        [Header("Resource Tier")]
        [SerializeField] private global::SceneResourceTier _resourceTier = global::SceneResourceTier.Low;

        [Header("Resource Members")]
        [SerializeField]
        private List<GameplayTargetEntityMember> _resourceMembers =
            new List<GameplayTargetEntityMember>();

        [Header("Navigation Debug")]
        [SerializeField] private bool _showNavigationCandidateDebugObjects = true;

        /// <summary>
        /// 当前群目标在目标系统中的类型
        /// </summary>
        public override GameplayTargetKind TargetKind => GameplayTargetKind.Resource;
        protected override string IdPrefix => "ResourceCluster";
        private const string NavigationCandidateDebugRootName = "__ResourceNavigationCandidates";
        private const float NavMeshResourceSampleRadius = 4f;
        private const float ResourceApproachPadding = 2f;
        private const int ResourceApproachDirectionCount = 16;

        private readonly List<Vector3> _navigationCandidateBuffer = new List<Vector3>();
        private readonly List<Collider> _colliderBuffer = new List<Collider>();
        private readonly Dictionary<GameObject, List<Vector3>> _navigationCandidatesByResource =
            new Dictionary<GameObject, List<Vector3>>();
        private readonly HashSet<GameObject> _initializedNavigationDebugObjects = new HashSet<GameObject>();
        private NavMeshPath _navigationPath;
        public long NavigationPathCalculationCount { get; private set; }

        /// <summary>
        /// 当前资源群统一使用的资源等级
        /// </summary>
        public global::SceneResourceTier ResourceTier => _resourceTier;

        /// <summary>
        /// 当前资源群内配置的具体资源成员
        /// </summary>
        public IReadOnlyList<GameplayTargetEntityMember> ResourceMembers => _resourceMembers;

        /// <summary>
        /// 向调试日志追加资源群内每个成员的可搜索状态和 NavMesh 可达性快照
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="agentPosition"></param>
        /// <param name="navMeshAgent"></param>
        public void AppendNavigationDebugSnapshot(
            StringBuilder builder,
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent)
        {
            if (builder == null)
                return;

            RefreshRuntimeState();
            builder.AppendLine(
                $"resourceClusterDebug: id={TargetId} completed={HasBeenCompleted} members={_resourceMembers.Count}");

            if (!TryResolveNavMeshStartPosition(agentPosition, navMeshAgent, out Vector3 startPosition, out int areaMask))
            {
                builder.AppendLine(
                    "resourceClusterDebugStart: failed " +
                    $"agentPosition={FormatDebugVector(agentPosition)} navAgent={(navMeshAgent != null)}");
                return;
            }

            builder.AppendLine(
                "resourceClusterDebugStart: " +
                $"start={FormatDebugVector(startPosition)} agentPosition={FormatDebugVector(agentPosition)} " +
                $"areaMask={areaMask}");

            NavMeshPath path = new NavMeshPath();
            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                AppendMemberNavigationDebugSnapshot(
                    builder,
                    i,
                    member,
                    agentPosition,
                    navMeshAgent,
                    startPosition,
                    areaMask,
                    path);
            }
        }

        /// <summary>
        /// 获取资源群配置的实际资源等级
        /// 群内资源箱不再单独决定等级
        /// </summary>
        /// <param name="resourceTier"></param>
        /// <returns></returns>
        public bool TryResolveResourceTier(out global::SceneResourceTier resourceTier)
        {
            resourceTier = _resourceTier;
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _resourceMembers ??= new List<GameplayTargetEntityMember>();
            ApplyResourceTierToMembers();
        }

        protected override void OnEnable()
        {
            _resourceMembers ??= new List<GameplayTargetEntityMember>();
            ApplyResourceTierToMembers();
            base.OnEnable();
            InitializeNavigationCandidateDebugMarkers();
        }

        protected override void OnDisable()
        {
            _navigationCandidatesByResource.Clear();
            _initializedNavigationDebugObjects.Clear();
            _navigationPath = null;
            base.OnDisable();
        }

        private NavMeshPath GetNavigationPath()
        {
            if (_navigationPath == null)
                _navigationPath = new NavMeshPath();

            return _navigationPath;
        }

        /// <summary>
        /// 判断资源对象是否属于当前资源群
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public bool ContainsResource(GameObject resourceObject)
        {
            if (resourceObject == null)
                return false;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member != null && member.Matches(resourceObject))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取离 Agent 最近的未完成资源对象
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public bool TryGetNearestIncompleteResource(
            Vector3 agentPosition,
            out GameObject resourceObject)
        {
            RefreshRuntimeState();
            resourceObject = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (!IsMemberAvailableForSearch(member))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, member.Position);
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                resourceObject = member.EntityObject;
                nearestDistanceSqr = distanceSqr;
            }

            return resourceObject != null;
        }

        /// <summary>
        /// 获取离 Agent 最近且 NavMesh 完整可达的未完成资源对象
        /// 避免 Agent 反复追踪只有 partial path 的箱子而卡在原地
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="navMeshAgent"></param>
        /// <param name="resourceObject"></param>
        /// <returns></returns>
        public bool TryGetNearestReachableIncompleteResource(
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            out GameObject resourceObject)
        {
            return TryGetNearestReachableIncompleteResource(
                agentPosition,
                navMeshAgent,
                out resourceObject,
                out _);
        }

        /// <summary>
        /// 获取离 Agent 最近且 NavMesh 完整可达的未完成资源对象和实际可站立导航点。
        /// 资源 pivot 或箱体中心可能不在可站立面上，斜坡/平台场景需要围绕碰撞体找入口点。
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="navMeshAgent"></param>
        /// <param name="resourceObject"></param>
        /// <param name="navigationPosition"></param>
        /// <returns></returns>
        public bool TryGetNearestReachableIncompleteResource(
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            out GameObject resourceObject,
            out Vector3 navigationPosition)
        {
            return TryGetNearestReachableIncompleteResource(agentPosition, navMeshAgent, float.PositiveInfinity,
                out resourceObject, out navigationPosition);
        }

        /// <summary>发现阶段先按具体成员范围预筛；执行阶段仍可调用无范围重载。</summary>
        public bool TryGetNearestReachableIncompleteResource(
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            float maximumMemberDistance,
            out GameObject resourceObject,
            out Vector3 navigationPosition)
        {
            RefreshRuntimeState();
            resourceObject = null;
            navigationPosition = default;

            if (!TryResolveNavMeshStartPosition(agentPosition, navMeshAgent, out Vector3 startPosition, out int areaMask))
            {
                if (!float.IsPositiveInfinity(maximumMemberDistance)) return false;
                if (!TryGetNearestIncompleteResource(agentPosition, out resourceObject))
                    return false;

                navigationPosition = resourceObject != null ? resourceObject.transform.position : default;
                return resourceObject != null;
            }

            NavMeshPath path = GetNavigationPath();
            float nearestPathLength = float.MaxValue;
            float nearestDistanceSqr = float.MaxValue;
            float maximumDistanceSqr = Mathf.Max(0f, maximumMemberDistance) * Mathf.Max(0f, maximumMemberDistance);

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member == null || (member.Position - agentPosition).sqrMagnitude > maximumDistanceSqr ||
                    !IsMemberAvailableForSearch(member))
                    continue;

                if (!TryFindReachableResourceNavigationPosition(
                        member.EntityObject,
                        member.Position,
                        agentPosition,
                        startPosition,
                        areaMask,
                        path,
                        out Vector3 candidateNavigationPosition,
                        out float pathLength))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, member.Position);
                if (pathLength > nearestPathLength ||
                    (Mathf.Approximately(pathLength, nearestPathLength) && distanceSqr >= nearestDistanceSqr))
                {
                    continue;
                }

                resourceObject = member.EntityObject;
                navigationPosition = candidateNavigationPosition;
                nearestPathLength = pathLength;
                nearestDistanceSqr = distanceSqr;
            }

            return resourceObject != null;
        }

        /// <summary>
        /// 标记群内某个资源已经被搜索接触
        /// </summary>
        /// <param name="resourceObject"></param>
        public void MarkResourceTouched(GameObject resourceObject)
        {
            GameplayTargetEntityMember member = FindMember(resourceObject);
            if (member == null)
                return;

            member.MarkTouched();
            MarkTouched();
            RefreshRuntimeState();
        }

        /// <summary>
        /// 标记群内某个资源已经完成
        /// </summary>
        /// <param name="resourceObject"></param>
        public void MarkResourceCompleted(GameObject resourceObject)
        {
            GameplayTargetEntityMember member = FindMember(resourceObject);
            if (member == null)
                return;

            member.MarkTouched();
            member.MarkCompleted();
            RefreshRuntimeState();
        }

        protected override void CollectMemberPositions(List<Vector3> memberPositions)
        {
            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member?.EntityObject != null)
                    memberPositions.Add(member.EntityObject.transform.position);
            }
        }

        protected override void RefreshRuntimeState()
        {
            EnsureMemberIds();

            bool hasAnyMember = false;
            bool hasTouchedMember = false;
            bool hasIncompleteMember = false;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member == null)
                    continue;

                hasAnyMember = true;
                RefreshMemberCompletionFromResource(member);
                hasTouchedMember |= member.HasBeenTouched;
                hasIncompleteMember |= !member.HasBeenCompleted;
            }

            SetAggregatedState(hasTouchedMember, hasAnyMember && !hasIncompleteMember);
        }

        [ContextMenu("Generate Missing Member Ids")]
        private void GenerateMissingMemberIds()
        {
            EnsureMemberIds();
        }

        // 补齐成员 ID，保证资源实体状态不会依赖对象名
        private void EnsureMemberIds()
        {
            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                _resourceMembers[i]?.EnsureEntityId(TargetId, i);
            }
        }

        // 群等级是资源点的唯一配置来源，运行时把它同步到箱子 fallback 字段
        private void ApplyResourceTierToMembers()
        {
            if (_resourceMembers == null)
                return;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member != null && member.TryGetComponent(out global::LootBoxEntity lootBox))
                    lootBox.ApplyResourceClusterTier(_resourceTier);
            }
        }

        // 根据对象层级关系查找对应成员，兼容碰撞体挂在子物体上的情况
        private GameplayTargetEntityMember FindMember(GameObject resourceObject)
        {
            if (resourceObject == null)
                return null;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (member != null && member.Matches(resourceObject))
                    return member;
            }

            return null;
        }

        // 已完成或已失效资源不再作为 Agent 搜索候选
        private static bool IsMemberAvailableForSearch(GameplayTargetEntityMember member)
        {
            if (member == null || member.HasBeenCompleted || member.EntityObject == null)
                return false;

            if (member.TryGetComponent(out global::LootBoxEntity lootBox))
            {
                if (lootBox.IsBoardGameResourcePoint)
                    return lootBox.CanBeSearchedAsResourcePoint();

                return lootBox.gameObject.activeInHierarchy && lootBox.GetSavedItems().Count > 0;
            }

            if (member.TryGetComponent(out global::WorldLootItem worldItem))
            {
                return worldItem.gameObject.activeInHierarchy &&
                       worldItem.ItemData != null &&
                       worldItem.CurrentAmount > 0;
            }

            return false;
        }

        private void AppendMemberNavigationDebugSnapshot(
            StringBuilder builder,
            int memberIndex,
            GameplayTargetEntityMember member,
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            Vector3 startPosition,
            int areaMask,
            NavMeshPath path)
        {
            if (member == null)
            {
                builder.AppendLine($"resourceMember[{memberIndex}]: null");
                return;
            }

            // 先记录资源状态，区分资源本身不可搜和后续 NavMesh 不可达
            bool available = IsMemberAvailableForSearch(member);
            builder.Append(
                $"resourceMember[{memberIndex}]: " +
                $"id={member.EntityId} object={FormatDebugObject(member.EntityObject)} " +
                $"touched={member.HasBeenTouched} completed={member.HasBeenCompleted} " +
                $"available={available} pos={FormatDebugVector(member.Position)}");
            AppendResourceComponentDebug(builder, member);
            builder.AppendLine();

            if (!available || member.EntityObject == null)
                return;

            // 围绕该资源实体生成候选停靠点，逐个检查采样和路径结果
            FillResourceNavigationCandidates(
                member.EntityObject,
                member.Position,
                agentPosition,
                _navigationCandidateBuffer);
            EnsureNavigationCandidateDebugMarkers(member.EntityObject, _navigationCandidateBuffer);

            int sampledCount = 0;
            int completeCount = 0;
            int partialCount = 0;
            int invalidCount = 0;
            int calculateFailedCount = 0;
            float bestCompleteLength = float.MaxValue;
            float bestPartialDistanceToTarget = float.MaxValue;
            Vector3 bestCompletePosition = default;
            Vector3 bestPartialEndPosition = default;
            Vector3 bestPartialTargetPosition = default;

            for (int candidateIndex = 0; candidateIndex < _navigationCandidateBuffer.Count; candidateIndex++)
            {
                Vector3 candidatePosition = _navigationCandidateBuffer[candidateIndex];
                // 候选点需要先吸附到当前 Agent 可用的 NavMesh 区域
                if (!NavMesh.SamplePosition(
                        candidatePosition,
                        out NavMeshHit targetHit,
                        NavMeshResourceSampleRadius,
                        areaMask))
                {
                    continue;
                }

                sampledCount++;
                bool calculated =
                    navMeshAgent != null &&
                    navMeshAgent.enabled &&
                    navMeshAgent.isOnNavMesh
                        ? navMeshAgent.CalculatePath(targetHit.position, path)
                        : NavMesh.CalculatePath(
                            startPosition,
                            targetHit.position,
                            areaMask,
                            path);
                if (!calculated)
                    calculateFailedCount++;

                // 完整路径用于实际选择资源目标，记录最短完整路径作为对照
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    completeCount++;
                    float pathLength = CalculatePathLength(path);
                    if (pathLength < bestCompleteLength)
                    {
                        bestCompleteLength = pathLength;
                        bestCompletePosition = targetHit.position;
                    }

                    continue;
                }

                // Partial 路径不直接用于搜索，但记录离目标最近的终点用于判断 NavMesh 断点
                if (path.status == NavMeshPathStatus.PathPartial)
                {
                    partialCount++;
                    Vector3 partialEndPosition = GetPathEndPosition(path, startPosition);
                    float distanceToTarget = Vector3.Distance(partialEndPosition, targetHit.position);
                    if (distanceToTarget < bestPartialDistanceToTarget)
                    {
                        bestPartialDistanceToTarget = distanceToTarget;
                        bestPartialEndPosition = partialEndPosition;
                        bestPartialTargetPosition = targetHit.position;
                    }

                    continue;
                }

                invalidCount++;
            }

            builder.Append(
                $"resourceMemberCandidates[{memberIndex}]: " +
                $"count={_navigationCandidateBuffer.Count} sampled={sampledCount} " +
                $"complete={completeCount} partial={partialCount} " +
                $"invalid={invalidCount} calculateFailed={calculateFailedCount}");
            if (completeCount > 0)
            {
                builder.Append(
                    $" bestComplete={FormatDebugVector(bestCompletePosition)} " +
                    $"bestCompleteLength={bestCompleteLength:0.###}");
            }

            if (partialCount > 0)
            {
                builder.Append(
                    $" bestPartialEnd={FormatDebugVector(bestPartialEndPosition)} " +
                    $"bestPartialTarget={FormatDebugVector(bestPartialTargetPosition)} " +
                    $"bestPartialEndToTarget={bestPartialDistanceToTarget:0.###}");
            }

            builder.AppendLine();
        }

        private static void AppendResourceComponentDebug(
            StringBuilder builder,
            GameplayTargetEntityMember member)
        {
            if (member.TryGetComponent(out global::LootBoxEntity lootBox))
            {
                builder.Append(
                    $" lootBox(active={lootBox.gameObject.activeInHierarchy}, " +
                    $"board={lootBox.IsBoardGameResourcePoint}, " +
                    $"state={lootBox.ResourceState}, " +
                    $"items={lootBox.GetSavedItems().Count}, " +
                    $"searchable={lootBox.CanBeSearchedAsResourcePoint()})");
                return;
            }

            if (member.TryGetComponent(out global::WorldLootItem worldItem))
            {
                builder.Append(
                    $" worldItem(active={worldItem.gameObject.activeInHierarchy}, " +
                    $"item={(worldItem.ItemData != null)}, amount={worldItem.CurrentAmount})");
            }
        }

        private static Vector3 GetPathEndPosition(NavMeshPath path, Vector3 fallbackPosition)
        {
            Vector3[] corners = path != null ? path.corners : null;
            if (corners == null || corners.Length <= 0)
                return fallbackPosition;

            return corners[corners.Length - 1];
        }

        private static bool TryResolveNavMeshStartPosition(
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            out Vector3 startPosition,
            out int areaMask)
        {
            startPosition = default;
            areaMask = NavMesh.AllAreas;

            if (navMeshAgent == null || !navMeshAgent.enabled)
                return false;

            areaMask = navMeshAgent.areaMask;
            if (navMeshAgent.isOnNavMesh)
            {
                startPosition = navMeshAgent.nextPosition;
                return true;
            }

            if (NavMesh.SamplePosition(
                    agentPosition,
                    out NavMeshHit startHit,
                    NavMeshResourceSampleRadius,
                    areaMask))
            {
                startPosition = startHit.position;
                return true;
            }

            return false;
        }

        private bool TryFindReachableResourceNavigationPosition(
            GameObject resourceObject,
            Vector3 fallbackPosition,
            Vector3 agentPosition,
            Vector3 startPosition,
            int areaMask,
            NavMeshPath path,
            out Vector3 navigationPosition,
            out float pathLength)
        {
            navigationPosition = default;
            pathLength = float.MaxValue;

            // 资源对象的 pivot 通常不在可站立面上，先围绕碰撞体生成一组可尝试停靠点
            List<Vector3> navigationCandidates = GetOrBuildResourceNavigationCandidates(
                resourceObject,
                fallbackPosition,
                agentPosition);
            EnsureNavigationCandidateDebugMarkers(resourceObject, navigationCandidates);

            for (int i = 0; i < navigationCandidates.Count; i++)
            {
                // 只接受 PathComplete，避免 Agent 追向无法最终到达的 partial 终点
                if (!TryCalculateCompletePathToCandidate(
                        navigationCandidates[i],
                        startPosition,
                        areaMask,
                        path,
                        out Vector3 candidateNavigationPosition,
                        out float candidatePathLength))
                {
                    continue;
                }

                // Candidate points are ordered from closest/simple to fallback, so the first complete path is enough for discovery.
                navigationPosition = candidateNavigationPosition;
                pathLength = candidatePathLength;
                return true;
            }

            return false;
        }

        private void FillResourceNavigationCandidates(
            GameObject resourceObject,
            Vector3 fallbackPosition,
            Vector3 agentPosition,
            List<Vector3> candidates)
        {
            candidates.Clear();
            if (resourceObject == null)
            {
                AddUniqueCandidate(candidates, fallbackPosition);
                return;
            }

            // 先收集所有有效实体碰撞体，并用离 Agent 最近的碰撞体点作为候选
            _colliderBuffer.Clear();
            resourceObject.GetComponentsInChildren<Collider>(false, _colliderBuffer);
            Bounds combinedBounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _colliderBuffer.Count; i++)
            {
                Collider resourceCollider = _colliderBuffer[i];
                if (resourceCollider == null ||
                    !resourceCollider.enabled ||
                    !resourceCollider.gameObject.activeInHierarchy ||
                    resourceCollider.isTrigger)
                {
                    continue;
                }

                AddUniqueCandidate(
                    candidates,
                    WithY(resourceCollider.ClosestPoint(agentPosition), resourceCollider.bounds.min.y));
                if (!hasBounds)
                {
                    combinedBounds = resourceCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(resourceCollider.bounds);
                }
            }

            // 再补充成员配置点、包围盒中心，以及包围盒外圈采样点
            Vector3 center = hasBounds ? combinedBounds.center : resourceObject.transform.position;
            float candidateBaseY = hasBounds ? combinedBounds.min.y : fallbackPosition.y;
            AddUniqueCandidate(candidates, WithY(fallbackPosition, candidateBaseY));
            AddUniqueCandidate(candidates, WithY(center, candidateBaseY));

            float approachRadius = hasBounds
                ? Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z) + ResourceApproachPadding
                : ResourceApproachPadding;
            approachRadius = Mathf.Max(ResourceApproachPadding, approachRadius);

            for (int i = 0; i < ResourceApproachDirectionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / ResourceApproachDirectionCount;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * approachRadius,
                    0f,
                    Mathf.Sin(angle) * approachRadius);
                AddUniqueCandidate(candidates, WithY(center + offset, candidateBaseY));
            }
        }

        private List<Vector3> GetOrBuildResourceNavigationCandidates(
            GameObject resourceObject,
            Vector3 fallbackPosition,
            Vector3 agentPosition)
        {
            if (resourceObject == null)
            {
                _navigationCandidateBuffer.Clear();
                AddUniqueCandidate(_navigationCandidateBuffer, fallbackPosition);
                return _navigationCandidateBuffer;
            }

            if (!_navigationCandidatesByResource.TryGetValue(
                    resourceObject,
                    out List<Vector3> navigationCandidates) ||
                navigationCandidates == null)
            {
                navigationCandidates = new List<Vector3>();
                _navigationCandidatesByResource[resourceObject] = navigationCandidates;
            }

            // Reuse storage, not stale coordinates: ClosestPoint depends on this agent,
            // and members/colliders can move after the initial debug-marker build.
            FillResourceNavigationCandidates(resourceObject, fallbackPosition, agentPosition, navigationCandidates);

            return navigationCandidates;
        }

        private void InitializeNavigationCandidateDebugMarkers()
        {
            _initializedNavigationDebugObjects.Clear();
            if (!_showNavigationCandidateDebugObjects)
            {
                SetNavigationCandidateDebugObjectsActive(false);
            }

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                GameObject resourceObject = member?.EntityObject;
                if (resourceObject == null)
                    continue;

                List<Vector3> navigationCandidates = GetOrBuildResourceNavigationCandidates(
                    resourceObject,
                    member.Position,
                    CenterPosition);

                if (_showNavigationCandidateDebugObjects)
                    EnsureNavigationCandidateDebugMarkers(resourceObject, navigationCandidates);
            }
        }

        private void EnsureNavigationCandidateDebugMarkers(
            GameObject resourceObject,
            List<Vector3> candidates)
        {
            if (!_showNavigationCandidateDebugObjects ||
                resourceObject == null ||
                _initializedNavigationDebugObjects.Contains(resourceObject))
            {
                return;
            }

            Transform root = EnsureNavigationCandidateDebugRoot(resourceObject);
            if (root == null)
                return;

            root.gameObject.SetActive(true);
            _initializedNavigationDebugObjects.Add(resourceObject);

            int candidateCount = candidates != null ? candidates.Count : 0;
            for (int i = 0; i < candidateCount; i++)
            {
                GameObject marker = GetOrCreateNavigationCandidateDebugObject(root, i);
                marker.transform.position = candidates[i];
                marker.transform.localRotation = Quaternion.identity;
                marker.transform.localScale = Vector3.one;
                marker.SetActive(true);
            }

            for (int i = candidateCount; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null)
                {
                    StripNavigationCandidateDebugComponents(child.gameObject);
                    child.gameObject.SetActive(false);
                }
            }
        }

        private Transform EnsureNavigationCandidateDebugRoot(GameObject resourceObject)
        {
            if (resourceObject == null)
                return null;

            Transform existingRoot = resourceObject.transform.Find(NavigationCandidateDebugRootName);
            if (existingRoot != null)
            {
                return existingRoot;
            }

            GameObject rootObject = new GameObject(NavigationCandidateDebugRootName);
            rootObject.transform.SetParent(resourceObject.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private GameObject GetOrCreateNavigationCandidateDebugObject(Transform root, int index)
        {
            string markerName = $"Candidate_{index:00}";
            if (root != null && index < root.childCount)
            {
                GameObject existingMarker = root.GetChild(index).gameObject;
                if (existingMarker.name != markerName)
                    existingMarker.name = markerName;

                StripNavigationCandidateDebugComponents(existingMarker);
                return existingMarker;
            }

            GameObject marker = new GameObject(markerName);
            marker.transform.SetParent(root, false);
            return marker;
        }

        private void StripNavigationCandidateDebugComponents(GameObject marker)
        {
            if (marker == null)
                return;

            RemoveDebugComponent<MeshFilter>(marker);
            RemoveDebugComponent<MeshRenderer>(marker);
            RemoveDebugComponent<Collider>(marker);
        }

        private static void RemoveDebugComponent<T>(GameObject marker) where T : Component
        {
            T[] components = marker.GetComponents<T>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                if (components[i] == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(components[i]);
                else
                    UnityEngine.Object.DestroyImmediate(components[i]);
            }
        }

        private void SetNavigationCandidateDebugObjectsActive(bool active)
        {
            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameObject resourceObject = _resourceMembers[i]?.EntityObject;
                Transform root = resourceObject != null
                    ? resourceObject.transform.Find(NavigationCandidateDebugRootName)
                    : null;
                if (root != null)
                {
                    for (int childIndex = 0; childIndex < root.childCount; childIndex++)
                    {
                        Transform child = root.GetChild(childIndex);
                        if (child != null)
                            StripNavigationCandidateDebugComponents(child.gameObject);
                    }

                    root.gameObject.SetActive(active);
                }
            }
        }

        private static Vector3 WithY(Vector3 value, float y)
        {
            value.y = y;
            return value;
        }

        private static void AddUniqueCandidate(List<Vector3> candidates, Vector3 candidate)
        {
            const float DuplicateCandidateDistanceSqr = 0.04f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if ((candidates[i] - candidate).sqrMagnitude <= DuplicateCandidateDistanceSqr)
                    return;
            }

            candidates.Add(candidate);
        }

        private bool TryCalculateCompletePathToCandidate(
            Vector3 candidatePosition,
            Vector3 startPosition,
            int areaMask,
            NavMeshPath path,
            out Vector3 navigationPosition,
            out float pathLength)
        {
            navigationPosition = default;
            pathLength = float.MaxValue;

            // 候选点只描述资源附近的空间位置，实际目标必须投到 NavMesh 上
            if (!NavMesh.SamplePosition(
                    candidatePosition,
                    out NavMeshHit targetHit,
                    NavMeshResourceSampleRadius,
                    areaMask))
            {
                return false;
            }

            // Use the resolved NavMesh start position to avoid per-agent path object allocations in the scan loop.
            NavigationPathCalculationCount++;
            bool calculated = NavMesh.CalculatePath(
                startPosition,
                targetHit.position,
                areaMask,
                path);
            if (!calculated || path.status != NavMeshPathStatus.PathComplete)
                return false;

            // 返回可实际 SetPath 的 NavMesh 点和完整路径长度，供外层做最近目标选择
            navigationPosition = targetHit.position;
            pathLength = GetPlanarDistanceSqr(startPosition, targetHit.position);
            return true;
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            Vector3[] corners = path != null ? path.corners : null;
            if (corners == null || corners.Length < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        // 根据资源对象当前状态回填成员完成状态
        private static void RefreshMemberCompletionFromResource(GameplayTargetEntityMember member)
        {
            if (member == null || member.HasBeenCompleted)
                return;

            // 运行时丢失的资源对象视为已处理，避免群目标永远无法完成
            if (member.EntityObject == null)
            {
                if (Application.isPlaying)
                    member.MarkCompleted();

                return;
            }

            if (member.TryGetComponent(out global::LootBoxEntity lootBox))
            {
                // 箱子被禁用、桌游资源点已搜刮、或普通箱子为空时都不再作为搜索候选
                if (!lootBox.gameObject.activeInHierarchy)
                {
                    member.MarkCompleted();
                    return;
                }

                if (lootBox.IsBoardGameResourcePoint)
                {
                    lootBox.RefreshResourcePointState();
                    if (lootBox.IsResourcePointLooted)
                        member.MarkCompleted();

                    return;
                }

                if (lootBox.GetSavedItems().Count <= 0)
                    member.MarkCompleted();

                return;
            }

            if (member.TryGetComponent(out global::WorldLootItem worldItem))
            {
                // 地面掉落失效或数量归零后，资源成员同步完成
                if (!worldItem.gameObject.activeInHierarchy ||
                    worldItem.ItemData == null ||
                    worldItem.CurrentAmount <= 0)
                {
                    member.MarkCompleted();
                }
            }
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private static string FormatDebugObject(Object targetObject)
        {
            return targetObject != null ? targetObject.name : "null";
        }

        private static string FormatDebugVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
