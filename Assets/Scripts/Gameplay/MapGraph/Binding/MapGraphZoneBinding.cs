using System;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>区域 ID 到场景区域的直接绑定；不向独立地图资产写入场景对象。</summary>
    [Serializable]
    public sealed class MapGraphZoneBinding
    {
        [SerializeField] private string _zoneId, _sourceObjectId;
        [SerializeField] private TargetZoneAuthoring _zone;
        public string ZoneId => _zoneId ?? string.Empty;
        public string SourceObjectId => _sourceObjectId ?? string.Empty;
        public TargetZoneAuthoring Zone => _zone;
        public MapGraphZoneBinding(string zoneId, TargetZoneAuthoring zone, string sourceObjectId = "")
        { _zoneId = zoneId; _zone = zone; _sourceObjectId = sourceObjectId; }
    }
}
