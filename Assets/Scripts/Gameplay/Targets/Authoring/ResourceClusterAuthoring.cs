using System.Collections.Generic;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 资源群目标配置
    /// 通过手动拖拽资源实体，把多个箱子或地面掉落抽象成一个群目标
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceClusterAuthoring : GameplayTargetClusterAuthoringBase
    {
        [Header("Resource Members")]
        [SerializeField] private List<GameplayTargetEntityMember> _resourceMembers =
            new List<GameplayTargetEntityMember>();

        public override GameplayTargetKind TargetKind => GameplayTargetKind.Resource;
        protected override string IdPrefix => "ResourceCluster";

        public IReadOnlyList<GameplayTargetEntityMember> ResourceMembers => _resourceMembers;

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
