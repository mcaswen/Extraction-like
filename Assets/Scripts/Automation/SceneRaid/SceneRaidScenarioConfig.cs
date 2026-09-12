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
        public bool enabled = true, keepEditorOpen = true;
        public bool buildPlayer;
        public bool quitPlayerWhenComplete;
        public bool captureCommandCatalog;
        public string scenarioJson, scenarioSha256;

        public Commands.SceneRaidCommandScenario ParseCommandScenario()
        {
            if (schemaVersion != 2 || mode != "ManualCluster" || string.IsNullOrWhiteSpace(scenarioJson) ||
                !string.Equals(HashScenario(scenarioJson), scenarioSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Missing or mismatched command scenario.");
            var scenario = JsonUtility.FromJson<Commands.SceneRaidCommandScenario>(scenarioJson);
            if (scenario == null) throw new InvalidOperationException("Missing command scenario.");
            scenario.Validate();
            return scenario;
        }
        public static string HashScenario(string json)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
        }

        public static string ExplicitPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sceneRaidConfig");
            if (index < 0) return null;
            if (index + 1 >= args.Length) throw new InvalidOperationException("Missing SceneRaid config argument.");
            return args[index + 1];
        }

        public static SceneRaidScenarioConfig LoadExplicit(bool validateProductName = true)
        {
            string path = ExplicitPath();
            if (path == null) return null;
            var config = SceneRaidRequestFile.ReadPending(path);
            if (config == null) return null;
            if (config == null || (config.schemaVersion != 1 && config.schemaVersion != 2) || string.IsNullOrEmpty(config.runId) ||
                !Path.IsPathRooted(config.outputPath) || config.observeSeconds <= 0 || config.observeSeconds > 600 ||
                config.scenePath != "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity" ||
                (config.mode != "Audit" && config.mode != "Observe" && config.mode != "Autonomous" && config.mode != "ManualCluster") ||
                (config.buildPlayer && config.mode != "Audit") ||
                (config.simulationSpeed != 1 && !((config.mode == "Autonomous" || config.mode == "ManualCluster") && (config.simulationSpeed == 2 || config.simulationSpeed == 4))) ||
                Application.companyName != "AnomalySearch.Automation" ||
                (validateProductName && Application.productName != "AgentRepro_" + config.runId))
                throw new InvalidOperationException("Invalid or non-isolated SceneRaid session.");
            if (config.mode == "ManualCluster") config.ParseCommandScenario();
            return config;
        }
    }

    [Serializable]
    public sealed class SceneRaidRunResult
    {
        public int schemaVersion = 1;
        public string runId, mode, status, reason, scenePath, unityVersion, graphicsDevice, quality, persistentDataPath;
        public string runtime, graphicsApi;
        public bool batchMode, profilerEnabled, performanceAcceptance;
        public bool developmentBuild;
        public int vSyncCount, targetFrameRate;
        public int screenWidth, screenHeight, cameraWidth, cameraHeight, frames, frameCapacity, renderedFrames, events, lostEvents, errors, warnings;
        public double elapsedWallSeconds, elapsedGameSeconds, instrumentationMilliseconds;
        public int inventorySessions;
        public string[] observedAgents, inventoryAgents;
        public string[] counters;
    }
}
#endif
