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
        [SerializeField] private Sprite _icon;
        [SerializeField] private MapGraphNodeIconKind _iconKind = MapGraphNodeIconKind.None;
        [SerializeField] private MapGraphResourceTier _resourceTier = MapGraphResourceTier.None;
        [SerializeField] private MapGraphDangerTier _dangerTier = MapGraphDangerTier.None;

        public MapGraphNodeDefinition(
            string nodeId,
            MapGraphNodeKind nodeKind,
            Vector2 position,
            string displayName = "",
            string description = "",
            Sprite icon = null,
            MapGraphNodeIconKind iconKind = MapGraphNodeIconKind.None,
            MapGraphResourceTier resourceTier = MapGraphResourceTier.None,
            MapGraphDangerTier dangerTier = MapGraphDangerTier.None)
        {
            _nodeId = nodeId ?? string.Empty;
            _nodeKind = nodeKind;
            _position = position;
            _displayName = displayName ?? string.Empty;
            _description = description ?? string.Empty;
            _icon = icon;
            _iconKind = iconKind;
            _resourceTier = resourceTier;
            _dangerTier = dangerTier;
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

        /// <summary>
        /// 节点显示图标
        /// 从旧桌游地图转换时会尽量沿用原 NodeIconSet
        /// </summary>
        public Sprite Icon => _icon;

        /// <summary>
        /// 旧桌游图标类型快照
        /// </summary>
        public MapGraphNodeIconKind IconKind => _iconKind;

        /// <summary>
        /// 旧桌游资源等级快照
        /// </summary>
        public MapGraphResourceTier ResourceTier => _resourceTier;

        /// <summary>
        /// 旧桌游危险等级快照
        /// </summary>
        public MapGraphDangerTier DangerTier => _dangerTier;
    }
}
