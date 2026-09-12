using System;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>地图作者会话。工作副本参与 Unity Undo，计算请求通过单调版本隔离，原资产只读。</summary>
    public sealed class MapGraphEditorDocument : IDisposable
    {
        private SO_MapGraphDefinition _source;
        private bool _hasSource;
        private string _sourceBaseline;
        private string _workingBaseline;
        private Func<MapGraphSceneSnapshot> _pendingCapture, _verifiedCapture;
        private bool _disposed;
        public SO_MapGraphDefinition WorkingDefinition { get; private set; }
        public SO_MapGraphDefinition SourceDefinition => _source;
        public MapGraphLayoutDraft Layout => WorkingDefinition != null && WorkingDefinition.IsCommandGraph ? MapGraphLayoutDraft.FromDefinition(WorkingDefinition) : null;
        public MapGraphGenerationController PendingGeneration { get; private set; }
        public MapGraphGenerationResult LastVerifiedInput { get; private set; }
        public long Revision { get; private set; }
        public bool IsDirty => !_disposed && EditorJsonUtility.ToJson(WorkingDefinition) != _workingBaseline;
        public bool HasSourceConflict => _hasSource && (_source == null || EditorJsonUtility.ToJson(_source) != _sourceBaseline);
        public event Action Changed;

        public MapGraphEditorDocument(SO_MapGraphDefinition source = null)
        {
            if (source != null && !source.IsCommandGraph) throw new ArgumentException("请先显式转换旧图，不能在编辑时隐式迁移。", nameof(source));
            _source = source; _hasSource = source != null; _sourceBaseline = source != null ? EditorJsonUtility.ToJson(source) : "";
            WorkingDefinition = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
            WorkingDefinition.hideFlags = HideFlags.HideAndDontSave; WorkingDefinition.name = "Command map working copy";
            _workingBaseline = EditorJsonUtility.ToJson(WorkingDefinition);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public MapGraphGenerationController BeginGeneration(Scene scene, MapGraphGenerationMode mode = MapGraphGenerationMode.ConnectionsAndLayout)
            => BeginGeneration(() => MapGraphSceneCollector.Capture(scene), mode);

        internal MapGraphGenerationController BeginGeneration(Func<MapGraphSceneSnapshot> capture, MapGraphGenerationMode mode = MapGraphGenerationMode.ConnectionsAndLayout)
        {
            EnsureOpen(); PendingGeneration?.Cancel();
            _pendingCapture = capture;
            PendingGeneration = new MapGraphGenerationController(capture, WorkingDefinition.GenerationSettings, Layout, mode, Revision);
            return PendingGeneration;
        }

        public bool TryApplyGeneration(MapGraphGenerationController request, out string failure)
        {
            EnsureOpen(); failure = "";
            if (request == null || request != PendingGeneration || request.InputRevision != Revision)
            { failure = "GenerationRequestSuperseded"; return false; }
            if (!request.TryGetCurrentResult(out var result)) { failure = "GenerationResultUnavailable:" + request.Stage; return false; }
            MapGraphNavigationBakeData bake; MapGraphGenerationSettings settings;
            try
            {
                // 首版资产只保存一个 profile，按采集器稳定排序选取；完整矩阵仍保留在会话证据中。
                bake = MapGraphNavigationBakeBuilder.Build(result, WorkingDefinition.Revision + 1, result.Scene.Profiles[0].Data.ProfileId);
                settings = JsonUtility.FromJson<MapGraphGenerationSettings>(result.GenerationSettingsJson);
                if (settings == null) throw new InvalidOperationException("MissingGenerationSettings");
            }
            catch (Exception exception) { failure = exception.Message; return false; }
            LastVerifiedInput = result;
            _verifiedCapture = _pendingCapture;
            ApplyUndo("应用地图生成", () => WorkingDefinition.ApplyCommandData(WorkingDefinition.MapId, WorkingDefinition.DisplayName, result.Layout.StartNodeId,
                result.Layout.Zones, result.Layout.Nodes, result.Layout.Edges, result.Layout.Constraints, bake, settings));
            return true;
        }

        public bool TryVerifyForSave(out MapGraphGenerationResult input, out string failure)
        {
            EnsureOpen(); input = null; failure = "";
            if (Layout == null || LastVerifiedInput == null || _verifiedCapture == null) { failure = "MapNeedsValidation"; return false; }
            if (PendingGeneration != null && PendingGeneration.IsRunning) { failure = "GenerationStillRunning"; return false; }
            if (HasSourceConflict) { failure = "SourceAssetChanged"; return false; }
            try
            {
                Physics.SyncTransforms(); var current = _verifiedCapture(); var verified = LastVerifiedInput.Scene;
                if (current == null || !current.IsValid || current.SceneGuid != verified.SceneGuid || current.ScenePath != verified.ScenePath ||
                    current.SceneFingerprint != verified.SceneFingerprint || current.NavigationFingerprint != verified.NavigationFingerprint ||
                    WorkingDefinition.NavigationBake.SceneFingerprint != verified.SceneFingerprint || WorkingDefinition.NavigationBake.NavigationFingerprint != verified.NavigationFingerprint)
                { failure = "GenerationInputsChanged"; return false; }
                input = LastVerifiedInput; return true;
            }
            catch (Exception exception) { failure = "GenerationInputUnavailable:" + exception.Message; return false; }
        }

        internal void MarkSaved(SO_MapGraphDefinition source)
        {
            EnsureOpen(); _source = source; _hasSource = true; _sourceBaseline = EditorJsonUtility.ToJson(source);
            _workingBaseline = EditorJsonUtility.ToJson(WorkingDefinition); Changed?.Invoke();
        }

        public void SetGenerationSettings(MapGraphGenerationSettings settings)
        {
            EnsureOpen(); if (settings == null) throw new ArgumentNullException(nameof(settings));
            string json = "{\"_generationSettings\":" + JsonUtility.ToJson(settings) + "}";
            ApplyUndo("调整地图生成参数", () => JsonUtility.FromJsonOverwrite(json, WorkingDefinition));
        }

        public void SetDisplayName(string displayName)
        {
            EnsureOpen(); if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("地图名称不能为空。", nameof(displayName));
            // 只更新既有 SO 字段，保持图、成本和图标资源不变；不添加第二份地图元数据。
            using var serialized = new SerializedObject(WorkingDefinition);
            serialized.FindProperty("_displayName").stringValue = displayName.Trim();
            ApplyUndo("修改地图名称", () => serialized.ApplyModifiedPropertiesWithoutUndo());
        }

        private void ApplyUndo(string label, Action mutation)
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(WorkingDefinition, label);
            try
            {
                mutation(); WorkingDefinition.OnAfterDeserialize(); EditorUtility.SetDirty(WorkingDefinition);
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            AdvanceRevision();
        }

        private void OnUndoRedo()
        {
            if (_disposed || WorkingDefinition == null) return;
            WorkingDefinition.OnAfterDeserialize(); AdvanceRevision();
        }
        private void AdvanceRevision()
        {
            Revision++; PendingGeneration?.Cancel(); PendingGeneration = null;
            Changed?.Invoke();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Undo.undoRedoPerformed -= OnUndoRedo; PendingGeneration?.Cancel(); PendingGeneration = null; LastVerifiedInput = null;
            if (WorkingDefinition != null) { Undo.ClearUndo(WorkingDefinition); UnityEngine.Object.DestroyImmediate(WorkingDefinition); WorkingDefinition = null; }
            Changed = null;
        }
        private void EnsureOpen() { if (_disposed) throw new ObjectDisposedException(nameof(MapGraphEditorDocument)); }
    }
}
