using System;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// 抽象地图中的边定义
    /// 当前阶段按双向边处理，用于 UI 连线、寻路和 Agent 图上移动插值
    /// </summary>
    [Serializable]
    public sealed class MapGraphEdgeDefinition
    {
        [SerializeField] private string _edgeId;
        [SerializeField] private string _fromNodeId;
        [SerializeField] private string _toNodeId;
        [SerializeField] private float _lengthUnits = 1f;
        [SerializeField] private MapGraphAxis _axis;
        [SerializeField] private MapGraphEdgeOrigin _origin;
        [SerializeField] private float _fromInset, _toInset, _widthOverride;
        [SerializeField] private bool _useColorOverride;
        [SerializeField] private Color _colorOverride = Color.white;

        public MapGraphEdgeDefinition(
            string edgeId,
            string fromNodeId,
            string toNodeId,
            float lengthUnits, MapGraphAxis axis = MapGraphAxis.Unspecified,
            MapGraphEdgeOrigin origin = MapGraphEdgeOrigin.Legacy, float fromInset = 0, float toInset = 0,
            float widthOverride = 0, bool useColorOverride = false, Color colorOverride = default)
        {
            _edgeId = edgeId ?? string.Empty;
            _fromNodeId = fromNodeId ?? string.Empty;
            _toNodeId = toNodeId ?? string.Empty;
            _lengthUnits = Mathf.Max(0.1f, lengthUnits);
            _axis = axis; _origin = origin; _fromInset = fromInset; _toInset = toInset;
            _widthOverride = widthOverride; _useColorOverride = useColorOverride; _colorOverride = colorOverride;
        }

        /// <summary>
        /// 边稳定 ID
        /// </summary>
        public string EdgeId => _edgeId ?? string.Empty;

        /// <summary>
        /// 边的一端节点 ID
        /// </summary>
        public string FromNodeId => _fromNodeId ?? string.Empty;

        /// <summary>
        /// 边的另一端节点 ID
        /// </summary>
        public string ToNodeId => _toNodeId ?? string.Empty;

        /// <summary>
        /// 历史图的兼容成本。正式指挥图的导航成本另存，不使用显示线长。
        /// </summary>
        public float LengthUnits => Mathf.Max(0.1f, _lengthUnits);
        public MapGraphAxis Axis => _axis;
        public MapGraphEdgeOrigin Origin => _origin;
        public float FromInset => _fromInset;
        public float ToInset => _toInset;
        public float WidthOverride => _widthOverride;
        public bool UseColorOverride => _useColorOverride;
        public Color ColorOverride => _colorOverride;

        public MapGraphEdgeDefinition WithPresentation(MapGraphAxis axis, float fromInset, float toInset,
            float width, bool useColor, Color color, MapGraphEdgeOrigin origin)
            => new MapGraphEdgeDefinition(EdgeId, FromNodeId, ToNodeId, LengthUnits, axis, origin,
                fromInset, toInset, width, useColor, color);
    }
}
