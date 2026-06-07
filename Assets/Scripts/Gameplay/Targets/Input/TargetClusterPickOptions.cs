using UnityEngine;

namespace Gameplay.Targets.Input
{
    /// <summary>
    /// Screen-space target cluster picking options.
    /// </summary>
    public readonly struct TargetClusterPickOptions
    {
        public TargetClusterPickOptions(
            LayerMask clusterLayerMask,
            bool useClusterLayerMask,
            bool ignoreCompletedClusters)
        {
            ClusterLayerMask = clusterLayerMask;
            UseClusterLayerMask = useClusterLayerMask;
            IgnoreCompletedClusters = ignoreCompletedClusters;
        }

        public LayerMask ClusterLayerMask { get; }
        public bool UseClusterLayerMask { get; }
        public bool IgnoreCompletedClusters { get; }
    }
}
