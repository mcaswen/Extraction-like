using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    public enum MapGraphGenerationMode { ConnectionsAndLayout, LayoutOnly, ValidateOnly, PlacementConnections, PlacementAdjustment, PlacementValidation, NavigationEvidence }
    public enum MapGraphGenerationStage { Collecting, ScanningNavigation, Generating, Validating, Ready, Failed, Stale, Cancelled }

    /// <summary>计算结果；NavigationEvidence 只证明导航输入，不证明布局可发布。文档核对用途、请求和版本。</summary>
    public sealed class MapGraphGenerationResult
    {
        public string RequestId { get; }
        public long InputRevision { get; }
        public MapGraphGenerationMode Mode { get; }
        public MapGraphSceneSnapshot Scene { get; }
        public MapGraphLayoutDraft Layout { get; }
        public string GenerationSettingsJson { get; }
        public IReadOnlyList<MapGraphScannedAnchor> Anchors { get; }
        public IReadOnlyList<MapGraphScannedConnection> Connections { get; }
        internal MapGraphGenerationResult(string requestId, long inputRevision, MapGraphGenerationMode mode,
            MapGraphSceneSnapshot scene, MapGraphLayoutDraft layout, IEnumerable<MapGraphScannedAnchor> anchors,
            IEnumerable<MapGraphScannedConnection> connections, string generationSettingsJson)
        {
            RequestId = requestId; InputRevision = inputRevision; Mode = mode; Scene = scene; Layout = layout;
            GenerationSettingsJson = generationSettingsJson;
            Anchors = anchors.ToList().AsReadOnly(); Connections = connections.ToList().AsReadOnly();
        }
    }
}
