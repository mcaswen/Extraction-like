#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public static class SceneRaidBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var config = SceneRaidScenarioConfig.LoadExplicit();
            if (config == null || config.mode == "Audit") return;
            if (!Application.isEditor)
            {
                int quality = System.Array.IndexOf(QualitySettings.names, "High Fidelity");
                if (quality < 0) throw new System.InvalidOperationException("Validation Player requires High Fidelity quality.");
                QualitySettings.SetQualityLevel(quality, true);
                Screen.SetResolution(config.width, config.height, FullScreenMode.FullScreenWindow);
                UnityEngine.Profiling.Profiler.enabled = config.profile;
            }
            Random.InitState(config.seed);
            var root = new GameObject("[SceneRaidAutomation]");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SceneRaidRunController>().Initialize(config);
        }
    }
}
#endif
