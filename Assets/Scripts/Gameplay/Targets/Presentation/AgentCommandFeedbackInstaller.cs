using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Targets.Presentation
{
    public static class AgentCommandFeedbackInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstalled();
        public static void EnsureInstalled()
        {
            if (Object.FindObjectOfType<AgentCommandFeedbackPresenter>() != null) return;
            var prefab = Resources.Load<GameObject>("HUD/Pfb_AgentCommandFeedback");
            if (prefab != null) Object.Instantiate(prefab);
        }
    }
}
