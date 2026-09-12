using System;
using System.IO;
using System.Linq;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentReproduction
{
    /// <summary>显式隔离批处理的正式地图生成和读回验收。源项目写入由外部增量应用工具负责。</summary>
    public static class MapGraphSceneInstallEntry
    {
        public const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        public const string AssetPath = "Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset";
        private static MapGraphEditorDocument _document;
        private static MapGraphGenerationController _request;
        private static string _output;
        private static Scene _scene;
        private static double _started;
        private static bool _verifyOnly;
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-mapGraphInstallOutput");
            if (!Application.isBatchMode || Application.companyName != "AnomalySearch.Automation" || index < 0 || index + 1 >= args.Length)
                throw new InvalidOperationException("Use the isolated map install launcher.");
            _output = args[index + 1]; Directory.CreateDirectory(_output);
            try
            {
                File.Copy(ScenePath, Path.Combine(_output, "base-scene.unity"), true);
                _scene = EditorSceneManager.OpenScene(ScenePath);
                var source = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetPath);
                _verifyOnly = source != null;
                if (_verifyOnly)
                {
                    var input = MapGraphSceneCollector.Capture(_scene);
                    var existing = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
                    Require(existing.MapDefinition == source && existing.IsValid, "Source scene binding is invalid before verification.");
                    var originalCosts = new MapGraphNavigationCostService(source, existing, input.Profiles[0].QueryProfile, input.SceneFingerprint, input.NavigationFingerprint);
                    Require(originalCosts.PendingEdgeCount == 0 && originalCosts.CalculationCount == 0, "Original source bake is stale; verification must not repair it first.");
                }
                _document = new MapGraphEditorDocument(source);
                if (source == null) _document.SetDisplayName("区域指挥图");
                _request = _document.BeginGeneration(_scene, source == null ? MapGraphGenerationMode.ConnectionsAndLayout : MapGraphGenerationMode.ValidateOnly);
                _started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - _started > 180) throw new TimeoutException("Map installation exceeded generation deadline.");
                _request.Advance(64, 6); if (_request.IsRunning) return;
                if (_request.Stage != MapGraphGenerationStage.Ready) throw new InvalidOperationException(string.Join("\n", _request.Diagnostics));
                var input = _request.Result;
                Require(_document.TryApplyGeneration(_request, out var failure), failure);
                if (!_verifyOnly) Require(MapGraphAuthoringTransaction.TrySave(_document, _scene, AssetPath, out _, out failure), failure);
                string content = _document.Layout.ContentFingerprint; var assetGuid = AssetDatabase.AssetPathToGUID(AssetPath);
                _document.Dispose(); _document = null;
                _scene = EditorSceneManager.OpenScene(ScenePath);
                var current = MapGraphSceneCollector.Capture(_scene);
                Require(current.SceneFingerprint == input.Scene.SceneFingerprint && current.NavigationFingerprint == input.Scene.NavigationFingerprint, "Installation changed world inputs.");
                var binding = _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGraphBindingAuthoring>(true)).Single();
                Require(binding.IsValid, string.Join("\n", binding.ValidationErrors));
                var draft = MapGraphLayoutDraft.FromDefinition(binding.MapDefinition);
                Require(draft.ContentFingerprint == content && MapGraphValidation.Validate(draft).IsValid, "Saved map differs from the verified draft.");
                Require(binding.TargetBindings.Count == 28 && binding.ZoneBindings.Count == 7, "Actual scene binding baseline changed.");
                var costs = new MapGraphNavigationCostService(binding.MapDefinition, binding, current.Profiles[0].QueryProfile, current.SceneFingerprint, current.NavigationFingerprint);
                Require(costs.PendingEdgeCount == 0 && costs.CalculationCount == 0, "Saved navigation bake could not be reused.");
                File.Copy(ScenePath, Path.Combine(_output, "saved-scene.unity"), true);
                File.Copy(AssetPath, Path.Combine(_output, "map.asset"), true); File.Copy(AssetPath + ".meta", Path.Combine(_output, "map.asset.meta"), true);
                File.WriteAllText(Path.Combine(_output, "result.json"), JsonUtility.ToJson(new Evidence
                {
                    passed = true, verifyOnly = _verifyOnly, scenePath = ScenePath, assetPath = AssetPath, assetGuid = assetGuid,
                    graphFingerprint = content, sceneFingerprint = current.SceneFingerprint, navigationFingerprint = current.NavigationFingerprint,
                    zones = draft.Zones.Count, nodes = draft.Nodes.Count, edges = draft.Edges.Count, pendingQueries = costs.PendingEdgeCount,
                    elapsedSeconds = EditorApplication.timeSinceStartup - _started, warnings = _request.Diagnostics.Where(d => !d.IsError).Select(d => d.ToString()).Distinct().ToArray()
                }, true));
                Finish(0);
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static void Fail(Exception exception)
        { if (!string.IsNullOrEmpty(_output)) File.WriteAllText(Path.Combine(_output, "error.txt"), exception.ToString()); Finish(1); }
        private static void Finish(int code)
        { EditorApplication.update -= Tick; _document?.Dispose(); _document = null; EditorApplication.Exit(code); }
        private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
        [Serializable] private sealed class Evidence
        { public bool passed, verifyOnly; public string scenePath, assetPath, assetGuid, graphFingerprint, sceneFingerprint, navigationFingerprint; public int zones, nodes, edges, pendingQueries; public double elapsedSeconds; public string[] warnings; }
    }
}
