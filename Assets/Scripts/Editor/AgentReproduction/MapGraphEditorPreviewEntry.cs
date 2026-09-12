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
            // 读取此窗口的 GUIView 渲染表面，桌面前台遮挡不能混入证据。
            // Unity 2022.3 GUIView.bindings.cs 的 GrabPixels，仅用于 Editor 验证工具。
            var rect = _window.position; float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(rect.width * scale), height = Mathf.RoundToInt(rect.height * scale);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var parent = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(_window);
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            var grab = viewType?.GetMethod("GrabPixels", flags);
            if (parent == null || grab == null) throw new MissingMethodException("Unity 2022.3 GUIView.GrabPixels unavailable.");
            viewType.GetMethod("RepaintImmediately", flags)?.Invoke(parent, null);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                grab.Invoke(parent, new object[] { target, new Rect(0, 0, width, height) });
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    for (int y = 0; y < height / 2; y++)
                        for (int x = 0; x < width; x++)
                        { int a = y * width + x, b = (height - 1 - y) * width + x; (pixels[a], pixels[b]) = (pixels[b], pixels[a]); }
                    texture.SetPixels32(pixels); texture.Apply();
                }
                int dark = 0;
                foreach (var p in pixels) if (p.r > 5 && p.r < 70 && p.g > 10 && p.g < 90 && p.b > 12 && p.b < 105) dark++;
                if (dark < pixels.Length / 3) throw new InvalidOperationException("CaptureDoesNotContainTheDarkEditorCanvas");
                File.WriteAllBytes(Path.Combine(_output, name), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture); }
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
