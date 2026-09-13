using System;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    public enum MapGraphSelectionKind { None, Node, Zone, Edge }

    /// <summary>纯编辑视口和鼠标交互。提出作者意图，不求解，不持久化。</summary>
    public sealed class MapGraphEditorCanvas
    {
        private static readonly Color Background = new Color32(16, 23, 28, 255);
        private static readonly Color ZoneFill = new Color32(25, 36, 43, 255);
        private static readonly Color Border = new Color32(51, 72, 81, 255);
        private static readonly Color Accent = new Color32(110, 211, 189, 255);
        private GUIStyle _zoneLabel, _smallLabel;
        private bool _fitted, _dragging, _panning, _resizing;
        private Vector2 _mouseStart, _positionStart, _ghost;
        private Rect _zoneStart;
        private string _connectFrom;
        private MapGraphAxis _connectAxis;
        private double _lastPreview;
        public MapGraphConnectionSelection ConnectionSelection { get; } = new MapGraphConnectionSelection();
        public long DocumentRevision { get; set; }
        public event Action ConnectionRequested;
        public bool GridPlacement { get; set; }
        public float GridSpacing { get; set; } = 80;
        public float Zoom { get; private set; } = 1;
        public Vector2 Center { get; private set; }
        public MapGraphSelectionKind SelectionKind { get; private set; }
        public string SelectionId { get; private set; } = "";
        public Rect ViewRect { get; private set; }
        public int PaintCount { get; private set; }
        public event Action<Func<MapGraphLayoutDraft, MapGraphLayoutDraft>, string, bool> EditRequested;
        public event Action CancelRequested;
        public MapGraphValidationIssue FocusedIssue { get; private set; }

        public void ClearDiagnostic() => FocusedIssue = null;
        public bool FocusDiagnostic(MapGraphLayoutDraft layout, MapGraphValidationIssue issue)
        {
            if (layout == null || issue == null) return false;
            if (!TryObjectBounds(layout, issue.SubjectId, out var bounds, out var kind))
            {
                if (!TryObjectBounds(layout, issue.RelatedId, out bounds, out kind)) return false;
                Select(kind, issue.RelatedId);
            }
            else Select(kind, issue.SubjectId);
            if (TryObjectBounds(layout, issue.RelatedId, out var related, out _))
                bounds = MapGraphGeometry.Union(bounds, related);
            FocusedIssue = issue; Center = bounds.center;
            Zoom = Mathf.Clamp(Mathf.Min(Mathf.Max(100, ViewRect.width - 100) / Mathf.Max(100, bounds.width),
                Mathf.Max(100, ViewRect.height - 100) / Mathf.Max(100, bounds.height)), 0.04f, 3);
            _fitted = true; return true;
        }
        private static bool TryObjectBounds(MapGraphLayoutDraft layout, string id, out Rect bounds, out MapGraphSelectionKind kind)
        {
            bounds = default; kind = MapGraphSelectionKind.None;
            if (string.IsNullOrEmpty(id)) return false;
            if (layout.Graph.TryGetNode(id, out var node))
            { bounds = MapGraphGeometry.NodeBounds(layout, node); kind = MapGraphSelectionKind.Node; return true; }
            if (layout.Graph.TryGetZone(id, out var zone))
            { bounds = zone.NameSafeBounds; kind = MapGraphSelectionKind.Zone; return true; }
            if (layout.Graph.TryGetEdge(id, out var edge) && layout.Graph.TryGetNode(edge.FromNodeId, out var from) && layout.Graph.TryGetNode(edge.ToNodeId, out var to))
            { bounds = MapGraphGeometry.Union(MapGraphGeometry.NodeBounds(layout, from), MapGraphGeometry.NodeBounds(layout, to)); kind = MapGraphSelectionKind.Edge; return true; }
            return false;
        }

        public Vector2 ToScreen(Vector2 point, Rect rect) => rect.center + new Vector2(point.x - Center.x, Center.y - point.y) * Zoom;
        public Vector2 ToMap(Vector2 point, Rect rect) => Center + new Vector2(point.x - rect.center.x, rect.center.y - point.y) / Zoom;
        public Rect ToScreen(Rect bounds, Rect rect)
        {
            var corner = ToScreen(new Vector2(bounds.xMin, bounds.yMax), rect);
            return new Rect(corner, bounds.size * Zoom);
        }
        public void ZoomAt(Vector2 point, Rect rect, float multiplier)
        {
            var fixedPoint = ToMap(point, rect); Zoom = Mathf.Clamp(Zoom * multiplier, 0.04f, 8);
            Center += fixedPoint - ToMap(point, rect);
        }
        public void Fit(MapGraphLayoutDraft layout, Rect rect)
        {
            if (layout == null || layout.Zones.Count == 0 || rect.width <= 0 || rect.height <= 0) return;
            var bounds = layout.Zones[0].Bounds;
            foreach (var zone in layout.Zones) bounds = MapGraphGeometry.Union(bounds, zone.Bounds);
            Center = bounds.center; Zoom = Mathf.Clamp(Mathf.Min((rect.width - 100) / Mathf.Max(1, bounds.width), (rect.height - 100) / Mathf.Max(1, bounds.height)), 0.04f, 8); _fitted = true;
        }
        public void Select(MapGraphSelectionKind kind, string id) { SelectionKind = kind; SelectionId = id ?? ""; }
        public (MapGraphSelectionKind kind, string id) Hit(MapGraphLayoutDraft layout, Vector2 screen, Rect rect)
        {
            if (layout == null) return (MapGraphSelectionKind.None, "");
            foreach (var node in layout.Nodes)
                if (Expanded(ToScreen(MapGraphGeometry.NodeBounds(layout, node), rect), 4).Contains(screen)) return (MapGraphSelectionKind.Node, node.NodeId);
            foreach (var edge in layout.Edges)
            {
                var presentation = MapGraphEditorEdgePresentation.Resolve(layout, edge);
                if (presentation.HasSegment && DistanceToSegment(screen, ToScreen(presentation.From, rect), ToScreen(presentation.To, rect)) < 6)
                    return (MapGraphSelectionKind.Edge, edge.EdgeId);
            }
            foreach (var zone in layout.Zones)
                if (ToScreen(zone.Bounds, rect).Contains(screen)) return (MapGraphSelectionKind.Zone, zone.ZoneId);
            return (MapGraphSelectionKind.None, "");
        }
        public void Draw(Rect rect, MapGraphLayoutDraft layout, MapGraphLayoutDraft preview, bool editable)
        {
            ViewRect = rect; EnsureStyles(); ConnectionSelection.Synchronize(layout, DocumentRevision);
            if (!_fitted) Fit(layout ?? preview, rect);
            // Clip all drawing, including pan/zoom, to the canvas; coordinates inside are local.
            GUI.BeginGroup(rect); var local = new Rect(0, 0, rect.width, rect.height);
            EditorGUI.DrawRect(local, Background);
            var visible = preview ?? layout;
            if (visible == null) GUI.Label(local, "生成当前场景的指挥地图", _zoneLabel);
            else
            {
                if (Event.current.type == EventType.Repaint) PaintCount++;
                foreach (var zone in visible.Zones)
                {
                    var bounds = ToScreen(zone.Bounds, local);
                    Box(bounds, ZoneFill, SelectionKind == MapGraphSelectionKind.Zone && SelectionId == zone.ZoneId ? Accent : Border, 1);
                    var nameRect = ToScreen(zone.NameSafeBounds, local);
                    if (GridPlacement) Box(nameRect, new Color(0.75f, 0.61f, 0.34f, 0.06f), new Color(0.75f, 0.61f, 0.34f, 0.35f), 1);
                    if (SelectionKind == MapGraphSelectionKind.Zone && SelectionId == zone.ZoneId)
                        EditorGUI.DrawRect(new Rect(bounds.xMax - 7, bounds.yMax - 7, 7, 7), Accent);
                }
                Handles.BeginGUI();
                if (GridPlacement) DrawGrid(local);
                foreach (var edge in visible.Edges)
                {
                    var presentation = MapGraphEditorEdgePresentation.Resolve(visible, edge);
                    if (presentation.HasSegment)
                    {
                        Handles.color = SelectionKind == MapGraphSelectionKind.Edge && SelectionId == edge.EdgeId ? Accent : presentation.RequiresAttention
                            ? new Color32(244, 170, 83, 255) : edge.UseColorOverride ? edge.ColorOverride : new Color32(86, 115, 123, 255);
                        var a = ToScreen(presentation.From, local); var b = ToScreen(presentation.To, local);
                        if (presentation.RequiresAttention) Handles.DrawDottedLine(a, b, 4);
                        else Handles.DrawAAPolyLine(Mathf.Max(1, (edge.WidthOverride > 0 ? edge.WidthOverride : 2) * Zoom), a, b);
                    }
                }
                if (preview != null && layout != null)
                    foreach (var node in layout.Nodes)
                        if (preview.Graph.TryGetNode(node.NodeId, out _) && !MapGraphGeometry.Near(layout.Graph.GetNodePosition(node.NodeId), preview.Graph.GetNodePosition(node.NodeId)))
                        {
                            var a = ToScreen(layout.Graph.GetNodePosition(node.NodeId), local); var b = ToScreen(preview.Graph.GetNodePosition(node.NodeId), local);
                            Handles.color = new Color(0.85f, 0.75f, 0.45f, 0.65f); Handles.DrawDottedLine(a, b, 4);
                            Box(new Rect(a - Vector2.one * 7, Vector2.one * 14), Color.clear, Handles.color, 1);
                        }
                foreach (var node in visible.Nodes) DrawNode(visible, node, local);
                if (FocusedIssue != null && preview == null)
                {
                    DrawDiagnosticObject(visible, FocusedIssue.SubjectId, local, new Color32(244, 170, 83, 255));
                    DrawDiagnosticObject(visible, FocusedIssue.RelatedId, local, new Color32(230, 117, 145, 255));
                }
                if (_dragging)
                {
                    Handles.color = Accent;
                    if (_connectFrom != null)
                    {
                        var start = ToScreen(visible.Graph.GetNodePosition(_connectFrom), local); var end = ToScreen(_ghost, local);
                        if (_connectAxis == MapGraphAxis.Horizontal) end.y = start.y; else end.x = start.x;
                        Handles.DrawAAPolyLine(2, start, end);
                    }
                    else if (SelectionKind == MapGraphSelectionKind.Node)
                        Box(new Rect(ToScreen(GridPlacement ? MapGraphGridPlacement.Snap(_positionStart + _ghost - _mouseStart, GridSpacing) : _ghost, local) - Vector2.one * 10, Vector2.one * 20), Color.clear, Accent, 1);
                }
                Handles.EndGUI();
                // 连接允许穿过名称框，文字最后绘制，避免线条盖住字形。
                _zoneLabel.fontSize = Mathf.Clamp(Mathf.RoundToInt(21 * Zoom), 10, 25);
                foreach (var zone in visible.Zones) GUI.Label(ToScreen(zone.NameSafeBounds, local), zone.DisplayName, _zoneLabel);
                if (preview != null) GUI.Label(new Rect(15, 10, local.width - 30, 22), "候选预览 · 金色虚线表示位置调整，实际连接仍为横竖直线", _smallLabel);
                if (layout != null) HandleInput(layout, local, editable);
            }
            GUI.Label(new Rect(15, local.height - 26, local.width - 30, 20), "滚轮缩放  ·  中键平移  ·  网格拖动  ·  Shift 选两群连线  ·  群图标避让名称框，连线可穿过", _smallLabel);
            GUI.EndGroup();
        }
        private void DrawDiagnosticObject(MapGraphLayoutDraft layout, string id, Rect rect, Color color)
        {
            if (!TryObjectBounds(layout, id, out var bounds, out var kind)) return;
            if (kind == MapGraphSelectionKind.Edge && layout.Graph.TryGetEdge(id, out var edge))
            {
                // 失效斜线只框端点，不将诊断辅助线伪装成新的斜向连接。
                foreach (string endpoint in new[] { edge.FromNodeId, edge.ToNodeId })
                    if (TryObjectBounds(layout, endpoint, out var nodeBounds, out _)) Box(Expanded(ToScreen(nodeBounds, rect), 5), Color.clear, color, 2);
                var presentation = MapGraphEditorEdgePresentation.Resolve(layout, edge);
                if (presentation.HasSegment)
                {
                    Handles.color = color; var a = ToScreen(presentation.From, rect); var b = ToScreen(presentation.To, rect);
                    if (presentation.RequiresAttention) Handles.DrawDottedLine(a, b, 4);
                    else Handles.DrawAAPolyLine(3, a, b);
                }
            }
            else Box(Expanded(ToScreen(bounds, rect), 3), new Color(color.r, color.g, color.b, 0.08f), color, 2);
        }
        private void DrawNode(MapGraphLayoutDraft layout, MapGraphNodeDefinition node, Rect rect)
        {
            var bounds = ToScreen(MapGraphGeometry.NodeBounds(layout, node), rect); var p = bounds.center;
            var color = node.NodeKind == MapGraphNodeKind.Resource ? new Color32(211, 180, 118, 255) : node.NodeKind == MapGraphNodeKind.Extraction ? Accent : new Color32(216, 126, 127, 255);
            bool selected = SelectionKind == MapGraphSelectionKind.Node && SelectionId == node.NodeId;
            Box(bounds, Background, selected ? Color.white : color, selected ? 2 : 1);
            Handles.color = color; float r = Mathf.Max(3, bounds.width * 0.26f);
            if (node.NodeKind == MapGraphNodeKind.Resource)
            {
                Handles.DrawAAPolyLine(1.5f, p + new Vector2(-r, -r * 0.65f), p + new Vector2(r, -r * 0.65f), p + new Vector2(r, r * 0.7f), p + new Vector2(-r, r * 0.7f), p + new Vector2(-r, -r * 0.65f));
                Handles.DrawAAPolyLine(1.5f, p + new Vector2(0, -r), p + new Vector2(0, r * 0.7f));
            }
            else if (node.NodeKind == MapGraphNodeKind.Extraction)
            {
                Handles.DrawAAPolyLine(1.5f, p + new Vector2(-r * 0.2f, -r), p + new Vector2(-r, -r), p + new Vector2(-r, r), p + new Vector2(-r * 0.2f, r));
                Handles.DrawAAPolyLine(1.5f, p + new Vector2(-r * 0.1f, 0), p + new Vector2(r, 0), p + new Vector2(r * 0.4f, -r * 0.55f));
                Handles.DrawAAPolyLine(1.5f, p + new Vector2(r, 0), p + new Vector2(r * 0.4f, r * 0.55f));
            }
            else Handles.DrawAAPolyLine(1.5f, p + Vector2.up * r, p + Vector2.right * r, p + Vector2.down * r, p + Vector2.left * r, p + Vector2.up * r);
            if (selected && !GridPlacement)
                for (int i = 0; i < 4; i++) EditorGUI.DrawRect(new Rect(Port(bounds, i) - Vector2.one * 3, Vector2.one * 6), Accent);
            if (node.NodeId == ConnectionSelection.FromNodeId || node.NodeId == ConnectionSelection.ToNodeId)
            {
                bool from = node.NodeId == ConnectionSelection.FromNodeId;
                var marker = from ? Accent : (Color)new Color32(237, 185, 105, 255);
                Box(Expanded(bounds, 4), Color.clear, marker, 2);
                GUI.Label(new Rect(p.x - 23, bounds.yMin - 23, 80, 20), from ? "起点" : "终点", _smallLabel);
            }
            if (bounds.Contains(Event.current.mousePosition)) GUI.Label(new Rect(p.x + 18, p.y - 24, 300, 22), node.DisplayName, _smallLabel);
        }
        private void HandleInput(MapGraphLayoutDraft layout, Rect rect, bool editable)
        {
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && rect.Contains(ev.mousePosition)) { ZoomAt(ev.mousePosition, rect, Mathf.Exp(-ev.delta.y * 0.08f)); ev.Use(); }
            if (ev.type == EventType.MouseDown && ev.button == 2 && rect.Contains(ev.mousePosition)) { _panning = true; ev.Use(); }
            if (_panning && ev.type == EventType.MouseDrag) { Center += new Vector2(-ev.delta.x, ev.delta.y) / Zoom; ev.Use(); }
            if (_panning && ev.type == EventType.MouseUp) { _panning = false; ev.Use(); }
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { _dragging = false; _connectFrom = null; ConnectionSelection.Cancel(); CancelRequested?.Invoke(); ev.Use(); }
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Delete && editable && SelectionKind == MapGraphSelectionKind.Edge)
            { string id = SelectionId; EditRequested?.Invoke(g => MapGraphEditOperations.DeleteEdge(g, id), "删除连接", true); ev.Use(); }
            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                _connectFrom = null;
                var clicked = Hit(layout, ev.mousePosition, rect);
                if (editable && ev.shift && clicked.kind == MapGraphSelectionKind.Node)
                {
                    _dragging = false;
                    if (ConnectionSelection.FromNodeId.Length == 0 && SelectionKind == MapGraphSelectionKind.Node && SelectionId != clicked.id)
                        ConnectionSelection.Pick(layout, SelectionId, false, DocumentRevision);
                    bool ready = ConnectionSelection.Pick(layout, clicked.id, true, DocumentRevision);
                    Select(clicked.kind, clicked.id);
                    if (ready) ConnectionRequested?.Invoke(); ev.Use(); return;
                }
                if (clicked.kind == MapGraphSelectionKind.Node) ConnectionSelection.Pick(layout, clicked.id, false, DocumentRevision);
                else ConnectionSelection.Cancel();
                if (!GridPlacement && editable && SelectionKind == MapGraphSelectionKind.Node && layout.Graph.TryGetNode(SelectionId, out var selected))
                {
                    var bounds = ToScreen(MapGraphGeometry.NodeBounds(layout, selected), rect);
                    for (int i = 0; i < 4; i++) if (Vector2.Distance(ev.mousePosition, Port(bounds, i)) <= 7)
                    { _connectFrom = SelectionId; _connectAxis = i < 2 ? MapGraphAxis.Horizontal : MapGraphAxis.Vertical; break; }
                }
                if (_connectFrom == null) { var hit = Hit(layout, ev.mousePosition, rect); Select(hit.kind, hit.id); }
                _mouseStart = ToMap(ev.mousePosition, rect); _ghost = _mouseStart; _lastPreview = 0;
                _dragging = editable && (_connectFrom != null || SelectionKind == MapGraphSelectionKind.Node || SelectionKind == MapGraphSelectionKind.Zone);
                if (SelectionKind == MapGraphSelectionKind.Node) _positionStart = layout.Graph.GetNodePosition(SelectionId);
                if (SelectionKind == MapGraphSelectionKind.Zone && layout.Graph.TryGetZone(SelectionId, out var zone))
                { _zoneStart = zone.Bounds; var bounds = ToScreen(zone.Bounds, rect); _resizing = Vector2.Distance(ev.mousePosition, bounds.max) < 12; }
                ev.Use();
            }
            if (_dragging && ev.type == EventType.MouseDrag)
            {
                _ghost = ToMap(ev.mousePosition, rect);
                if (!GridPlacement && _connectFrom == null && EditorApplication.timeSinceStartup - _lastPreview > 0.15)
                { RequestMove(false); _lastPreview = EditorApplication.timeSinceStartup; }
                ev.Use();
            }
            if (_dragging && ev.type == EventType.MouseUp)
            {
                _ghost = ToMap(ev.mousePosition, rect);
                if (_connectFrom != null)
                {
                    var hit = Hit(layout, ev.mousePosition, rect); string from = _connectFrom; var axis = _connectAxis;
                    if (hit.kind == MapGraphSelectionKind.Node && hit.id != from) EditRequested?.Invoke(g => MapGraphEditOperations.AddEdge(g, from, hit.id, axis), "添加连接", true);
                }
                else if ((_ghost - _mouseStart).sqrMagnitude > 0.01f) RequestMove(true);
                _dragging = false; _connectFrom = null; ev.Use();
            }
        }
        private void RequestMove(bool commit)
        {
            var delta = _ghost - _mouseStart; string id = SelectionId;
            if (SelectionKind == MapGraphSelectionKind.Node)
            { var position = _positionStart + delta; EditRequested?.Invoke(g => GridPlacement ? MapGraphGridPlacement.MoveNode(g, id, position, GridSpacing) : MapGraphEditOperations.MoveNode(g, id, position), "移动群", commit); }
            else if (SelectionKind == MapGraphSelectionKind.Zone)
            {
                var bounds = _resizing ? Rect.MinMaxRect(_zoneStart.xMin, _zoneStart.yMin + delta.y, _zoneStart.xMax + delta.x, _zoneStart.yMax) : new Rect(_zoneStart.position + delta, _zoneStart.size);
                EditRequested?.Invoke(g => GridPlacement ? MapGraphGridPlacement.MoveZone(g, id, bounds, GridSpacing, _resizing) : MapGraphEditOperations.MoveZone(g, id, bounds), _resizing ? "缩放区域" : "移动区域", commit);
            }
        }
        private void DrawGrid(Rect rect)
        {
            float spacing = MapGraphGridPlacement.ValidSpacing(GridSpacing);
            while (spacing * Zoom < 12) spacing *= 2; // 缩小时省略次级网格，限制绘制线数。
            var min = ToMap(new Vector2(0, rect.height), rect); var max = ToMap(new Vector2(rect.width, 0), rect);
            Handles.color = new Color(0.3f, 0.45f, 0.5f, 0.18f);
            for (float x = Mathf.Ceil(min.x / spacing) * spacing; x <= max.x; x += spacing)
                Handles.DrawLine(ToScreen(new Vector2(x, min.y), rect), ToScreen(new Vector2(x, max.y), rect));
            for (float y = Mathf.Ceil(min.y / spacing) * spacing; y <= max.y; y += spacing)
                Handles.DrawLine(ToScreen(new Vector2(min.x, y), rect), ToScreen(new Vector2(max.x, y), rect));
            if (_dragging && _connectFrom == null && SelectionKind == MapGraphSelectionKind.Node)
            {
                var point = MapGraphGridPlacement.Snap(_positionStart + _ghost - _mouseStart, GridSpacing);
                Handles.color = new Color(0.43f, 0.83f, 0.74f, 0.5f);
                Handles.DrawLine(ToScreen(new Vector2(point.x, min.y), rect), ToScreen(new Vector2(point.x, max.y), rect));
                Handles.DrawLine(ToScreen(new Vector2(min.x, point.y), rect), ToScreen(new Vector2(max.x, point.y), rect));
            }
        }
        private void EnsureStyles()
        {
            if (_zoneLabel != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/text-c.ttf");
            _zoneLabel = new GUIStyle(EditorStyles.label) { font = font, alignment = TextAnchor.MiddleCenter, fontSize = 15, clipping = TextClipping.Clip, normal = { textColor = new Color32(162, 185, 191, 255) } };
            _smallLabel = new GUIStyle(EditorStyles.label) { font = font, fontSize = 11, normal = { textColor = new Color32(129, 152, 161, 255) } };
        }
        private static Vector2 Port(Rect bounds, int i) => i == 0 ? new Vector2(bounds.xMin - 5, bounds.center.y) : i == 1 ? new Vector2(bounds.xMax + 5, bounds.center.y) : i == 2 ? new Vector2(bounds.center.x, bounds.yMin - 5) : new Vector2(bounds.center.x, bounds.yMax + 5);
        private static Rect Expanded(Rect r, float amount) => Rect.MinMaxRect(r.xMin - amount, r.yMin - amount, r.xMax + amount, r.yMax + amount);
        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        { var delta = b - a; return Vector2.Distance(p, a + delta * Mathf.Clamp01(Vector2.Dot(p - a, delta) / Mathf.Max(0.0001f, delta.sqrMagnitude))); }
        private static void Box(Rect r, Color fill, Color border, float width)
        {
            if (fill.a > 0) EditorGUI.DrawRect(r, fill);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, width), border); EditorGUI.DrawRect(new Rect(r.x, r.yMax - width, r.width, width), border);
            EditorGUI.DrawRect(new Rect(r.x, r.y, width, r.height), border); EditorGUI.DrawRect(new Rect(r.xMax - width, r.y, width, r.height), border);
        }
    }
}
