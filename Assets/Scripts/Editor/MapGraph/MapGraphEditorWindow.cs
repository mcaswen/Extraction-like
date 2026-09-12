using System;
using System.Linq;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>地图编辑会话的窗口编排。计算、交互及保存各自拥有独立组件。</summary>
    public sealed class MapGraphEditorWindow : EditorWindow
    {
        [SerializeField] private SO_MapGraphDefinition _source;
        [SerializeField] private string _recovery, _sourceBaseline, _scenePath;
        private MapGraphEditorDocument _document;
        private MapGraphEditorCanvas _canvas;
        private MapGraphEditorInspector _inspector;
        private bool _commitEdit, _autoValidate, _fit;
        private string _status = "从当前场景生成地图，或打开已有指挥图。";
        private double _lastRepaint;
        public MapGraphEditorDocument Document => _document;
        public MapGraphEditorCanvas Canvas => _canvas;
        public string Status => _status;

        [MenuItem("Tools/Anomaly Search/地图指挥编辑器")]
        public static MapGraphEditorWindow Open()
        {
            var window = GetWindow<MapGraphEditorWindow>(); window.Show(); window.Focus(); return window;
        }
        private void OnEnable()
        {
            titleContent = new GUIContent("地图指挥"); minSize = new Vector2(950, 650); wantsMouseMove = true;
            if (_source == null && string.IsNullOrEmpty(_recovery))
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                {
                    var binding = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).FirstOrDefault();
                    if (binding != null) _source = binding.MapDefinition;
                }
            }
            _document = new MapGraphEditorDocument(_source, _recovery, _sourceBaseline);
            _canvas = new MapGraphEditorCanvas(); _inspector = new MapGraphEditorInspector();
            _canvas.EditRequested += RequestEdit; _canvas.CancelRequested += Cancel;
            _document.Changed += OnDocumentChanged;
            if (string.IsNullOrEmpty(_scenePath)) _scenePath = SceneManager.GetActiveScene().path;
            if (_document.Layout != null) { _autoValidate = true; BeginGeneration(MapGraphGenerationMode.ValidateOnly); }
            OnDocumentChanged();
        }
        private void OnDisable()
        {
            if (_document == null) return;
            _source = _document.SourceDefinition;
            _recovery = _document.ExportWorkingCopy(); _sourceBaseline = _document.SourceBaseline;
            _document.Changed -= OnDocumentChanged; _document.Dispose(); _document = null;
            _inspector?.Dispose();
        }
        private void OnDocumentChanged()
        {
            hasUnsavedChanges = _document.IsDirty;
            saveChangesMessage = "地图工作副本尚未保存。";
            Repaint();
        }
        private Scene EditingScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == _scenePath) return active;
            return SceneManager.GetSceneByPath(_scenePath);
        }
        public void BeginGeneration(MapGraphGenerationMode mode = MapGraphGenerationMode.ConnectionsAndLayout)
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在 Edit Mode 编辑指挥图。");
                var scene = EditingScene(); if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("请先打开并保存待编辑的场景。");
                _document.BeginGeneration(scene, mode); _commitEdit = false; _status = "计算中，可随时取消。";
            }
            catch (Exception exception) { _status = exception.Message; }
        }
        public bool ApplyPreview()
        {
            var request = _document.PendingGeneration;
            if (!_document.TryApplyGeneration(request, out string failure)) { _status = failure; return false; }
            _status = "已应用预览。" + string.Join("；", request.Diagnostics.Where(d => !d.IsError).Select(d => d.ToString()).Distinct());
            _fit = true; return true;
        }
        public void RequestEdit(Func<MapGraphLayoutDraft, MapGraphLayoutDraft> edit, string label, bool commit)
        {
            try { _document.BeginEdit(edit, label); _commitEdit = commit; _status = "正在校验：" + label; }
            catch (Exception exception) { _status = exception.Message; }
        }
        public void Cancel()
        { _document.PendingGeneration?.Cancel(); _document.PendingEdit?.Cancel(); _commitEdit = false; _autoValidate = false; _status = "已取消，工作副本保持。"; Repaint(); }
        private void Update()
        {
            if (_document == null) return;
            var generation = _document.PendingGeneration;
            if (generation != null && generation.IsRunning)
            {
                generation.Advance();
                _status = $"{generation.Stage}  ·  导航 {generation.NavigationQueries}  ·  搜索 {generation.SearchStates}  ·  计算 {generation.ElapsedMilliseconds:F0} ms";
                if (!generation.IsRunning)
                {
                    if (generation.Result != null)
                    {
                        _fit = _document.Layout == null;
                        if (_autoValidate) { ApplyPreview(); _autoValidate = false; }
                        else _status = "预览就绪：" + EdgeDifference(generation.Result.Layout) + "。应用后可继续编辑。";
                    }
                    else _status = generation.Stage + "：" + string.Join("；", generation.Diagnostics.Select(d => d.ToString()));
                }
            }
            var edit = _document.PendingEdit;
            if (edit != null && !edit.IsCancelled)
            {
                if (!edit.IsComplete) edit.Advance();
                if (edit.IsComplete && _commitEdit)
                {
                    _commitEdit = false;
                    _status = _document.TryApplyEdit(edit, out string failure) ? "已应用：" + edit.Label : "编辑未应用：" + failure;
                }
            }
            if (EditorApplication.timeSinceStartup - _lastRepaint >= 0.05)
            { _lastRepaint = EditorApplication.timeSinceStartup; Repaint(); }
        }
        private string EdgeDifference(MapGraphLayoutDraft preview)
        {
            var old = _document.Layout;
            int added = preview.Edges.Count(e => old == null || !old.Graph.TryGetEdge(e.EdgeId, out var edge) || edge.FromNodeId != e.FromNodeId || edge.ToNodeId != e.ToNodeId);
            int removed = old?.Edges.Count(e => !preview.Graph.TryGetEdge(e.EdgeId, out var edge) || edge.FromNodeId != e.FromNodeId || edge.ToNodeId != e.ToNodeId) ?? 0;
            return $"新增 {added} 条，删除 {removed} 条连接";
        }
        public bool SaveTo(string newAssetPath = null)
        {
            if (_document.SourceDefinition == null && string.IsNullOrEmpty(newAssetPath))
                newAssetPath = EditorUtility.SaveFilePanelInProject("保存指挥地图", "MapGraph_Scene", "asset", "选择地图资产位置");
            if (_document.SourceDefinition == null && string.IsNullOrEmpty(newAssetPath)) return false;
            bool saved = MapGraphAuthoringTransaction.TrySave(_document, EditingScene(), newAssetPath, out _, out string failure);
            _status = saved ? "地图和场景绑定已保存。" : "保存失败：" + failure;
            if (saved) { _source = _document.SourceDefinition; _recovery = null; _sourceBaseline = _document.SourceBaseline; hasUnsavedChanges = false; }
            return saved;
        }
        public override void SaveChanges() { if (SaveTo()) base.SaveChanges(); }
        public override void DiscardChanges() { _recovery = null; base.DiscardChanges(); }
        private void OnGUI()
        {
            if (_document == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("生成路网与布局", EditorStyles.toolbarButton)) { _autoValidate = false; BeginGeneration(); }
                using (new EditorGUI.DisabledScope(_document.Layout == null))
                {
                    if (GUILayout.Button("仅重排", EditorStyles.toolbarButton)) { _autoValidate = false; BeginGeneration(MapGraphGenerationMode.LayoutOnly); }
                    if (GUILayout.Button("校验", EditorStyles.toolbarButton)) { _autoValidate = true; BeginGeneration(MapGraphGenerationMode.ValidateOnly); }
                }
                using (new EditorGUI.DisabledScope(_document.PendingGeneration?.Result == null))
                    if (GUILayout.Button("应用预览", EditorStyles.toolbarButton)) ApplyPreview();
                if (GUILayout.Button("取消", EditorStyles.toolbarButton)) Cancel();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("撤销", EditorStyles.toolbarButton)) Undo.PerformUndo();
                if (GUILayout.Button("重做", EditorStyles.toolbarButton)) Undo.PerformRedo();
                if (GUILayout.Button("全图", EditorStyles.toolbarButton)) _fit = true;
                using (new EditorGUI.DisabledScope(_document.Layout == null))
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton)) SaveTo();
            }
            var canvasRect = new Rect(0, 23, Mathf.Max(100, position.width - 310), position.height - 83);
            var preview = _document.PendingGeneration?.Result?.Layout ?? _document.PendingEdit?.Result;
            if (_fit) { _canvas.Fit(preview ?? _document.Layout, canvasRect); _fit = false; }
            bool editable = _document.LastVerifiedInput != null && _document.PendingGeneration?.Result == null && !(_document.PendingGeneration?.IsRunning ?? false) && !EditorApplication.isPlayingOrWillChangePlaymode;
            _canvas.Draw(canvasRect, _document.Layout, preview, editable);
            _inspector.Draw(new Rect(canvasRect.xMax + 4, 25, 302, position.height - 85), _document, _canvas, RequestEdit, editable);
            EditorGUI.HelpBox(new Rect(8, position.height - 55, position.width - 16, 48), _status, MessageType.None);
        }
    }
}
