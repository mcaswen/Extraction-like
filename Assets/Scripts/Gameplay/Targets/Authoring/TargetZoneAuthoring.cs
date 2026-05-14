using System.Collections.Generic;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 区域目标配置
    /// 聚合子群目标的接触和完成状态
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetZoneAuthoring : GameplayTargetAuthoringBase
    {
        [SerializeField] private List<GameplayTargetClusterAuthoringBase> _clusters =
            new List<GameplayTargetClusterAuthoringBase>();

        public override GameplayTargetLevel TargetLevel => GameplayTargetLevel.Zone;
        public override GameplayTargetKind TargetKind => GameplayTargetKind.Mixed;
        protected override string IdPrefix => "Zone";

        public IReadOnlyList<GameplayTargetClusterAuthoringBase> Clusters => _clusters;

        protected override void OnEnable()
        {
            base.OnEnable();
            RebuildClusterListFromChildren();
            RefreshAggregatedState();
        }

        private void Update()
        {
            RefreshAggregatedState();
        }

        /// <summary>
        /// 注册一个隶属于当前区域的群目标
        /// </summary>
        /// <param name="cluster"></param>
        public void RegisterCluster(GameplayTargetClusterAuthoringBase cluster)
        {
            if (cluster == null || _clusters.Contains(cluster))
                return;

            _clusters.Add(cluster);
            RefreshAggregatedState();
        }

        /// <summary>
        /// 注销一个隶属于当前区域的群目标
        /// </summary>
        /// <param name="cluster"></param>
        public void UnregisterCluster(GameplayTargetClusterAuthoringBase cluster)
        {
            if (cluster == null)
                return;

            _clusters.Remove(cluster);
            RefreshAggregatedState();
        }

        /// <summary>
        /// 根据子群目标刷新区域目标状态
        /// 任意子群已接触则区域已接触，所有子群完成则区域完成
        /// </summary>
        public void RefreshAggregatedState()
        {
            bool hasAnyCluster = false;
            bool hasTouchedCluster = false;
            bool hasIncompleteCluster = false;

            for (int i = _clusters.Count - 1; i >= 0; i--)
            {
                GameplayTargetClusterAuthoringBase cluster = _clusters[i];
                if (cluster == null)
                {
                    _clusters.RemoveAt(i);
                    continue;
                }

                hasAnyCluster = true;
                hasTouchedCluster |= cluster.HasBeenTouched;
                hasIncompleteCluster |= !cluster.HasBeenCompleted;
            }

            SetTouched(hasTouchedCluster);
            SetCompleted(hasAnyCluster && !hasIncompleteCluster);
        }

        [ContextMenu("Rebuild Cluster List From Children")]
        private void RebuildClusterListFromChildren()
        {
            // Zone 与 Cluster 强绑定，默认只收集当前 Zone 子层级下的群目标
            _clusters.Clear();
            GameplayTargetClusterAuthoringBase[] childClusters =
                GetComponentsInChildren<GameplayTargetClusterAuthoringBase>(true);
            _clusters.AddRange(childClusters);

            for (int i = _clusters.Count - 1; i >= 0; i--)
            {
                if (_clusters[i] == null || _clusters[i].Zone != this)
                    _clusters.RemoveAt(i);
            }
        }
    }
}
