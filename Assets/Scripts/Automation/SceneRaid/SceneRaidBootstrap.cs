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
            Random.InitState(config.seed);
            var root = new GameObject("[SceneRaidAutomation]");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SceneRaidRunController>().Initialize(config);
        }
    }
}
#endif
