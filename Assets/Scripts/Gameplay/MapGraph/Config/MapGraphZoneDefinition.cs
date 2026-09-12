using System;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>区域的全图矩形和中央名称占位；不保存场景对象引用。</summary>
    [Serializable]
    public sealed class MapGraphZoneDefinition
    {
        [SerializeField] private string _zoneId;
        [SerializeField] private string _displayName;
        [SerializeField] private Rect _bounds;
        [SerializeField] private Vector2 _nameSafeSize;
        [SerializeField] private bool _layoutLocked;
        [SerializeField] private string _sourceObjectId;

        public string ZoneId => _zoneId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? ZoneId : _displayName;
        public Rect Bounds => _bounds;
        public Vector2 NameSafeSize => _nameSafeSize;
        public Rect NameSafeBounds => new Rect(_bounds.center - _nameSafeSize * 0.5f, _nameSafeSize);
        public bool LayoutLocked => _layoutLocked;
        public string SourceObjectId => _sourceObjectId ?? string.Empty;

        public MapGraphZoneDefinition(string zoneId, string displayName, Rect bounds, Vector2 nameSafeSize,
            bool layoutLocked = false, string sourceObjectId = "")
        {
            _zoneId = zoneId; _displayName = displayName; _bounds = bounds; _nameSafeSize = nameSafeSize;
            _layoutLocked = layoutLocked; _sourceObjectId = sourceObjectId;
        }

        public MapGraphZoneDefinition WithLayout(Rect bounds, bool locked)
            => new MapGraphZoneDefinition(ZoneId, DisplayName, bounds, NameSafeSize, locked, SourceObjectId);
    }
}
