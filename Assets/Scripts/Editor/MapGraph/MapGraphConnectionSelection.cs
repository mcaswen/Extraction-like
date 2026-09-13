using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>画布临时有序双选。只提供端点，不编辑图，也不保存到资产。</summary>
    public sealed class MapGraphConnectionSelection
    {
        public string FromNodeId { get; private set; } = "";
        public string ToNodeId { get; private set; } = "";
        public string RebindEdgeId { get; private set; } = "";
        public string Failure { get; private set; } = "";
        public long Revision { get; private set; } = -1;
        public bool HasPair => FromNodeId.Length > 0 && ToNodeId.Length > 0;
        public void Synchronize(MapGraphLayoutDraft layout, long revision)
        {
            if (Revision != revision || layout == null || FromNodeId.Length > 0 && !layout.Graph.TryGetNode(FromNodeId, out _) ||
                ToNodeId.Length > 0 && !layout.Graph.TryGetNode(ToNodeId, out _) || RebindEdgeId.Length > 0 && !layout.Graph.TryGetEdge(RebindEdgeId, out _)) Cancel();
            Revision = revision;
        }
        public void Cancel() { FromNodeId = ToNodeId = RebindEdgeId = Failure = ""; }
        public void BeginRebind(MapGraphLayoutDraft layout, string edgeId, long revision)
        {
            Synchronize(layout, revision); Cancel();
            if (layout != null && layout.Graph.TryGetEdge(edgeId, out _)) RebindEdgeId = edgeId;
            else Failure = "连接已不存在。";
        }
        public bool Pick(MapGraphLayoutDraft layout, string id, bool shift, long revision)
        {
            Synchronize(layout, revision); Failure = "";
            if (layout == null || !layout.Graph.TryGetNode(id, out _)) { Cancel(); return false; }
            if (!shift || FromNodeId.Length == 0) { FromNodeId = id; ToNodeId = ""; return false; }
            if (id == FromNodeId) { Failure = "起点和终点不能是同一个群。"; return false; }
            ToNodeId = id; return true;
        }
        public bool TryGetAxis(MapGraphLayoutDraft layout, out MapGraphAxis axis)
        {
            axis = MapGraphAxis.Unspecified;
            if (!HasPair || !layout.Graph.TryGetNode(FromNodeId, out _) || !layout.Graph.TryGetNode(ToNodeId, out _)) return false;
            var from = layout.Graph.GetNodePosition(FromNodeId); var to = layout.Graph.GetNodePosition(ToNodeId);
            if (MapGraphGeometry.Near(from, to)) { Failure = "两个群的位置重叠，请先分开。"; return false; }
            if (MapGraphGeometry.TryGetAxis(from, to, out axis)) return true;
            Failure = "两个群尚未同行或同列，请先网格对齐，或使用微调建议。"; return false;
        }
        public void Reject(string reason) { Failure = reason ?? ""; }
    }
}
