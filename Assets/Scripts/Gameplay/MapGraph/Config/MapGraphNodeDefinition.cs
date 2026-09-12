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
        [SerializeField] private string _zoneId, _rowId, _columnId, _sourceObjectId;
        [SerializeField] private Vector2 _footprint = new Vector2(28, 28);
        [SerializeField] private bool _positionLocked;

        public MapGraphNodeDefinition(
            string nodeId,
            MapGraphNodeKind nodeKind,
            Vector2 position,
            string displayName = "",
            string description = "",
            Sprite icon = null,
            MapGraphNodeIconKind iconKind = MapGraphNodeIconKind.None,
            MapGraphResourceTier resourceTier = MapGraphResourceTier.None,
            MapGraphDangerTier dangerTier = MapGraphDangerTier.None,
            string zoneId = "", Vector2 footprint = default, string rowId = "", string columnId = "",
            bool positionLocked = false, string sourceObjectId = "")
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
            _zoneId = zoneId; _footprint = footprint == default ? new Vector2(28, 28) : footprint;
            _rowId = rowId; _columnId = columnId; _positionLocked = positionLocked; _sourceObjectId = sourceObjectId;
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
        /// 正式节点为 Zone 中心相对坐标；旧节点为全图坐标。显示位置统一通过 MapGraphService 解析。
        /// </summary>
        public Vector2 Position => _position;
        public string ZoneId => _zoneId ?? string.Empty;
        public Vector2 Footprint => _footprint;
        public string RowId => _rowId ?? string.Empty;
        public string ColumnId => _columnId ?? string.Empty;
        public bool PositionLocked => _positionLocked;
        public string SourceObjectId => _sourceObjectId ?? string.Empty;

        public MapGraphNodeDefinition WithLayout(Vector2 localPosition, string rowId, string columnId, bool locked)
            => new MapGraphNodeDefinition(NodeId, NodeKind, localPosition, DisplayName, Description, Icon, IconKind,
                ResourceTier, DangerTier, ZoneId, Footprint, rowId, columnId, locked, SourceObjectId);

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
