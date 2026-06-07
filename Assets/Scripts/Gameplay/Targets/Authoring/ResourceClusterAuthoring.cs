using System.Collections.Generic;
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
        [SerializeField] private List<GameplayTargetEntityMember> _resourceMembers =
            new List<GameplayTargetEntityMember>();

        public override GameplayTargetKind TargetKind => GameplayTargetKind.Resource;
        protected override string IdPrefix => "ResourceCluster";
        private const float NavMeshResourceSampleRadius = 4f;
        private const float ResourceApproachPadding = 1.25f;
        private const int ResourceApproachDirectionCount = 16;

        private readonly List<Vector3> _navigationCandidateBuffer = new List<Vector3>();

        public global::SceneResourceTier ResourceTier => _resourceTier;
        public IReadOnlyList<GameplayTargetEntityMember> ResourceMembers => _resourceMembers;

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
            RefreshRuntimeState();
            resourceObject = null;
            navigationPosition = default;

            if (!TryResolveNavMeshStartPosition(agentPosition, navMeshAgent, out Vector3 startPosition, out int areaMask))
            {
                if (!TryGetNearestIncompleteResource(agentPosition, out resourceObject))
                    return false;

                navigationPosition = resourceObject != null ? resourceObject.transform.position : default;
                return resourceObject != null;
            }

            NavMeshPath path = new NavMeshPath();
            float nearestPathLength = float.MaxValue;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _resourceMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _resourceMembers[i];
                if (!IsMemberAvailableForSearch(member))
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

            FillResourceNavigationCandidates(
                resourceObject,
                fallbackPosition,
                agentPosition,
                _navigationCandidateBuffer);

            bool foundReachablePosition = false;
            for (int i = 0; i < _navigationCandidateBuffer.Count; i++)
            {
                if (!TryCalculateCompletePathToCandidate(
                        _navigationCandidateBuffer[i],
                        startPosition,
                        areaMask,
                        path,
                        out Vector3 candidateNavigationPosition,
                        out float candidatePathLength))
                {
                    continue;
                }

                if (candidatePathLength >= pathLength)
                    continue;

                navigationPosition = candidateNavigationPosition;
                pathLength = candidatePathLength;
                foundReachablePosition = true;
            }

            return foundReachablePosition;
        }

        private static void FillResourceNavigationCandidates(
            GameObject resourceObject,
            Vector3 fallbackPosition,
            Vector3 agentPosition,
            List<Vector3> candidates)
        {
            candidates.Clear();
            AddUniqueCandidate(candidates, fallbackPosition);

            if (resourceObject == null)
                return;

            Collider[] colliders = resourceObject.GetComponentsInChildren<Collider>();
            Bounds combinedBounds = default;
            bool hasBounds = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider resourceCollider = colliders[i];
                if (resourceCollider == null ||
                    !resourceCollider.enabled ||
                    !resourceCollider.gameObject.activeInHierarchy ||
                    resourceCollider.isTrigger)
                {
                    continue;
                }

                AddUniqueCandidate(candidates, resourceCollider.ClosestPoint(agentPosition));
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

            Vector3 center = hasBounds ? combinedBounds.center : resourceObject.transform.position;
            AddUniqueCandidate(candidates, center);

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
                AddUniqueCandidate(candidates, center + offset);
            }
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

        private static bool TryCalculateCompletePathToCandidate(
            Vector3 candidatePosition,
            Vector3 startPosition,
            int areaMask,
            NavMeshPath path,
            out Vector3 navigationPosition,
            out float pathLength)
        {
            navigationPosition = default;
            pathLength = float.MaxValue;

            if (!NavMesh.SamplePosition(
                    candidatePosition,
                    out NavMeshHit targetHit,
                    NavMeshResourceSampleRadius,
                    areaMask))
            {
                return false;
            }

            bool calculated = NavMesh.CalculatePath(
                startPosition,
                targetHit.position,
                areaMask,
                path);
            if (!calculated || path.status != NavMeshPathStatus.PathComplete)
                return false;

            navigationPosition = targetHit.position;
            pathLength = CalculatePathLength(path);
            return true;
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }

        // 根据资源对象当前状态回填成员完成状态
        private static void RefreshMemberCompletionFromResource(GameplayTargetEntityMember member)
        {
            if (member == null || member.HasBeenCompleted)
                return;

            if (member.EntityObject == null)
            {
                if (Application.isPlaying)
                    member.MarkCompleted();

                return;
            }

            if (member.TryGetComponent(out global::LootBoxEntity lootBox))
            {
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
    }
}
