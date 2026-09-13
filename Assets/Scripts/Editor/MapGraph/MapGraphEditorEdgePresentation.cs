using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>Editor 连线的只读绘制/命中事实。警示线不改变模型，也不充当发布几何证据。</summary>
    public readonly struct MapGraphEditorEdgePresentation
    {
        public bool HasSegment { get; }
        public Vector2 From { get; }
        public Vector2 To { get; }
        public string Message { get; }
        public bool RequiresAttention => !string.IsNullOrEmpty(Message);
        public bool CanRepairDirection { get; }

        private MapGraphEditorEdgePresentation(bool hasSegment, Vector2 from, Vector2 to, string message = "", bool canRepairDirection = false)
        { HasSegment = hasSegment; From = from; To = to; Message = message; CanRepairDirection = canRepairDirection; }

        public static MapGraphEditorEdgePresentation Resolve(MapGraphLayoutDraft layout, MapGraphEdgeDefinition edge)
        {
            if (layout == null || edge == null || !layout.Graph.TryGetNode(edge.FromNodeId, out _) || !layout.Graph.TryGetNode(edge.ToNodeId, out _))
                return new MapGraphEditorEdgePresentation(false, default, default, "连接端点已不存在，请同步场景。");
            if (MapGraphGeometry.TryGetVisibleSegment(layout, edge, out var from, out var to))
                return new MapGraphEditorEdgePresentation(true, from, to, edge.UseColorOverride && edge.ColorOverride.a <= 0.05f
                    ? "自定义颜色接近透明，编辑器用警示虚线标出原连接；请提高颜色透明度中的 Alpha 值。" : "");

            if (!MapGraphGeometry.TryGetAxis(layout.Graph.GetNodePosition(edge.FromNodeId), layout.Graph.GetNodePosition(edge.ToNodeId), out var axis))
                return new MapGraphEditorEdgePresentation(false, default, default,
                    "两群尚未同行或同列，或位置重叠，原连接仍保留；请对齐、重选端点或删除连接。");
            bool repairDirection = axis != edge.Axis;
            if (!MapGraphGeometry.TryGetVisibleSegment(layout, edge, axis, 0, 0, out from, out to))
                return new MapGraphEditorEdgePresentation(false, default, default, "群图标重叠或间距不足，原连接仍保留；请拉开两群，或删除连接。");
            return new MapGraphEditorEdgePresentation(true, from, to, repairDirection
                ? "连接记录的方向与当前摆放不符，警示虚线表示原连接；可按当前对齐修正方向。"
                : "两端留白过大或无效，无法显示原线段；警示虚线保留连接位置，请调整起点和终点留白。", repairDirection);
        }
    }
}
