using System;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>作者属性控件；全部编辑通过文档操作，不自行写场景或求解。</summary>
    public sealed class MapGraphEditorInspector : IDisposable
    {
        private Vector2 _scroll;
        private bool _settings, _resetArmed;

        private SO_MapGraphDefinition _settingsCopy;
        private long _settingsRevision = -1;
        public void Draw(Rect rect, MapGraphEditorDocument document, MapGraphEditorCanvas canvas,
            Action<Func<MapGraphLayoutDraft, MapGraphLayoutDraft>, string, bool> edit, bool editable)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("地图指挥", EditorStyles.boldLabel);
            var name = EditorGUILayout.DelayedTextField("地图名称", document.WorkingDefinition.DisplayName);
            if (!string.IsNullOrWhiteSpace(name) && name != document.WorkingDefinition.DisplayName) document.SetDisplayName(name);
            var layout = document.AuthoringLayout;
            if (layout != null)
            {
                EditorGUILayout.LabelField($"{layout.Zones.Count} 个区域  ·  {layout.Nodes.Count} 个群  ·  {layout.Edges.Count} 条连接", EditorStyles.miniLabel);
                int attention = layout.Edges.Count(e => MapGraphEditorEdgePresentation.Resolve(layout, e).RequiresAttention);
                if (attention > 0) EditorGUILayout.HelpBox($"{attention} 条连接的显示待修正。警示虚线可直接选中；无法显示时，Shift 选两端可查看原连接。", MessageType.Warning);
                float spacing = EditorGUILayout.DelayedFloatField("网格间距", document.Placement.GridSpacing);
                int cells = EditorGUILayout.IntSlider("微调最多格数", document.Placement.AdjustmentCells, 1, 4);
                if (MapGraphGeometry.Finite(spacing) && spacing >= 1 && spacing <= 10000) document.SetPlacementSettings(spacing, cells);
                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    string id = canvas.SelectionId;
                    if (canvas.SelectionKind == MapGraphSelectionKind.Node && layout.Graph.TryGetNode(id, out var node))
                    {
                        EditorGUILayout.LabelField(node.DisplayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(node.NodeKind.ToString(), EditorStyles.miniLabel);
                        var p = layout.Graph.GetNodePosition(id); var moved = EditorGUILayout.Vector2Field("全图位置", p);
                        if (moved != p) edit(g => MapGraphGridPlacement.MoveNode(g, id, moved, document.Placement.GridSpacing), "调整群位置", true);
                        bool locked = EditorGUILayout.Toggle("锁定位置", node.PositionLocked);
                        if (locked != node.PositionLocked) edit(g => MapGraphEditOperations.LockNode(g, id, locked), "锁定群位置", true);
                        AlignmentLock(node.RowId, "锁定整行", layout, edit);
                        AlignmentLock(node.ColumnId, "锁定整列", layout, edit);

                    }
                    else if (canvas.SelectionKind == MapGraphSelectionKind.Zone && layout.Graph.TryGetZone(id, out var zone))
                    {
                        EditorGUILayout.LabelField(zone.DisplayName, EditorStyles.boldLabel);
                        var bounds = EditorGUILayout.RectField("区域矩形", zone.Bounds);
                        if (bounds != zone.Bounds) edit(g => MapGraphGridPlacement.MoveZone(g, id, bounds, document.Placement.GridSpacing, bounds.size != zone.Bounds.size), "调整区域矩形", true);
                        bool locked = EditorGUILayout.Toggle("锁定矩形", zone.LayoutLocked);
                        if (locked != zone.LayoutLocked) edit(g => MapGraphEditOperations.LockZone(g, id, locked), "锁定区域", true);
                        EditorGUILayout.HelpBox("移动区域会携带成员；拖动右下角调整矩形大小。", MessageType.None);
                    }
                    else if (canvas.SelectionKind == MapGraphSelectionKind.Edge && layout.Graph.TryGetEdge(id, out var edge))
                    {
                        EditorGUILayout.LabelField("群间连接", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("起点", layout.Graph.TryGetNode(edge.FromNodeId, out var from) ? from.DisplayName : edge.FromNodeId);
                        EditorGUILayout.LabelField("终点", layout.Graph.TryGetNode(edge.ToNodeId, out var to) ? to.DisplayName : edge.ToNodeId);
                        EditorGUILayout.LabelField("方向", edge.Axis == MapGraphAxis.Horizontal ? "水平" : "垂直");
                        var presentation = MapGraphEditorEdgePresentation.Resolve(layout, edge);
                        if (presentation.RequiresAttention) EditorGUILayout.HelpBox(presentation.Message, MessageType.Warning);
                        if (presentation.CanRepairDirection && GUILayout.Button("按当前对齐修正方向"))
                            edit(g => MapGraphEditOperations.AlignEdgeToNodes(g, id), "按当前摆放修正连接方向", true);
                        if (GUILayout.Button("重选端点")) canvas.ConnectionSelection.BeginRebind(layout, id, document.Revision);
                        EditorGUI.BeginChangeCheck();
                        float insetA = EditorGUILayout.DelayedFloatField("起点留白", edge.FromInset);
                        float insetB = EditorGUILayout.DelayedFloatField("终点留白", edge.ToInset);
                        float width = EditorGUILayout.DelayedFloatField("线宽（0 默认）", edge.WidthOverride);
                        bool custom = EditorGUILayout.Toggle("自定义颜色", edge.UseColorOverride);
                        var color = EditorGUILayout.ColorField("线条颜色", edge.ColorOverride);
                        if (EditorGUI.EndChangeCheck()) edit(g => MapGraphEditOperations.StyleEdge(g, id, insetA, insetB, width, custom, color), "调整连接样式", true);
                        if (GUILayout.Button("删除连接")) edit(g => MapGraphEditOperations.DeleteEdge(g, id), "删除连接", true);
                    }
                    else EditorGUILayout.HelpBox("选择群、区域或连线编辑属性。", MessageType.None);
                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField(canvas.ConnectionSelection.RebindEdgeId.Length > 0 ? "重绑连接 · 画布选点" : "Shift 选点连线", EditorStyles.boldLabel);
                    string Label(string nodeId) => layout.Graph.TryGetNode(nodeId, out var selected) ? selected.DisplayName : "尚未选择";
                    EditorGUILayout.LabelField("起点", Label(canvas.ConnectionSelection.FromNodeId));
                    EditorGUILayout.LabelField("终点", Label(canvas.ConnectionSelection.ToNodeId));
                    EditorGUILayout.HelpBox("先点起点，再按 Shift 点终点。两群必须同行或同列；第三次 Shift 替换终点，Esc 取消。", MessageType.None);
                    if (!string.IsNullOrEmpty(canvas.ConnectionSelection.Failure)) EditorGUILayout.HelpBox(canvas.ConnectionSelection.Failure, MessageType.Warning);
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField($"人工禁连：{layout.Constraints.ExcludedConnections.Count} 对", EditorStyles.miniLabel);
                    _resetArmed = EditorGUILayout.ToggleLeft("解除全部人工锁、禁连和样式", _resetArmed);
                    using (new EditorGUI.DisabledScope(!_resetArmed))
                        if (GUILayout.Button("重置人工覆写（可撤销）"))
                        { edit(MapGraphEditOperations.ResetOverrides, "重置人工覆写", true); _resetArmed = false; }
                }
            }
            EditorGUILayout.Space(12);
            _settings = EditorGUILayout.Foldout(_settings, "生成参数", true);
            if (_settings)
            {
                // 编辑独立临时设置副本；只在字段变化时作为文档操作提交。
                if (_settingsCopy == null || _settingsRevision != document.Revision)
                {
                    Dispose(); _settingsCopy = UnityEngine.Object.Instantiate(document.WorkingDefinition);
                    _settingsCopy.hideFlags = HideFlags.HideAndDontSave; _settingsRevision = document.Revision;
                }
                using var serialized = new SerializedObject(_settingsCopy);
                var property = serialized.FindProperty("_generationSettings");
                EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(property, true);
                if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedPropertiesWithoutUndo(); document.SetGenerationSettings(_settingsCopy.GenerationSettings); }
            }
            EditorGUILayout.EndScrollView(); GUILayout.EndArea();
        }
        public void Dispose() { if (_settingsCopy != null) UnityEngine.Object.DestroyImmediate(_settingsCopy); _settingsCopy = null; }
        private static void AlignmentLock(string id, string label, MapGraphLayoutDraft layout, Action<Func<MapGraphLayoutDraft, MapGraphLayoutDraft>, string, bool> edit)
        {
            var line = layout.Constraints.Alignments.FirstOrDefault(a => a.Id == id); if (line == null) return;
            bool locked = EditorGUILayout.Toggle(label, line.Locked);
            if (locked != line.Locked) edit(g => MapGraphEditOperations.LockAlignment(g, id, locked), label, true);
        }
    }
}
