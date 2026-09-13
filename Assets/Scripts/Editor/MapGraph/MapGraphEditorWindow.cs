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
        [SerializeField] private string _recovery, _sourceBaseline, _scenePath, _placementRecovery;
        private MapGraphEditorDocument _document;
        private MapGraphEditorCanvas _canvas;
        private MapGraphEditorInspector _inspector;
        private bool _fit, _refreshEvidence, _connectAfterRefresh;
        private string _status = "从当前场景生成地图，或打开已有指挥图。";
        private double _lastRepaint;
        private bool _discarded;
        [Serializable] private sealed class WindowRecovery
        { public string sourceGuid, working, baseline, placement; }
        private string RecoveryKey => "AnomalySearch.MapGridRecovery:" + Application.dataPath + ":" + _scenePath;
        public MapGraphEditorDocument Document => _document;
        public MapGraphEditorCanvas Canvas => _canvas;
        public string Status => _status;
        public bool CanEditPlacement => _document?.AuthoringLayout != null && !EditorApplication.isPlayingOrWillChangePlaymode &&
            (_refreshEvidence && !_connectAfterRefresh || _document.PendingGeneration?.Result == null && _document.PendingGeneration?.IsRunning != true);

        [MenuItem("Tools/Anomaly Search/地图指挥编辑器")]
        public static MapGraphEditorWindow Open()
        {
            var window = GetWindow<MapGraphEditorWindow>(); window.Show(); window.Focus(); return window;
        }
        private void OnEnable()
        {
            _discarded = false;
            if (string.IsNullOrEmpty(_scenePath)) _scenePath = SceneManager.GetActiveScene().path;
            if (_source == null && string.IsNullOrEmpty(_recovery))
            {
                var saved = SessionState.GetString(RecoveryKey, "");
                if (!string.IsNullOrEmpty(saved))
                {
                    var recovery = JsonUtility.FromJson<WindowRecovery>(saved);
                    _source = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetDatabase.GUIDToAssetPath(recovery.sourceGuid));
                    _recovery = recovery.working; _sourceBaseline = recovery.baseline; _placementRecovery = recovery.placement;
                }
            }
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
            _document = new MapGraphEditorDocument(_source, _recovery, _sourceBaseline, _placementRecovery);
            _canvas = new MapGraphEditorCanvas { GridPlacement = true }; _inspector = new MapGraphEditorInspector();
            _canvas.EditRequested += RequestEdit; _canvas.CancelRequested += Cancel; _canvas.ConnectionRequested += RequestConnection;
            _document.Changed += OnDocumentChanged;
            if (string.IsNullOrEmpty(_scenePath)) _scenePath = SceneManager.GetActiveScene().path;
            _fit = true;
            if (_document.Layout != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                try { _document.BeginNavigationRefresh(EditingScene()); _refreshEvidence = true; }
                catch (Exception exception) { _status = exception.Message; }
            }
            OnDocumentChanged();
        }
        private void OnDisable()
        {
            if (_document == null) return;
            if (!_discarded) RememberDraft();
            _document.Changed -= OnDocumentChanged; _document.Dispose(); _document = null;
            _inspector?.Dispose();
        }
        private void RememberDraft()
        {
            _source = _document.SourceDefinition; _sourceBaseline = _document.SourceBaseline;
            _placementRecovery = _document.ExportPlacement(); _recovery = _document.ExportWorkingCopy();
            if (_document.IsDirty)
                SessionState.SetString(RecoveryKey, JsonUtility.ToJson(new WindowRecovery
                { sourceGuid = _source != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_source)) : "",
                    working = _recovery, baseline = _sourceBaseline, placement = _placementRecovery }));
            else SessionState.EraseString(RecoveryKey);
        }
        public void KeepDraftAndClose()
        { RememberDraft(); hasUnsavedChanges = false; Close(); }

        private void OnDocumentChanged()
        {
            hasUnsavedChanges = _document.IsDirty;
            _canvas?.ConnectionSelection.Synchronize(_document.AuthoringLayout, _document.Revision);
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
                _refreshEvidence = _connectAfterRefresh = false;
                _document.BeginGeneration(scene, mode); _status = "计算中，可随时取消。";
            }
            catch (Exception exception) { _status = exception.Message; }
        }
        public bool ApplyPreview()
        {
            var request = _document.PendingGeneration;
            if (!_document.TryApplyGeneration(request, out string failure)) { _status = failure; return false; }
            _status = "已应用预览。" + string.Join("；", request.Diagnostics.Where(d => !d.IsError).Select(d => d.ToString()).Distinct());
            return true;
        }
        public void RequestEdit(Func<MapGraphLayoutDraft, MapGraphLayoutDraft> edit, string label, bool commit)
        {
            try
            {
                if (commit) { _document.EditPlacement(edit, label); _refreshEvidence = _connectAfterRefresh = false; }
                _status = "摆放草稿：" + label + "，请生成或校验线路后应用预览。";
            }
            catch (Exception exception) { _status = exception.Message; }
        }
        public void RequestConnection() => SubmitConnection(true);
        private void SubmitConnection(bool allowRefresh)
        {
            var selection = _canvas.ConnectionSelection;
            selection.Synchronize(_document.AuthoringLayout, _document.Revision);
            if (!selection.TryGetAxis(_document.AuthoringLayout, out var axis)) { _status = selection.Failure; Repaint(); return; }
            if (_document.TryConnectPlacement(selection.FromNodeId, selection.ToNodeId, axis, selection.RebindEdgeId, out string edgeId, out string failure))
            {
                selection.Cancel(); _canvas.Select(MapGraphSelectionKind.Edge, edgeId);
                _status = "连接已加入草稿，可调整样式；校验线路并应用预览后发布。";
            }
            else if (allowRefresh && (failure == "MapNeedsValidation" || failure == "GenerationInputsChanged"))
            {
                try { _document.BeginNavigationRefresh(EditingScene()); _refreshEvidence = _connectAfterRefresh = true; _status = "正在核对导航，完成后连接选中的两个群。"; }
                catch (Exception exception) { selection.Reject(exception.Message); _status = exception.Message; }
            }
            else { selection.Reject(failure); _status = "连线未应用：" + failure; }
            Repaint();
        }
        public void Cancel()
        { _document.PendingGeneration?.Cancel(); _document.PendingEdit?.Cancel(); _refreshEvidence = _connectAfterRefresh = false; _canvas.ConnectionSelection.Cancel(); _status = "已取消，工作副本保持。"; Repaint(); }
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
                        if (_refreshEvidence)
                        {
                            _refreshEvidence = false; bool connect = _connectAfterRefresh; _connectAfterRefresh = false;
                            if (_document.TryRefreshNavigationEvidence(generation, out string failure))
                            { _status = "可以网格摆放，Shift 选择两个群连线。"; if (connect) SubmitConnection(false); }
                            else _status = failure;
                        }
                        else _status = "预览就绪：" + EdgeDifference(generation.Result.Layout) + "。检查后应用预览。";
                    }
                    else _status = generation.Stage + "：" + string.Join("；", generation.Diagnostics.Select(d => d.ToString()));
                }
            }
            if (EditorApplication.timeSinceStartup - _lastRepaint >= 0.05)
            { _lastRepaint = EditorApplication.timeSinceStartup; Repaint(); }
        }
        private string EdgeDifference(MapGraphLayoutDraft preview)
        {
            var old = _document.AuthoringLayout;
            int added = preview.Edges.Count(e => old == null || !old.Graph.TryGetEdge(e.EdgeId, out var edge) || edge.FromNodeId != e.FromNodeId || edge.ToNodeId != e.ToNodeId);
            int removed = old?.Edges.Count(e => !preview.Graph.TryGetEdge(e.EdgeId, out var edge) || edge.FromNodeId != e.FromNodeId || edge.ToNodeId != e.ToNodeId) ?? 0;
            int moved = preview.Nodes.Count(n => old != null && old.Graph.TryGetNode(n.NodeId, out _) && !MapGraphGeometry.Near(old.Graph.GetNodePosition(n.NodeId), preview.Graph.GetNodePosition(n.NodeId)));
            return $"移动 {moved} 个群，新增 {added} 条，删除 {removed} 条连接";
        }
        public bool SaveTo(string newAssetPath = null)
        {
            if (_document.SourceDefinition == null && string.IsNullOrEmpty(newAssetPath))
                newAssetPath = EditorUtility.SaveFilePanelInProject("保存指挥地图", "MapGraph_Scene", "asset", "选择地图资产位置");
            if (_document.SourceDefinition == null && string.IsNullOrEmpty(newAssetPath)) return false;
            bool saved = MapGraphAuthoringTransaction.TrySave(_document, EditingScene(), newAssetPath, out _, out string failure);
            _status = saved ? "地图和场景绑定已保存。" : "保存失败：" + failure;
            if (saved) { SessionState.EraseString(RecoveryKey); _source = _document.SourceDefinition; _recovery = _placementRecovery = null; _sourceBaseline = _document.SourceBaseline; hasUnsavedChanges = false; }
            return saved;
        }
        public override void SaveChanges() { if (SaveTo()) base.SaveChanges(); }
        public override void DiscardChanges()
        { _discarded = true; _recovery = _placementRecovery = _sourceBaseline = null; SessionState.EraseString(RecoveryKey); base.DiscardChanges(); }
        private void OnGUI()
        {
            if (_document == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (_document.AuthoringLayout == null)
                { if (GUILayout.Button("从场景创建地图", EditorStyles.toolbarButton)) BeginGeneration(); }
                else
                {
                    if (GUILayout.Button("1  网格摆放", EditorStyles.toolbarButton)) Cancel();
                    if (GUILayout.Button("2  生成连接", EditorStyles.toolbarButton)) BeginGeneration(MapGraphGenerationMode.PlacementConnections);
                    if (GUILayout.Button("微调建议", EditorStyles.toolbarButton)) BeginGeneration(MapGraphGenerationMode.PlacementAdjustment);
                    if (GUILayout.Button("校验线路", EditorStyles.toolbarButton)) BeginGeneration(MapGraphGenerationMode.ValidateOnly);
                }
                using (new EditorGUI.DisabledScope(_refreshEvidence || _document.PendingGeneration?.Result == null))
                    if (GUILayout.Button("应用预览", EditorStyles.toolbarButton)) ApplyPreview();
                if (GUILayout.Button("取消", EditorStyles.toolbarButton)) Cancel();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("撤销", EditorStyles.toolbarButton)) Undo.PerformUndo();
                if (GUILayout.Button("重做", EditorStyles.toolbarButton)) Undo.PerformRedo();
                if (GUILayout.Button("全图", EditorStyles.toolbarButton)) _fit = true;
                if (GUILayout.Button("暂存关闭", EditorStyles.toolbarButton)) { KeepDraftAndClose(); GUIUtility.ExitGUI(); }
                using (new EditorGUI.DisabledScope(_document.Layout == null || _document.HasPlacementDraft || _document.PendingGeneration?.IsRunning == true || _document.PendingGeneration?.Result != null))
                    if (GUILayout.Button("3  发布地图", EditorStyles.toolbarButton)) SaveTo();
            }
            EditorGUI.LabelField(new Rect(10, 24, position.width - 20, 20), "工程：" + Application.dataPath.Replace("/Assets", "") + "   |   场景：" + _scenePath, EditorStyles.miniLabel);
            var canvasRect = new Rect(0, 48, Mathf.Max(100, position.width - 310), position.height - 138);
            var preview = _refreshEvidence ? null : _document.PendingGeneration?.Result?.Layout ?? _document.PendingEdit?.Result;
            if (_fit) { _canvas.Fit(preview ?? _document.AuthoringLayout, canvasRect); _fit = false; }
            bool editable = CanEditPlacement;
            _canvas.GridSpacing = _document.Placement.GridSpacing; _canvas.DocumentRevision = _document.Revision;
            _canvas.Draw(canvasRect, _document.AuthoringLayout, preview, editable);
            _inspector.Draw(new Rect(canvasRect.xMax + 4, 48, 302, position.height - 138), _document, _canvas, RequestEdit, editable);
            EditorGUI.HelpBox(new Rect(8, position.height - 83, position.width - 16, 76), (_document.HasPlacementDraft ? "摆放草稿 · 待生成/校验线路\n" : _document.LastVerifiedInput == null ? "工作图 · 本会话待校验\n" : "已验证工作图\n") + _status, MessageType.None);
        }
    }
}
