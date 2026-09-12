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
        private string _selected;
        private int _from, _to = 1, _alignment;
        private MapGraphAxis _axis = MapGraphAxis.Horizontal;
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
            var layout = document.Layout;
            if (layout != null)
            {
                EditorGUILayout.LabelField($"{layout.Zones.Count} 个区域  ·  {layout.Nodes.Count} 个群  ·  {layout.Edges.Count} 条连接", EditorStyles.miniLabel);
                EditorGUILayout.Space(8);
                using (new EditorGUI.DisabledScope(!editable))
                {
                    string id = canvas.SelectionId;
                    if (canvas.SelectionKind == MapGraphSelectionKind.Node && layout.Graph.TryGetNode(id, out var node))
                    {
                        EditorGUILayout.LabelField(node.DisplayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(node.NodeKind.ToString(), EditorStyles.miniLabel);
                        var p = layout.Graph.GetNodePosition(id); var moved = EditorGUILayout.Vector2Field("全图位置", p);
                        if (moved != p) edit(g => MapGraphEditOperations.MoveNode(g, id, moved), "调整群位置", true);
                        bool locked = EditorGUILayout.Toggle("锁定位置", node.PositionLocked);
                        if (locked != node.PositionLocked) edit(g => MapGraphEditOperations.LockNode(g, id, locked), "锁定群位置", true);
                        AlignmentLock(node.RowId, "锁定整行", layout, edit);
                        AlignmentLock(node.ColumnId, "锁定整列", layout, edit);
                        _alignment = NodePopup("对齐到", layout, _alignment);
                        if (layout.Nodes.Count > 0)
                        {
                            var target = layout.Graph.GetNodePosition(layout.Nodes[_alignment].NodeId);
                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button("同一行")) edit(g => MapGraphEditOperations.MoveNode(g, id, new Vector2(p.x, target.y)), "对齐群行", true);
                            if (GUILayout.Button("同一列")) edit(g => MapGraphEditOperations.MoveNode(g, id, new Vector2(target.x, p.y)), "对齐群列", true);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    else if (canvas.SelectionKind == MapGraphSelectionKind.Zone && layout.Graph.TryGetZone(id, out var zone))
                    {
                        EditorGUILayout.LabelField(zone.DisplayName, EditorStyles.boldLabel);
                        var bounds = EditorGUILayout.RectField("区域矩形", zone.Bounds);
                        if (bounds != zone.Bounds) edit(g => MapGraphEditOperations.MoveZone(g, id, bounds), "调整区域矩形", true);
                        bool locked = EditorGUILayout.Toggle("锁定矩形", zone.LayoutLocked);
                        if (locked != zone.LayoutLocked) edit(g => MapGraphEditOperations.LockZone(g, id, locked), "锁定区域", true);
                        EditorGUILayout.HelpBox("移动区域会携带成员；拖动右下角调整矩形大小。", MessageType.None);
                    }
                    else if (canvas.SelectionKind == MapGraphSelectionKind.Edge && layout.Graph.TryGetEdge(id, out var edge))
                    {
                        EditorGUILayout.LabelField("群间连接", EditorStyles.boldLabel);
                        if (_selected != id)
                        {
                            _from = Index(layout, edge.FromNodeId); _to = Index(layout, edge.ToNodeId); _axis = edge.Axis;
                        }
                        ConnectionFields(layout);
                        if (GUILayout.Button("应用端点 / 方向"))
                        { string from = layout.Nodes[_from].NodeId, to = layout.Nodes[_to].NodeId; var axis = _axis; edit(g => MapGraphEditOperations.RebindEdge(g, id, from, to, axis), "重绑连接端点", true); }
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
                    _selected = id;
                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField("添加连接", EditorStyles.boldLabel);
                    if (layout.Nodes.Count >= 2)
                    {
                        ConnectionFields(layout);
                        if (GUILayout.Button("连接两个群"))
                        { string from = layout.Nodes[_from].NodeId, to = layout.Nodes[_to].NodeId; var axis = _axis; edit(g => MapGraphEditOperations.AddEdge(g, from, to, axis), "添加连接", true); }
                    }
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
        private void ConnectionFields(MapGraphLayoutDraft layout)
        { _from = NodePopup("起点群", layout, _from); _to = NodePopup("终点群", layout, _to); _axis = EditorGUILayout.Popup("直线方向", _axis == MapGraphAxis.Vertical ? 1 : 0, new[] { "水平", "垂直" }) == 0 ? MapGraphAxis.Horizontal : MapGraphAxis.Vertical; }
        private static int NodePopup(string label, MapGraphLayoutDraft layout, int value)
            => EditorGUILayout.Popup(label, Mathf.Clamp(value, 0, layout.Nodes.Count - 1), layout.Nodes.Select(n => n.DisplayName).ToArray());
        private static int Index(MapGraphLayoutDraft layout, string id)
        { for (int i = 0; i < layout.Nodes.Count; i++) if (layout.Nodes[i].NodeId == id) return i; return 0; }
        private static void AlignmentLock(string id, string label, MapGraphLayoutDraft layout, Action<Func<MapGraphLayoutDraft, MapGraphLayoutDraft>, string, bool> edit)
        {
            var line = layout.Constraints.Alignments.FirstOrDefault(a => a.Id == id); if (line == null) return;
            bool locked = EditorGUILayout.Toggle(label, line.Locked);
            if (locked != line.Locked) edit(g => MapGraphEditOperations.LockAlignment(g, id, locked), label, true);
        }
    }
}
