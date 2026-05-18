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
        private bool _hasCachedShape;

        public override GameplayTargetLevel TargetLevel => GameplayTargetLevel.Cluster;
        public TargetZoneAuthoring Zone => ResolveZone();
        public override Vector3 CenterPosition => _hasCachedShape ? _cachedCenterPosition : transform.position;
        public IReadOnlyList<Vector3> RangePoints => _rangePoints;

        protected virtual bool RefreshStateEveryFrame => false;
        protected virtual bool RefreshRangeEveryFrame => false;

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
            if (RefreshStateEveryFrame)
                RefreshRuntimeState();

            if (RefreshRangeEveryFrame)
                RefreshRangeShape();
        }

        /// <summary>
        /// 刷新群目标范围轮廓
        /// 用于编辑器预览和运行时 LineRenderer 显示
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

        protected abstract void CollectMemberPositions(List<Vector3> memberPositions);

        protected abstract void RefreshRuntimeState();

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
        private void BuildRangeShape()
        {
            _memberPositionBuffer.Clear();
            CollectMemberPositions(_memberPositionBuffer);

            GameplayTargetShapeUtility.BuildSmoothRange(
                _memberPositionBuffer,
                transform.position,
                _rangePadding,
                _fallbackRadius,
                _circleSegments,
                _smoothSegmentsPerEdge,
                _rangePoints,
                out _cachedCenterPosition);

            _cachedCenterPosition = GameplayTargetShapeUtility.ProjectPointToGround(
                _cachedCenterPosition,
                transform,
                _groundProbeHeight,
                _groundProbeDistance,
                _minGroundNormalY);
            _cachedCenterPosition += Vector3.up * _rangeHeightOffset;

            for (int i = 0; i < _rangePoints.Count; i++)
            {
                Vector3 point = GameplayTargetShapeUtility.ProjectPointToGround(
                    _rangePoints[i],
                    transform,
                    _groundProbeHeight,
                    _groundProbeDistance,
                    _minGroundNormalY);
                point.y += _rangeHeightOffset;
                _rangePoints[i] = point;
            }

            _hasCachedShape = true;
        }

        // 如果配置了 LineRenderer，则把计算出的范围点同步到场景表现
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

        // Gizmo 绘制直接复用运行时范围生成逻辑，保证编辑器预览和运行时表现一致
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

            Gizmos.DrawSphere(_cachedCenterPosition, 0.2f);
        }
    }
}
