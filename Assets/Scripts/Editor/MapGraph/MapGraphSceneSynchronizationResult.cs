using System.Collections.Generic;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>同步提案及输入证据；应用由作者文档复核，结果本身不写资产。</summary>
    public sealed class MapGraphSceneSynchronizationResult
    {
        public MapGraphSceneSnapshot Scene { get; }
        public string OriginalFingerprint { get; }
        public long InputRevision { get; }
        public MapGraphLayoutDraft Layout { get; }
        public IReadOnlyList<MapGraphValidationIssue> Changes { get; }
        public bool HasChanges => Changes.Count > 0;
        internal MapGraphSceneSynchronizationResult(MapGraphSceneSnapshot scene, MapGraphLayoutDraft original,
            MapGraphLayoutDraft layout, IEnumerable<MapGraphValidationIssue> changes, long revision)
        { Scene = scene; OriginalFingerprint = original.ContentFingerprint; Layout = layout;
            Changes = new List<MapGraphValidationIssue>(changes).AsReadOnly(); InputRevision = revision; }
    }
}
