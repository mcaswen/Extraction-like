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

        public MapGraphEdgeDefinition(
            string edgeId,
            string fromNodeId,
            string toNodeId,
            float lengthUnits)
        {
            _edgeId = edgeId ?? string.Empty;
            _fromNodeId = fromNodeId ?? string.Empty;
            _toNodeId = toNodeId ?? string.Empty;
            _lengthUnits = Mathf.Max(0.1f, lengthUnits);
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
        /// 抽象边长度
        /// 影响寻路权重和图上移动速度换算
        /// </summary>
        public float LengthUnits => Mathf.Max(0.1f, _lengthUnits);
    }
}
