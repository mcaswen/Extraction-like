using System;
using System.IO;
using System.Linq;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Editor.MapGraph
{
    internal enum MapGraphSaveCheckpoint { AssetSaved, SceneSaved }
    /// <summary>验证后的正式配置事务。只写目标图和对应场景，生成算法不参与持久化。</summary>
    public static class MapGraphAuthoringTransaction
    {
        public static bool TrySave(MapGraphEditorDocument document, Scene scene, string newAssetPath,
            out MapGraphBindingAuthoring binding, out string failure)
            => TrySave(document, scene, newAssetPath, null, out binding, out failure);

        internal static bool TrySave(MapGraphEditorDocument document, Scene scene, string newAssetPath,
            Action<MapGraphSaveCheckpoint> onCheckpoint, out MapGraphBindingAuthoring binding, out string failure)
        {
            binding = null; failure = "";
            if (document == null || !scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            { failure = "SaveNeedsLoadedScene"; return false; }
            if (!document.TryVerifyForSave(out var input, out failure)) return false;
            if (input.Scene.ScenePath != scene.path) { failure = "SaveSceneMismatch"; return false; }
            long documentRevision = document.Revision;
            var source = document.SourceDefinition;
            string path = source != null ? AssetDatabase.GetAssetPath(source) : (newAssetPath ?? "").Replace('\\', '/');
            if (!ValidAssetPath(path) || source == null && (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null))
            { failure = "InvalidOrOccupiedMapAssetPath"; return false; }
            if (!Directory.Exists(Path.GetDirectoryName(path))) { failure = "MissingAssetDirectory"; return false; }
            var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).ToArray();
            if (existing.Length > 1 || existing.Length == 1 && existing[0].MapDefinition != null && existing[0].MapDefinition != source)
            { failure = "AmbiguousSceneMapBinding"; return false; }
            var draft = document.Layout;
            MapGraphNavigationBakeData bake; MapGraphTargetBinding[] targets; MapGraphZoneBinding[] zones;
            try
            {
                string profileId = input.Scene.Profiles[0].Data.ProfileId;
                bake = MapGraphNavigationBakeBuilder.Build(input.Scene, draft, input.Anchors, input.Connections, (source?.Revision ?? 0) + 1, profileId);
                var anchors = input.Anchors.Where(a => a.ProfileId == profileId).ToDictionary(a => a.NodeId, StringComparer.Ordinal);
                targets = draft.Nodes.Select(n =>
                {
                    var target = input.Scene.Nodes.Single(s => s.Id == n.NodeId);
                    if (target.Target == null || target.Target.gameObject.scene != scene || target.SourceObjectId != n.SourceObjectId || target.ZoneId != n.ZoneId)
                        throw new InvalidOperationException("InvalidSavedTarget:" + n.NodeId);
                    return new MapGraphTargetBinding(n.NodeId, target.Target, anchors[n.NodeId].Position, n.SourceObjectId);
                }).ToArray();
                zones = draft.Zones.Select(z =>
                {
                    var target = input.Scene.Zones.Single(s => s.Id == z.ZoneId);
                    if (target.IsSynthetic != z.IsSynthetic || target.SourceObjectId != z.SourceObjectId ||
                        (z.IsSynthetic ? target.Target != null : target.Target == null || target.Target.gameObject.scene != scene))
                        throw new InvalidOperationException("InvalidSavedZone:" + z.ZoneId);
                    return new MapGraphZoneBinding(z.ZoneId, target.Target, z.SourceObjectId, z.IsSynthetic);
                }).ToArray();
            }
            catch (Exception exception) { failure = exception.Message; return false; }
            if (document.Revision != documentRevision || document.HasSourceConflict) { failure = "DocumentChangedBeforeSave"; return false; }

            bool createdAsset = false, wroteAsset = false, wroteScene = false;
            int group = -1;
            byte[] priorScene = null, priorAsset = null;
            try
            {
                priorScene = File.ReadAllBytes(scene.path);
                priorAsset = source != null ? File.ReadAllBytes(path) : null;
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("保存指挥地图和场景绑定");
                if (source == null)
                {
                    source = ScriptableObject.CreateInstance<SO_MapGraphDefinition>(); source.name = Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(source, path); createdAsset = true;
                }
                Undo.RegisterCompleteObjectUndo(source, "保存指挥地图");
                var working = document.WorkingDefinition;
                source.ApplyCommandData(working.MapId, working.DisplayName, draft.StartNodeId, draft.Zones, draft.Nodes, draft.Edges,
                    draft.Constraints, bake, JsonUtility.FromJson<MapGraphGenerationSettings>(JsonUtility.ToJson(working.GenerationSettings)));
                if (existing.Length == 1) { binding = existing[0]; Undo.RegisterCompleteObjectUndo(binding, "保存群绑定"); }
                else
                {
                    var root = new GameObject("MapGraphBinding"); SceneManager.MoveGameObjectToScene(root, scene);
                    Undo.RegisterCreatedObjectUndo(root, "创建指挥地图绑定"); binding = Undo.AddComponent<MapGraphBindingAuthoring>(root);
                }
                binding.Configure(source, targets, zones);
                if (!binding.IsValid) throw new InvalidOperationException(string.Join("\n", binding.ValidationErrors));
                EditorUtility.SetDirty(binding); EditorSceneManager.MarkSceneDirty(scene);
                Undo.FlushUndoRecordObjects();
                wroteAsset = true; AssetDatabase.SaveAssetIfDirty(source);
                onCheckpoint?.Invoke(MapGraphSaveCheckpoint.AssetSaved);
                wroteScene = true;
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("SceneSaveFailed:" + scene.path);
                onCheckpoint?.Invoke(MapGraphSaveCheckpoint.SceneSaved);
                Undo.CollapseUndoOperations(group); document.MarkSaved(source); return true;
            }
            catch (Exception exception)
            {
                failure = "MapSaveFailed:" + exception.Message;
                try
                {
                    if (group >= 0) Undo.RevertAllDownToGroup(group);
                    if (createdAsset) AssetDatabase.DeleteAsset(path);
                    else if (wroteAsset && priorAsset != null) { File.WriteAllBytes(path, priorAsset); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                    if (wroteScene) File.WriteAllBytes(scene.path, priorScene);
                }
                catch (Exception rollback) { failure += "\nRollbackFailed:" + rollback.Message; }
                binding = existing.Length == 1 ? existing[0] : null;
                return false;
            }
        }

        private static bool ValidAssetPath(string path)
            => path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
               !path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0);
    }
}
