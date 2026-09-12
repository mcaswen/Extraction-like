using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnomalySearch.Editor.MapGraph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AgentReproduction
{
    /// <summary>隔离的实际 Editor 窗口视觉证据入口，生产窗口不依赖此工具。</summary>
    public static class MapGraphEditorPreviewEntry
    {
        private static MapGraphEditorWindow _window;
        private static string _output;
        private static double _started, _readyAt;
        private static int _stage, _paint;
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-mapGraphPreviewOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("MissingPreviewOutput");
            _output = args[index + 1]; Directory.CreateDirectory(_output);
            if (Application.companyName != "AnomalySearch.Automation") throw new InvalidOperationException("Use isolated preview launcher.");
            EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            _window = ScriptableObject.CreateInstance<MapGraphEditorWindow>();
            _window.position = new Rect(120, 100, 1400, 900); _window.ShowUtility(); _window.Focus();
            _started = EditorApplication.timeSinceStartup; _stage = 0; _window.BeginGeneration();
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - _started > 180) throw new TimeoutException("Editor preview exceeded 180 seconds: " + _window.Status);
                if (_stage == 0)
                {
                    var request = _window.Document.PendingGeneration;
                    if (request == null || request.IsRunning) return;
                    if (request.Result == null) throw new InvalidOperationException(_window.Status);
                    if (request.Diagnostics.Any(d => d.Code == "AuthoredGraphDisconnected")) throw new InvalidOperationException("EmptyCandidateGraphWarningLeakedToGeneratedResult");
                    if (!_window.ApplyPreview()) throw new InvalidOperationException(_window.Status);
                    _readyAt = EditorApplication.timeSinceStartup; _paint = _window.Canvas.PaintCount; _stage = 1;
                }
                else if (EditorApplication.timeSinceStartup - _readyAt > 3 && _window.Canvas.PaintCount > _paint + 5)
                {
                    Capture(_stage == 1 ? "01-editor-overview.png" : "02-editor-detail.png");
                    if (_stage == 1)
                    {
                        var layout = _window.Document.Layout; var node = layout.Nodes.First(n => n.NodeKind == Gameplay.MapGraph.Config.MapGraphNodeKind.Resource);
                        _window.Canvas.Select(MapGraphSelectionKind.Node, node.NodeId);
                        var rect = _window.Canvas.ViewRect;
                        _window.Canvas.ZoomAt(_window.Canvas.ToScreen(layout.Graph.GetNodePosition(node.NodeId), rect), rect, 1.65f);
                        _readyAt = EditorApplication.timeSinceStartup; _paint = _window.Canvas.PaintCount; _stage = 2;
                    }
                    else
                    {
                        var layout = _window.Document.Layout;
                        if (!MapGraphValidation.Validate(layout).IsValid) throw new InvalidOperationException("RenderedLayoutInvalid");
                        File.WriteAllText(Path.Combine(_output, "result.json"), JsonUtility.ToJson(new Evidence
                        { passed = true, zones = layout.Zones.Count, nodes = layout.Nodes.Count, edges = layout.Edges.Count,
                            paints = _window.Canvas.PaintCount, window = _window.position, canvas = _window.Canvas.ViewRect,
                            pixelScale = EditorGUIUtility.pixelsPerPoint, graphFingerprint = layout.ContentFingerprint, elapsedSeconds = EditorApplication.timeSinceStartup - _started }, true));
                        Finish(0);
                    }
                }
            }
            catch (Exception exception)
            { File.WriteAllText(Path.Combine(_output, "error.txt"), exception.ToString()); Finish(1); }
        }
        private static void Capture(string name)
        {
            Reporting.UnityEditorViewCapture.Capture(_window, Path.Combine(_output,name), 1f/3f);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (_window != null) { _window.DiscardChanges(); _window.Close(); }
            EditorApplication.Exit(code);
        }
        [Serializable] private sealed class Evidence
        { public bool passed; public int zones, nodes, edges, paints; public float pixelScale; public Rect window, canvas; public string graphFingerprint; public double elapsedSeconds; }
    }
}
