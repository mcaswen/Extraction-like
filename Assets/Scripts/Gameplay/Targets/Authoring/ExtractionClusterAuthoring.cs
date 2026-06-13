using System.Collections.Generic;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 撤离点群目标配置
    /// 把一个或多个撤离点抽象成一个可被 Agent 感知和选择的群目标
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtractionClusterAuthoring : GameplayTargetClusterAuthoringBase
    {
        [Header("Extraction Members")]
        [SerializeField] private List<GameplayTargetEntityMember> _extractionMembers =
            new List<GameplayTargetEntityMember>();

        [Header("Collider Range Override")]
        [SerializeField] private GameObject _rangeColliderTarget;

        public override GameplayTargetKind TargetKind => GameplayTargetKind.Extraction;
        protected override string IdPrefix => "ExtractionCluster";

        public IReadOnlyList<GameplayTargetEntityMember> ExtractionMembers => _extractionMembers;

        /// <summary>
        /// 判断撤离点是否属于当前撤离点群
        /// </summary>
        /// <param name="extractionPoint"></param>
        /// <returns></returns>
        public bool ContainsExtractionPoint(global::ExtractionPointController extractionPoint)
        {
            if (extractionPoint == null)
                return false;

            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _extractionMembers[i];
                if (member != null && member.Matches(extractionPoint.gameObject))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取离 Agent 最近的可用撤离点
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="extractionPoint"></param>
        /// <returns></returns>
        public bool TryGetNearestExtractionPoint(
            Vector3 agentPosition,
            out global::ExtractionPointController extractionPoint)
        {
            extractionPoint = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _extractionMembers[i];
                if (!TryGetAvailableExtractionPoint(member, out global::ExtractionPointController candidate))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, candidate.transform.position);
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                extractionPoint = candidate;
                nearestDistanceSqr = distanceSqr;
            }

            return extractionPoint != null;
        }

        /// <summary>
        /// 标记群内某个撤离点已经被接触
        /// </summary>
        /// <param name="extractionPoint"></param>
        public void MarkExtractionTouched(global::ExtractionPointController extractionPoint)
        {
            GameplayTargetEntityMember member = FindMember(extractionPoint);
            if (member == null)
                return;

            member.MarkTouched();
            MarkTouched();
            RefreshRuntimeState();
        }

        /// <summary>
        /// 标记群内某个撤离点已经完成撤离
        /// </summary>
        /// <param name="extractionPoint"></param>
        public void MarkExtractionCompleted(global::ExtractionPointController extractionPoint)
        {
            GameplayTargetEntityMember member = FindMember(extractionPoint);
            if (member == null)
                return;

            member.MarkTouched();
            member.MarkCompleted();
            RefreshRuntimeState();
        }

        protected override bool TryBuildCustomRangeShape(
            List<Vector3> rangePoints,
            out Vector3 centerPosition,
            out Transform groundProjectionOwner)
        {
            centerPosition = transform.position;
            groundProjectionOwner = transform;

            Collider rangeCollider = ResolveRangeCollider();
            if (rangeCollider == null)
                return false;

            groundProjectionOwner = rangeCollider.transform;
            if (rangeCollider is BoxCollider boxCollider)
            {
                BuildBoxColliderRangeShape(boxCollider, rangePoints, out centerPosition);
                return true;
            }

            return TryBuildBoundsRangeShape(rangeCollider.bounds, rangePoints, out centerPosition);
        }

        protected override void CollectMemberPositions(List<Vector3> memberPositions)
        {
            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _extractionMembers[i];
                if (member?.EntityObject != null)
                    memberPositions.Add(member.EntityObject.transform.position);
            }
        }

        protected override void RefreshRuntimeState()
        {
            EnsureMemberIds();

            bool hasAnyMember = false;
            bool hasTouchedMember = false;
            bool hasCompletedMember = false;

            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _extractionMembers[i];
                if (member == null)
                    continue;

                hasAnyMember = true;
                hasTouchedMember |= member.HasBeenTouched;
                hasCompletedMember |= member.HasBeenCompleted;
            }

            // 撤离点群是可选入口集合，任意撤离点完成就视为群目标完成
            SetAggregatedState(hasTouchedMember, hasAnyMember && hasCompletedMember);
        }

        [ContextMenu("Generate Missing Member Ids")]
        private void GenerateMissingMemberIds()
        {
            EnsureMemberIds();
        }

        // 补齐成员 ID，保证撤离状态不会依赖对象名
        private void EnsureMemberIds()
        {
            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                _extractionMembers[i]?.EnsureEntityId(TargetId, i);
            }
        }

        // 根据撤离点对象查找对应成员
        private GameplayTargetEntityMember FindMember(global::ExtractionPointController extractionPoint)
        {
            if (extractionPoint == null)
                return null;

            for (int i = 0; i < _extractionMembers.Count; i++)
            {
                GameplayTargetEntityMember member = _extractionMembers[i];
                if (member != null && member.Matches(extractionPoint.gameObject))
                    return member;
            }

            return null;
        }

        private Collider ResolveRangeCollider()
        {
            if (_rangeColliderTarget == null)
                return null;

            if (_rangeColliderTarget.TryGetComponent(out Collider directCollider))
                return directCollider;

            return _rangeColliderTarget.GetComponentInChildren<Collider>(true);
        }

        private static void BuildBoxColliderRangeShape(
            BoxCollider boxCollider,
            List<Vector3> rangePoints,
            out Vector3 centerPosition)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Transform colliderTransform = boxCollider.transform;
            Vector3 localCenter = boxCollider.center;

            rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(-halfSize.x, 0f, -halfSize.z)));
            rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(-halfSize.x, 0f, halfSize.z)));
            rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(halfSize.x, 0f, halfSize.z)));
            rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(halfSize.x, 0f, -halfSize.z)));
            centerPosition = colliderTransform.TransformPoint(localCenter);
        }

        private static bool TryBuildBoundsRangeShape(
            Bounds bounds,
            List<Vector3> rangePoints,
            out Vector3 centerPosition)
        {
            centerPosition = bounds.center;
            if (bounds.size.x <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
                return false;

            float y = bounds.center.y;
            rangePoints.Add(new Vector3(bounds.min.x, y, bounds.min.z));
            rangePoints.Add(new Vector3(bounds.min.x, y, bounds.max.z));
            rangePoints.Add(new Vector3(bounds.max.x, y, bounds.max.z));
            rangePoints.Add(new Vector3(bounds.max.x, y, bounds.min.z));
            return true;
        }

        // 已完成或失效的撤离点不再作为执行候选
        private static bool TryGetAvailableExtractionPoint(
            GameplayTargetEntityMember member,
            out global::ExtractionPointController extractionPoint)
        {
            extractionPoint = null;
            if (member == null || member.HasBeenCompleted || member.EntityObject == null)
                return false;

            return member.TryGetComponent(out extractionPoint) &&
                   extractionPoint != null &&
                   extractionPoint.gameObject.activeInHierarchy;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}
