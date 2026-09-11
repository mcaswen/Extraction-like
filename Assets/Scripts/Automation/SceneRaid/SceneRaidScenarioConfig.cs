#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.IO;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    [Serializable]
    public sealed class SceneRaidScenarioConfig
    {
        public int schemaVersion = 1;
        public string runId, outputPath, scenePath, mode = "Observe";
        public int seed = 731, width = 3840, height = 2160;
        public float observeSeconds = 60, simulationSpeed = 1;
        public bool profile, binaryProfile;

        public static SceneRaidScenarioConfig LoadExplicit()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sceneRaidConfig");
            if (index < 0) return null;
            if (index + 1 >= args.Length) throw new InvalidOperationException("Missing SceneRaid config argument.");
            var config = JsonUtility.FromJson<SceneRaidScenarioConfig>(File.ReadAllText(args[index + 1]).TrimStart('\uFEFF'));
            if (config == null || config.schemaVersion != 1 || string.IsNullOrEmpty(config.runId) ||
                !Path.IsPathRooted(config.outputPath) || config.observeSeconds <= 0 || config.observeSeconds > 600 ||
                config.scenePath != "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity" ||
                (config.mode != "Audit" && config.mode != "Observe") || config.simulationSpeed != 1 ||
                Application.companyName != "AnomalySearch.Automation" ||
                Application.productName != "AgentRepro_" + config.runId)
                throw new InvalidOperationException("Invalid or non-isolated SceneRaid session.");
            return config;
        }
    }

    [Serializable]
    public sealed class SceneRaidRunResult
    {
        public int schemaVersion = 1;
        public string runId, mode, status, reason, scenePath, unityVersion, graphicsDevice, quality, persistentDataPath;
        public bool batchMode, profilerEnabled, performanceAcceptance;
        public int screenWidth, screenHeight, cameraWidth, cameraHeight, frames, renderedFrames, events, lostEvents, errors, warnings;
        public double elapsedWallSeconds, elapsedGameSeconds, instrumentationMilliseconds;
        public string[] observedAgents;
        public string[] counters;
    }
}
#endif
