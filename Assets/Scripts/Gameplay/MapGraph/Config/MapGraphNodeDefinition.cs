using System;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// 抽象地图中的单个节点定义
    /// 包含节点稳定 ID、显示名称、战术类型和 UI 平面坐标
    /// </summary>
    [Serializable]
    public sealed class MapGraphNodeDefinition
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private string _displayName;
        [SerializeField] private MapGraphNodeKind _nodeKind = MapGraphNodeKind.Custom;
        [SerializeField] private Vector2 _position;
        [SerializeField] private string _description;

        public MapGraphNodeDefinition(
            string nodeId,
            MapGraphNodeKind nodeKind,
            Vector2 position,
            string displayName = "",
            string description = "")
        {
            _nodeId = nodeId ?? string.Empty;
            _nodeKind = nodeKind;
            _position = position;
            _displayName = displayName ?? string.Empty;
            _description = description ?? string.Empty;
        }

        /// <summary>
        /// 节点稳定 ID
        /// 运行时绑定和寻路都通过它引用节点
        /// </summary>
        public string NodeId => _nodeId ?? string.Empty;

        /// <summary>
        /// 节点显示名称
        /// 为空时回退为节点 ID
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? NodeId : _displayName;

        /// <summary>
        /// 节点战术类型
        /// </summary>
        public MapGraphNodeKind NodeKind => _nodeKind;

        /// <summary>
        /// 节点在抽象图 UI 平面上的坐标
        /// </summary>
        public Vector2 Position => _position;

        /// <summary>
        /// 给策划或调试面板阅读的额外说明
        /// </summary>
        public string Description => _description ?? string.Empty;
    }
}
