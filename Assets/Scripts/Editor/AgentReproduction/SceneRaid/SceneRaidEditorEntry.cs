using System;
using System.IO;
using System.Reflection;
using AnomalySearch.Automation.SceneRaid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace AnomalySearch.Editor.SceneRaid
{
    [InitializeOnLoad]
    public static class SceneRaidEditorEntry
    {
        private const string Armed = "SceneRaid.Armed";
        private const string Phase = "SceneRaid.Phase";
        private static SceneRaidScenarioConfig _config;
        static SceneRaidEditorEntry() { EditorApplication.update += Poll; }

        public static void Run()
        {
            try
            {
                _config = SceneRaidScenarioConfig.LoadExplicit();
                if (_config == null) throw new InvalidOperationException("Explicit sceneRaidConfig is required.");
                if (Application.isBatchMode) throw new InvalidOperationException("SceneRaid requires a graphical Editor.");
                Directory.CreateDirectory(_config.outputPath);
                SessionState.SetBool("SceneRaid.OldPlayOptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt("SceneRaid.OldPlayOptions", (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetBool(Armed, true);
                SessionState.SetString(Phase, "loading");
                EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
                var scene = EditorSceneManager.OpenScene(_config.scenePath, OpenSceneMode.Single);
                SceneRaidSceneAudit.Save(scene, _config);
                if (_config.mode == "Audit") { Finish(0); return; }
                ConfigureGameView(_config.width, _config.height);
                QualitySettings.SetQualityLevel(2, true);
                Profiler.logFile = Path.Combine(_config.outputPath, "cpu-profile.raw");
                Profiler.enableBinaryLog = _config.profile && _config.binaryProfile;
                Profiler.enabled = _config.profile;
                SessionState.SetString(Phase, "entering");
                EditorApplication.isPlaying = true;
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void ConfigureGameView(int width, int height)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
            var groupType = group.GetType();
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            var enumType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new object[] { Enum.ToObject(enumType, 1), width, height, "SceneRaid 4K" }, null);
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var gameViewType = assembly.GetType("UnityEditor.GameView", true);
            var view = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, count - 1);
            view.maximized = true;
            view.Focus();
            view.Repaint();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Armed, false)) return;
            try
            {
                if (_config == null) _config = SceneRaidScenarioConfig.LoadExplicit();
                string phase = SessionState.GetString(Phase, "");
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetString(Phase, "playing");
                    if (!File.Exists(Path.Combine(_config.outputPath, "result.json"))) return;
                    SessionState.SetString(Phase, "exiting");
                    EditorApplication.isPlaying = false;
                }
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && phase == "exiting")
                {
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = false;
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    File.WriteAllText(Path.Combine(_config.outputPath, "cleanup.json"), "{\"sceneUnloaded\":true}");
                    SessionState.SetString(Phase, "draining");
                }
                else if (phase == "draining") SessionState.SetString(Phase, "readyToExit");
                else if (phase == "readyToExit") Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && phase == "playing")
                    throw new InvalidOperationException("Play Mode stopped without a completed run.");
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Fail(Exception ex)
        {
            if (_config != null) File.WriteAllText(Path.Combine(_config.outputPath, "editor-error.txt"), ex.ToString());
            Debug.LogException(ex);
            Finish(1);
        }
        private static void Finish(int exitCode)
        {
            SessionState.SetBool(Armed, false);
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("SceneRaid.OldPlayOptionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("SceneRaid.OldPlayOptions", 0);
            EditorApplication.Exit(exitCode);
        }
    }
}
