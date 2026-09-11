using System;
using System.IO;
using System.Linq;
using AnomalySearch.Automation.SceneRaid;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnomalySearch.Editor.SceneRaid
{
    // 构建只发生在显式配置的隔离 Editor，运行时控制器不依赖本文件。
    public static class SceneRaidBuildEntry
    {
        [Serializable] private sealed class Evidence
        {
            public int schemaVersion = 1;
            public int optionsBits, effectiveOptionsBits;
            public string runId, scenePath, status, executable, unityVersion, backend, target, options, quality;
            public string[] defines, messages;
            public double seconds;
            public ulong bytes;
            public int errors, warnings;
        }

        public static void Build(SceneRaidScenarioConfig config)
        {
            if (Application.companyName != "AnomalySearch.Automation" ||
                Application.productName != "AgentRepro_" + config.runId || config.mode != "Audit" || !config.buildPlayer)
                throw new InvalidOperationException("Build requires an isolated SceneRaid request.");
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("This validation profile requires the current Mono standalone backend.");

            var evidence = new Evidence
            {
                runId = config.runId, scenePath = config.scenePath, status = "FAILED",
                executable = Path.Combine(config.outputPath, "Player", "SceneRaid.exe"),
                unityVersion = Application.unityVersion, backend = "Mono2x", target = "StandaloneWindows64",
                options = "Development", optionsBits = (int)BuildOptions.Development, quality = "High Fidelity",
                defines = new[] { "ANOMALY_SCENE_AUTOMATION" }, messages = Array.Empty<string>()
            };
            int oldWidth = PlayerSettings.defaultScreenWidth, oldHeight = PlayerSettings.defaultScreenHeight;
            var oldMode = PlayerSettings.fullScreenMode;
            int oldQuality = QualitySettings.GetQualityLevel();
            try
            {
                int quality = Array.IndexOf(QualitySettings.names, evidence.quality);
                if (quality < 0) throw new InvalidOperationException("High Fidelity quality profile is missing.");
                QualitySettings.SetQualityLevel(quality, true);
                PlayerSettings.defaultScreenWidth = config.width;
                PlayerSettings.defaultScreenHeight = config.height;
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                Directory.CreateDirectory(Path.GetDirectoryName(evidence.executable));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { config.scenePath }, locationPathName = evidence.executable,
                    target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development, extraScriptingDefines = evidence.defines
                });
                evidence.status = report.summary.result == BuildResult.Succeeded ? "BUILT" : "FAILED";
                evidence.effectiveOptionsBits = (int)report.summary.options;
                evidence.seconds = report.summary.totalTime.TotalSeconds;
                evidence.bytes = report.summary.totalSize;
                evidence.errors = report.summary.totalErrors; evidence.warnings = report.summary.totalWarnings;
                evidence.messages = report.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                    .Select(message => message.type + ": " + message.content).ToArray();
                if (evidence.status != "BUILT" || !File.Exists(evidence.executable))
                    throw new InvalidOperationException("SceneRaid Player build failed; inspect build-result.json and Editor.log.");
            }
            catch (Exception ex)
            {
                evidence.status = "FAILED";
                evidence.messages = evidence.messages.Concat(new[] { ex.ToString() }).ToArray();
                throw;
            }
            finally
            {
                PlayerSettings.defaultScreenWidth = oldWidth; PlayerSettings.defaultScreenHeight = oldHeight;
                PlayerSettings.fullScreenMode = oldMode;
                QualitySettings.SetQualityLevel(oldQuality, true);
                string path = Path.Combine(config.outputPath, "build-result.json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(evidence, true));
                File.Move(path + ".tmp", path);
            }
        }
    }
}
