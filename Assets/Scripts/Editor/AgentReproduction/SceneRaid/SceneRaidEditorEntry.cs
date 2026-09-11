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
        private static SceneRaidProfilerCapture _capture;
        private static double _nextIdlePoll;

        [Serializable] private sealed class EditorState
        {
            public string runId, utc, phase;
            public int pid, exitCode;
            public bool idle;
        }

        private static EditorState CurrentState(string runId, int exitCode = 0)
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            bool idle = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating && !SessionState.GetBool(Armed, false);
            return new EditorState { runId = runId, utc = DateTime.UtcNow.ToString("o"), pid = process.Id,
                phase = idle ? "idle" : "busy", idle = idle, exitCode = exitCode };
        }

        private static void WriteState(EditorState state)
        {
            string path = SceneRaidScenarioConfig.ExplicitPath() + ".editor-state.json";
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(state));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
        }

        private static void PollRetainedEditor()
        {
            if (!SessionState.GetBool("SceneRaid.Retained", false) || EditorApplication.timeSinceStartup < _nextIdlePoll) return;
            _nextIdlePoll = EditorApplication.timeSinceStartup + 0.5;
            var state = CurrentState(SessionState.GetString("SceneRaid.LastRunId", ""));
            WriteState(state);
            if (!state.idle) return;
            var request = SceneRaidScenarioConfig.LoadExplicit(false);
            if (request == null || request.runId == state.runId) return;
            if (SessionState.GetString("SceneRaid.RefreshedRequest", "") != request.runId)
            {
                SessionState.SetString("SceneRaid.RefreshedRequest", request.runId);
                AssetDatabase.Refresh();
                return;
            }
            if (EditorUtility.scriptCompilationFailed)
            {
                _config = request;
                Fail(new InvalidOperationException("Script compilation failed before Play Mode."));
                return;
            }
            Run();
        }
        static SceneRaidEditorEntry()
        {
            EditorApplication.update += Poll;
            EditorApplication.quitting += () => TraceShutdown("EditorApplication.quitting");
            AssemblyReloadEvents.beforeAssemblyReload += () => TraceShutdown("beforeAssemblyReload");
            AppDomain.CurrentDomain.DomainUnload += (_, __) => TraceShutdown("AppDomain.DomainUnload");
            AppDomain.CurrentDomain.ProcessExit += (_, __) => TraceShutdown("AppDomain.ProcessExit");
        }

        // File-only tracing remains usable after Unity native services stop. No hooks are installed in a Player.
        private static void TraceShutdown(string phase)
        {
            if (_config == null) return;
            try
            {
                File.AppendAllText(Path.Combine(_config.outputPath, "shutdown-phases.jsonl"),
                    "{\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\",\"phase\":\"" + phase + "\"}\n");
            }
            catch (IOException) { /* The external process monitor still records a missing phase file. */ }
        }

        public static void Run()
        {
            _config = null;
            try
            {
                _config = SceneRaidScenarioConfig.LoadExplicit(false);
                if (_config == null) throw new InvalidOperationException("Explicit sceneRaidConfig is required.");
                if (Application.isBatchMode) throw new InvalidOperationException("SceneRaid requires a graphical Editor.");
                PlayerSettings.productName = "AgentRepro_" + _config.runId;
                _config = SceneRaidScenarioConfig.LoadExplicit();
                Directory.CreateDirectory(_config.outputPath);
                SessionState.SetBool("SceneRaid.OldPlayOptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt("SceneRaid.OldPlayOptions", (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetBool(Armed, true);
                SessionState.SetBool("SceneRaid.Retained", _config.keepEditorOpen);
                SessionState.SetString("SceneRaid.LastRunId", _config.runId);
                WriteState(CurrentState(_config.runId));
                SessionState.SetString(Phase, "loading");
                EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
                var scene = EditorSceneManager.OpenScene(_config.scenePath, OpenSceneMode.Single);
                SceneRaidSceneAudit.Save(scene, _config);
                if (_config.mode == "Audit")
                {
                    if (_config.buildPlayer) SceneRaidBuildEntry.Build(_config);
                    Finish(0); return;
                }
                ConfigureGameView(_config.width, _config.height);
                QualitySettings.SetQualityLevel(2, true);
                Profiler.logFile = Path.Combine(_config.outputPath, "cpu-profile.raw");
                Profiler.enableBinaryLog = _config.profile && _config.binaryProfile;
                Profiler.enabled = _config.profile;
                if (_config.profile)
                {
                    if (UnityEditorInternal.ProfilerDriver.deepProfiling)
                        throw new InvalidOperationException("CPU hierarchy diagnosis requires Deep Profile to be disabled.");
                    SessionState.SetBool("SceneRaid.OldProfileEditor", UnityEditorInternal.ProfilerDriver.profileEditor);
                    UnityEditorInternal.ProfilerDriver.profileEditor = true;
                    UnityEditorInternal.ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
                }
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
            try
            {
                if (!SessionState.GetBool(Armed, false)) { PollRetainedEditor(); return; }
                if (_config == null) _config = SceneRaidScenarioConfig.LoadExplicit();
                string phase = SessionState.GetString(Phase, "");
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetString(Phase, "playing");
                    if (_config.profile)
                    {
                        if (_capture == null) _capture = new SceneRaidProfilerCapture(_config);
                        _capture.SampleLatest();
                    }
                    if (!File.Exists(Path.Combine(_config.outputPath, "result.json"))) return;
                    _capture?.Save();
                    _capture = null;
                    if (_config.profile) UnityEditorInternal.ProfilerDriver.enabled = false;
                    SessionState.SetString(Phase, "exiting");
                    TraceShutdown("PlayMode.stopRequested");
                    EditorApplication.isPlaying = false;
                }
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && phase == "exiting")
                {
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = false;
                    TraceShutdown("PlayMode.stopped");
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    TraceShutdown("Scene.unloaded");
                    File.WriteAllText(Path.Combine(_config.outputPath, "cleanup.json"), "{\"sceneUnloaded\":true}");
                    SessionState.SetString(Phase, "draining");
                }
                else if (phase == "draining") SessionState.SetString(Phase, "readyToExit");
                else if (phase == "readyToExit") Finish(0);
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && phase == "playing")
                    throw new InvalidOperationException("Play Mode stopped without a completed run.");
                else if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && phase == "entering")
                    throw new InvalidOperationException("Play Mode entry was rejected or cancelled; inspect compilation errors.");
            }
            catch (IOException) when (!SessionState.GetBool(Armed, false))
            {
                // 空闲交接暂时占用文件时，下次轮询重试，不结束或改写上一轮。
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Fail(Exception ex)
        {
            if (_config != null) File.WriteAllText(Path.Combine(_config.outputPath, "editor-error.txt"), ex.ToString());
            Debug.LogException(ex);
            if (_config != null) Finish(1);
        }
        private static void Finish(int exitCode)
        {
            SessionState.SetBool(Armed, false);
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            if (_config != null && _config.profile)
            {
                UnityEditorInternal.ProfilerDriver.enabled = false;
                UnityEditorInternal.ProfilerDriver.profileEditor = SessionState.GetBool("SceneRaid.OldProfileEditor", false);
            }
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("SceneRaid.OldPlayOptionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("SceneRaid.OldPlayOptions", 0);
            if (_config != null && _config.keepEditorOpen)
            {
                SceneRaidRequestFile.Complete(SceneRaidScenarioConfig.ExplicitPath(), _config.runId);
                var state = CurrentState(_config.runId, exitCode);
                WriteState(state);
                File.WriteAllText(Path.Combine(_config.outputPath, "editor-ready.json"), JsonUtility.ToJson(state));
                TraceShutdown("Editor.retained");
                _config = null;
                return;
            }
            TraceShutdown("EditorApplication.Exit.before");
            EditorApplication.Exit(exitCode);
            TraceShutdown("EditorApplication.Exit.returned");
        }
    }
}
