using System.Collections.Generic;
using Gameplay.Targets.Data;
using Gameplay.Targets.Runtime;
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

        [Header("Zone Range Shape")]
        [SerializeField] private float _rangePadding = 4f;
        [SerializeField] private float _fallbackRadius = 8f;
        [SerializeField] private int _circleSegments = 48;
        [SerializeField] private int _smoothSegmentsPerEdge = 8;
        [SerializeField] private float _rangeHeightOffset = 0.08f;
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _rangeColor = new Color(0.04f, 0.45f, 0.28f, 0.95f);
        [SerializeField] private float _rangeLineWidth = 0.18f;
        [SerializeField] private LineRenderer _rangeLineRenderer;

        [Header("Collider Range Override")]
        [SerializeField] private GameObject _rangeColliderTarget;

        [Header("Ground Projection")]
        [SerializeField] private float _groundProbeHeight = 20f;
        [SerializeField] private float _groundProbeDistance = 80f;
        [SerializeField] private float _minGroundNormalY = 0.35f;

        private readonly List<Vector3> _sourcePointBuffer = new List<Vector3>();
        private readonly List<Vector3> _rangePoints = new List<Vector3>();
        private Vector3 _cachedCenterPosition;
        private bool _hasCachedShape;

        public override GameplayTargetLevel TargetLevel => GameplayTargetLevel.Zone;
        public override GameplayTargetKind TargetKind => GameplayTargetKind.Mixed;
        public override Vector3 CenterPosition => _hasCachedShape ? _cachedCenterPosition : transform.position;
        protected override string IdPrefix => "Zone";

        public IReadOnlyList<GameplayTargetClusterAuthoringBase> Clusters => _clusters;
        public IReadOnlyList<Vector3> RangePoints => _rangePoints;

        protected override void OnValidate()
        {
            base.OnValidate();
            RefreshRangeShape();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AddMissingChildClusters();
            RefreshAggregatedState();
            RefreshRangeShape();
        }

        private void Update()
        {
            RefreshAggregatedState();
            RefreshRangeShape();
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
            RefreshRangeShape();
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
            RefreshRangeShape();
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
                    continue;

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
            AddMissingChildClusters();
            RefreshRangeShape();
        }

        // 自动补充子层级中的群目标，但不清空手动配置，避免编辑器列表槽位被刷新逻辑吞掉
        private void AddMissingChildClusters()
        {
            GameplayTargetClusterAuthoringBase[] childClusters =
                GetComponentsInChildren<GameplayTargetClusterAuthoringBase>(true);
            for (int i = 0; i < childClusters.Length; i++)
            {
                GameplayTargetClusterAuthoringBase childCluster = childClusters[i];
                if (childCluster == null || childCluster.Zone != this || _clusters.Contains(childCluster))
                    continue;

                _clusters.Add(childCluster);
            }
        }

        /// <summary>
        /// 刷新区域目标范围轮廓
        /// 区域圈基于子群目标范围点生成，默认比群目标更宽更深
        /// </summary>
        public void RefreshRangeShape()
        {
            BuildRangeShape();
            ApplyRangeLineRenderer();
        }

        [ContextMenu("Attach Range Line Renderer")]
        private void AttachRangeLineRenderer()
        {
            if (_rangeLineRenderer == null)
                _rangeLineRenderer = GetComponent<LineRenderer>();

            if (_rangeLineRenderer == null)
                _rangeLineRenderer = gameObject.AddComponent<LineRenderer>();

            _rangeLineRenderer.loop = true;
            _rangeLineRenderer.useWorldSpace = true;

            RefreshRangeShape();
        }

        // 收集子群轮廓点作为区域范围的输入，保证区域圈包住所有小群
        private void CollectSourcePoints()
        {
            _sourcePointBuffer.Clear();
            for (int i = _clusters.Count - 1; i >= 0; i--)
            {
                GameplayTargetClusterAuthoringBase cluster = _clusters[i];
                if (cluster == null)
                    continue;

                IReadOnlyList<Vector3> clusterRangePoints = cluster.RangePoints;
                // 场景重载时 Zone 的 OnValidate 可能早于 Cluster；先建立子群轮廓，
                // 避免将群中心误当成完整范围并把缩小后的线写回场景。
                if (clusterRangePoints == null || clusterRangePoints.Count == 0)
                {
                    cluster.RefreshRangeShape();
                    clusterRangePoints = cluster.RangePoints;
                }
                if (clusterRangePoints != null && clusterRangePoints.Count > 0)
                {
                    for (int pointIndex = 0; pointIndex < clusterRangePoints.Count; pointIndex++)
                    {
                        _sourcePointBuffer.Add(clusterRangePoints[pointIndex]);
                    }

                    continue;
                }

                _sourcePointBuffer.Add(cluster.CenterPosition);
            }
        }

        // 根据子群位置生成区域轮廓，并将结果投射到地面
        private void BuildRangeShape()
        {
            if (TryBuildColliderRangeShape())
                return;

            CollectSourcePoints();

            GameplayTargetShapeUtility.BuildSmoothRange(
                _sourcePointBuffer,
                transform.position,
                _rangePadding,
                _fallbackRadius,
                _circleSegments,
                _smoothSegmentsPerEdge,
                _rangePoints,
                out _cachedCenterPosition);

            ProjectRangeShapeToGround(transform);
            _hasCachedShape = true;
        }

        // 如果配置了 Collider 来源，Zone 直接使用它的水平外轮廓作为范围
        private bool TryBuildColliderRangeShape()
        {
            Collider rangeCollider = ResolveRangeCollider();
            if (rangeCollider == null)
                return false;

            _rangePoints.Clear();
            if (rangeCollider is BoxCollider boxCollider)
            {
                BuildBoxColliderRangeShape(boxCollider);
            }
            else if (!TryBuildBoundsRangeShape(rangeCollider.bounds))
            {
                return false;
            }

            ProjectRangeShapeToGround(rangeCollider.transform);
            _hasCachedShape = true;
            return true;
        }

        private Collider ResolveRangeCollider()
        {
            if (_rangeColliderTarget == null)
                return null;

            if (_rangeColliderTarget.TryGetComponent(out Collider directCollider))
                return directCollider;

            return _rangeColliderTarget.GetComponentInChildren<Collider>(true);
        }

        private void BuildBoxColliderRangeShape(BoxCollider boxCollider)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Transform colliderTransform = boxCollider.transform;
            Vector3 localCenter = boxCollider.center;

            _rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(-halfSize.x, 0f, -halfSize.z)));
            _rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(-halfSize.x, 0f, halfSize.z)));
            _rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(halfSize.x, 0f, halfSize.z)));
            _rangePoints.Add(colliderTransform.TransformPoint(localCenter + new Vector3(halfSize.x, 0f, -halfSize.z)));
            _cachedCenterPosition = colliderTransform.TransformPoint(localCenter);
        }

        private bool TryBuildBoundsRangeShape(Bounds bounds)
        {
            if (bounds.size.x <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
                return false;

            float y = bounds.center.y;
            _rangePoints.Add(new Vector3(bounds.min.x, y, bounds.min.z));
            _rangePoints.Add(new Vector3(bounds.min.x, y, bounds.max.z));
            _rangePoints.Add(new Vector3(bounds.max.x, y, bounds.max.z));
            _rangePoints.Add(new Vector3(bounds.max.x, y, bounds.min.z));
            _cachedCenterPosition = bounds.center;
            return true;
        }

        private void ProjectRangeShapeToGround(Transform groundProjectionOwner)
        {
            _cachedCenterPosition = GameplayTargetShapeUtility.ProjectPointToGround(
                _cachedCenterPosition,
                groundProjectionOwner,
                _groundProbeHeight,
                _groundProbeDistance,
                _minGroundNormalY);
            _cachedCenterPosition += Vector3.up * _rangeHeightOffset;

            for (int i = 0; i < _rangePoints.Count; i++)
            {
                Vector3 point = GameplayTargetShapeUtility.ProjectPointToGround(
                    _rangePoints[i],
                    groundProjectionOwner,
                    _groundProbeHeight,
                    _groundProbeDistance,
                    _minGroundNormalY);
                point.y += _rangeHeightOffset;
                _rangePoints[i] = point;
            }
        }

        // 如果配置了 LineRenderer，则把区域轮廓同步到场景表现
        private void ApplyRangeLineRenderer()
        {
            if (_rangeLineRenderer == null)
                return;

            _rangeLineRenderer.useWorldSpace = true;
            _rangeLineRenderer.loop = true;
            _rangeLineRenderer.startColor = _rangeColor;
            _rangeLineRenderer.endColor = _rangeColor;
            _rangeLineRenderer.widthMultiplier = Mathf.Max(0.01f, _rangeLineWidth);
            _rangeLineRenderer.positionCount = _rangePoints.Count;
            for (int i = 0; i < _rangePoints.Count; i++)
            {
                _rangeLineRenderer.SetPosition(i, _rangePoints[i]);
            }
        }

        // Gizmo 使用同一份区域轮廓点，方便未挂 LineRenderer 时也能预览范围
        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;

            BuildRangeShape();
            if (_rangePoints.Count <= 1)
                return;

            Gizmos.color = _rangeColor;
            for (int i = 0; i < _rangePoints.Count; i++)
            {
                Vector3 current = _rangePoints[i];
                Vector3 next = _rangePoints[(i + 1) % _rangePoints.Count];
                Gizmos.DrawLine(current, next);
            }

            Gizmos.DrawSphere(_cachedCenterPosition, 0.28f);
        }
    }
}
