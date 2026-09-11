using System.Collections.Generic;
using Gameplay.Targets.Data;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    /// <summary>
    /// 群目标配置基类
    /// 负责 Zone 归属、成员范围轮廓和群目标状态聚合
    /// </summary>
    public abstract class GameplayTargetClusterAuthoringBase : GameplayTargetAuthoringBase
    {
        private static readonly Unity.Profiling.ProfilerMarker LateMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Cluster.LateUpdate");
        private static readonly Unity.Profiling.ProfilerMarker StateMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Cluster.State");
        private static readonly Unity.Profiling.ProfilerMarker InputMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Cluster.Input");
        private static readonly Unity.Profiling.ProfilerMarker RangeMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Cluster.Range");
        private const double RangeRefreshIntervalSeconds = 0.05;
        [Header("Zone Binding")]
        [SerializeField] private TargetZoneAuthoring _zone;
        [SerializeField] private bool _autoResolveZoneFromParent = true;

        [Header("Range Shape")]
        [SerializeField] private float _rangePadding = 2f;
        [SerializeField] private float _fallbackRadius = 2f;
        [SerializeField] private int _circleSegments = 32;
        [SerializeField] private int _smoothSegmentsPerEdge = 6;
        [SerializeField] private float _rangeHeightOffset = 0.05f;
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _rangeColor = new Color(0.15f, 0.85f, 0.65f, 0.9f);
        [SerializeField] private float _rangeLineWidth = 0.08f;
        [SerializeField] private LineRenderer _rangeLineRenderer;

        [Header("Ground Projection")]
        [SerializeField] private float _groundProbeHeight = 20f;
        [SerializeField] private float _groundProbeDistance = 80f;
        [SerializeField] private float _minGroundNormalY = 0.35f;

        private readonly List<Vector3> _memberPositionBuffer = new List<Vector3>();
        private readonly List<Vector3> _rangePoints = new List<Vector3>();
        private Vector3 _cachedCenterPosition;
        private readonly GameplayTargetRangeCache _rangeCache = new GameplayTargetRangeCache();
        private double _nextRangeRefreshTime;

        public override GameplayTargetLevel TargetLevel => GameplayTargetLevel.Cluster;
        public TargetZoneAuthoring Zone => ResolveZone();
        public override Vector3 CenterPosition => _rangeCache.HasShape ? _rangeCache.Center : transform.position;
        public IReadOnlyList<Vector3> RangePoints => _rangeCache.Points;
        public long RangeGeometryBuildCount => _rangeCache.GeometryBuildCount;
        public long RangeGroundProjectionCount => _rangeCache.GroundProjectionCount;
        public long RangeLineWriteCount => _rangeCache.LineWriteCount;
        public int RangeInputPointCount => _rangeCache.InputPointCount;

        private GameplayTargetRangeCache.Settings RangeSettings => new GameplayTargetRangeCache.Settings
        {
            Padding = _rangePadding, Radius = _fallbackRadius, CircleSegments = _circleSegments,
            SmoothSegments = _smoothSegmentsPerEdge, HeightOffset = _rangeHeightOffset,
            ProbeHeight = _groundProbeHeight, ProbeDistance = _groundProbeDistance, MinNormalY = _minGroundNormalY
        };

        protected virtual bool RefreshStateEveryFrame => false;
        protected virtual bool HideRangeWhenCompleted => false;
        protected LineRenderer ConfiguredRangeLineRenderer
        {
            get
            {
                if (_rangeLineRenderer == null)
                    _rangeLineRenderer = GetComponent<LineRenderer>();

                return _rangeLineRenderer;
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveZone();
            RefreshRangeShape();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveZone()?.RegisterCluster(this);
            RefreshRuntimeState();
            RefreshRangeShape();
        }

        protected override void OnDisable()
        {
            if (_zone != null)
                _zone.UnregisterCluster(this);

            base.OnDisable();
        }

        protected virtual void LateUpdate()
        {
            using var markerScope = LateMarker.Auto();
            if (RefreshStateEveryFrame)
                using (StateMarker.Auto()) RefreshRuntimeState();

            RefreshRangeShape(false);
        }

        /// <summary>
        /// 刷新群目标范围轮廓
        /// 用于编辑器预览和运行时 LineRenderer 显示
        /// </summary>
        public void RefreshRangeShape() => RefreshRangeShape(true);

        private void RefreshRangeShape(bool force)
        {
            if (HideRangeWhenCompleted && HasBeenCompleted)
            {
                ClearRangeLineRenderer();
                return;
            }

            // Limit display work in real time, including accelerated simulations. State and completion stay per frame.
            double now = Time.unscaledTimeAsDouble;
            if (!force && Application.isPlaying && now < _nextRangeRefreshTime)
                return;

            BuildRangeShape(force);
            _rangeCache.ApplyLine(_rangeLineRenderer, _rangeColor, _rangeLineWidth, force);
            _nextRangeRefreshTime = now + RangeRefreshIntervalSeconds;
        }

        [ContextMenu("Attach Range Line Renderer")]
        private void AttachRangeLineRenderer()
        {
            if (_rangeLineRenderer == null)
                _rangeLineRenderer = GetComponent<LineRenderer>();

            if (_rangeLineRenderer == null)
                _rangeLineRenderer = gameObject.AddComponent<LineRenderer>();

            ConfigureRangeLineRenderer(_rangeLineRenderer);

            RefreshRangeShape();
        }

        protected abstract void CollectMemberPositions(List<Vector3> memberPositions);

        protected abstract void RefreshRuntimeState();

        protected virtual bool TryBuildCustomRangeShape(
            List<Vector3> rangePoints,
            out Vector3 centerPosition,
            out Transform groundProjectionOwner)
        {
            centerPosition = transform.position;
            groundProjectionOwner = transform;
            return false;
        }

        /// <summary>
        /// 按另一个群目标的范围显示配置创建当前群的 LineRenderer
        /// 用于运行时生成的群目标复用来源群的显示风格
        /// </summary>
        /// <param name="sourceCluster"></param>
        /// <param name="templateRenderer"></param>
        protected void AttachRangeLineRendererFromTemplate(
            GameplayTargetClusterAuthoringBase sourceCluster,
            LineRenderer templateRenderer)
        {
            if (sourceCluster != null)
                CopyRangeDisplaySettingsFrom(sourceCluster);

            if (_rangeLineRenderer == null)
                _rangeLineRenderer = GetComponent<LineRenderer>();

            if (_rangeLineRenderer == null)
                _rangeLineRenderer = gameObject.AddComponent<LineRenderer>();

            CopyLineRendererSettings(templateRenderer, _rangeLineRenderer);
            ConfigureRangeLineRenderer(_rangeLineRenderer);
            RefreshRangeShape();
        }

        // 子类完成聚合计算后统一写回目标状态
        protected void SetAggregatedState(bool hasBeenTouched, bool hasBeenCompleted)
        {
            SetTouched(hasBeenTouched);
            SetCompleted(hasBeenCompleted);
        }

        // 默认从父级寻找 Zone，保持推荐层级结构下的强绑定关系
        protected TargetZoneAuthoring ResolveZone()
        {
            if (_zone != null || !_autoResolveZoneFromParent)
                return _zone;

            Transform current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent(out TargetZoneAuthoring parentZone))
                {
                    _zone = parentZone;
                    return _zone;
                }

                current = current.parent;
            }

            return _zone;
        }

        // 根据成员点生成范围轮廓，并同步缓存中心点
        private void BuildRangeShape(bool force)
        {
            bool custom;
            Transform groundProjectionOwner;
            using (InputMarker.Auto())
            {
                _rangePoints.Clear();
                custom = TryBuildCustomRangeShape(_rangePoints, out _cachedCenterPosition,
                    out groundProjectionOwner) && _rangePoints.Count > 1;
                if (!custom)
                {
                    _memberPositionBuffer.Clear();
                    CollectMemberPositions(_memberPositionBuffer);
                }
            }
            using (RangeMarker.Auto()) _rangeCache.Refresh(custom ? _rangePoints : _memberPositionBuffer,
                custom ? _cachedCenterPosition : transform.position, custom,
                custom && groundProjectionOwner != null ? groundProjectionOwner : transform, RangeSettings, force);
        }

        private void ClearRangeLineRenderer()
        {
            if (_rangeLineRenderer == null)
                return;

            _rangeLineRenderer.positionCount = 0;
        }

        private void CopyRangeDisplaySettingsFrom(GameplayTargetClusterAuthoringBase sourceCluster)
        {
            _rangePadding = sourceCluster._rangePadding;
            _fallbackRadius = sourceCluster._fallbackRadius;
            _circleSegments = sourceCluster._circleSegments;
            _smoothSegmentsPerEdge = sourceCluster._smoothSegmentsPerEdge;
            _rangeHeightOffset = sourceCluster._rangeHeightOffset;
            _drawGizmos = sourceCluster._drawGizmos;
            _rangeColor = sourceCluster._rangeColor;
            _rangeLineWidth = sourceCluster._rangeLineWidth;
            _groundProbeHeight = sourceCluster._groundProbeHeight;
            _groundProbeDistance = sourceCluster._groundProbeDistance;
            _minGroundNormalY = sourceCluster._minGroundNormalY;
        }

        private static void CopyLineRendererSettings(LineRenderer templateRenderer, LineRenderer targetRenderer)
        {
            if (templateRenderer == null || targetRenderer == null)
                return;

            targetRenderer.sharedMaterial = templateRenderer.sharedMaterial;
            targetRenderer.widthCurve = templateRenderer.widthCurve;
            targetRenderer.colorGradient = templateRenderer.colorGradient;
            targetRenderer.numCornerVertices = templateRenderer.numCornerVertices;
            targetRenderer.numCapVertices = templateRenderer.numCapVertices;
            targetRenderer.alignment = templateRenderer.alignment;
            targetRenderer.textureMode = templateRenderer.textureMode;
            targetRenderer.shadowCastingMode = templateRenderer.shadowCastingMode;
            targetRenderer.receiveShadows = templateRenderer.receiveShadows;
            targetRenderer.sortingLayerID = templateRenderer.sortingLayerID;
            targetRenderer.sortingOrder = templateRenderer.sortingOrder;
        }

        private static void ConfigureRangeLineRenderer(LineRenderer lineRenderer)
        {
            if (lineRenderer == null)
                return;

            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
        }

        // Gizmo 绘制直接复用运行时范围生成逻辑，保证编辑器预览和运行时表现一致
        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
                return;

            if (!Application.isPlaying) RefreshRangeShape(false);
            if (RangePoints.Count <= 1)
                return;

            Gizmos.color = _rangeColor;
            for (int i = 0; i < RangePoints.Count; i++)
            {
                Vector3 current = RangePoints[i];
                Vector3 next = RangePoints[(i + 1) % RangePoints.Count];
                Gizmos.DrawLine(current, next);
            }

            Gizmos.DrawSphere(CenterPosition, 0.2f);
        }
    }
}
