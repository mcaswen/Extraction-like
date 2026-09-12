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
        public float Zoom { get; private set; } = 1;
        public Vector2 Center { get; private set; }
        public MapGraphSelectionKind SelectionKind { get; private set; }
        public string SelectionId { get; private set; } = "";
        public Rect ViewRect { get; private set; }
        public int PaintCount { get; private set; }
        public event Action<Func<MapGraphLayoutDraft, MapGraphLayoutDraft>, string, bool> EditRequested;
        public event Action CancelRequested;

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
                if (MapGraphGeometry.TryGetVisibleSegment(layout, edge, out var a, out var b) &&
                    DistanceToSegment(screen, ToScreen(a, rect), ToScreen(b, rect)) < 6) return (MapGraphSelectionKind.Edge, edge.EdgeId);
            foreach (var zone in layout.Zones)
                if (ToScreen(zone.Bounds, rect).Contains(screen)) return (MapGraphSelectionKind.Zone, zone.ZoneId);
            return (MapGraphSelectionKind.None, "");
        }
        public void Draw(Rect rect, MapGraphLayoutDraft layout, MapGraphLayoutDraft preview, bool editable)
        {
            ViewRect = rect; EnsureStyles();
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
                    _zoneLabel.fontSize = Mathf.Clamp(Mathf.RoundToInt(21 * Zoom), 10, 25);
                    GUI.Label(nameRect, zone.DisplayName, _zoneLabel);
                    if (SelectionKind == MapGraphSelectionKind.Zone && SelectionId == zone.ZoneId)
                        EditorGUI.DrawRect(new Rect(bounds.xMax - 7, bounds.yMax - 7, 7, 7), Accent);
                }
                Handles.BeginGUI();
                foreach (var edge in visible.Edges)
                    if (MapGraphGeometry.TryGetVisibleSegment(visible, edge, out var a, out var b))
                    {
                        Handles.color = SelectionKind == MapGraphSelectionKind.Edge && SelectionId == edge.EdgeId ? Accent : edge.UseColorOverride ? edge.ColorOverride : new Color32(86, 115, 123, 255);
                        Handles.DrawAAPolyLine(Mathf.Max(1, (edge.WidthOverride > 0 ? edge.WidthOverride : 2) * Zoom), ToScreen(a, local), ToScreen(b, local));
                    }
                foreach (var node in visible.Nodes) DrawNode(visible, node, local);
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
                        Box(new Rect(ToScreen(_ghost, local) - Vector2.one * 10, Vector2.one * 20), Color.clear, Accent, 1);
                }
                Handles.EndGUI();
                if (layout != null) HandleInput(layout, local, editable);
            }
            GUI.Label(new Rect(15, local.height - 26, local.width - 30, 20), "滚轮缩放  ·  中键平移  ·  拖动群 / 区域  ·  从群端口拖线", _smallLabel);
            GUI.EndGroup();
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
            if (selected)
                for (int i = 0; i < 4; i++) EditorGUI.DrawRect(new Rect(Port(bounds, i) - Vector2.one * 3, Vector2.one * 6), Accent);
            if (bounds.Contains(Event.current.mousePosition)) GUI.Label(new Rect(p.x + 18, p.y - 24, 300, 22), node.DisplayName, _smallLabel);
        }
        private void HandleInput(MapGraphLayoutDraft layout, Rect rect, bool editable)
        {
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && rect.Contains(ev.mousePosition)) { ZoomAt(ev.mousePosition, rect, Mathf.Exp(-ev.delta.y * 0.08f)); ev.Use(); }
            if (ev.type == EventType.MouseDown && ev.button == 2 && rect.Contains(ev.mousePosition)) { _panning = true; ev.Use(); }
            if (_panning && ev.type == EventType.MouseDrag) { Center += new Vector2(-ev.delta.x, ev.delta.y) / Zoom; ev.Use(); }
            if (_panning && ev.type == EventType.MouseUp) { _panning = false; ev.Use(); }
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { _dragging = false; _connectFrom = null; CancelRequested?.Invoke(); ev.Use(); }
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Delete && editable && SelectionKind == MapGraphSelectionKind.Edge)
            { string id = SelectionId; EditRequested?.Invoke(g => MapGraphEditOperations.DeleteEdge(g, id), "删除连接", true); ev.Use(); }
            if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
            {
                _connectFrom = null;
                if (editable && SelectionKind == MapGraphSelectionKind.Node && layout.Graph.TryGetNode(SelectionId, out var selected))
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
                if (_connectFrom == null && EditorApplication.timeSinceStartup - _lastPreview > 0.15)
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
            { var position = _positionStart + delta; EditRequested?.Invoke(g => MapGraphEditOperations.MoveNode(g, id, position), "移动群", commit); }
            else if (SelectionKind == MapGraphSelectionKind.Zone)
            {
                var bounds = _resizing ? Rect.MinMaxRect(_zoneStart.xMin, _zoneStart.yMin + delta.y, _zoneStart.xMax + delta.x, _zoneStart.yMax) : new Rect(_zoneStart.position + delta, _zoneStart.size);
                EditRequested?.Invoke(g => MapGraphEditOperations.MoveZone(g, id, bounds), _resizing ? "缩放区域" : "移动区域", commit);
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
