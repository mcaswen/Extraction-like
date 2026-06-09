using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "Scene_sdw_test2";
        [SerializeField] private Text[] menuLabels;
        [SerializeField]
        private string[] preferredFontNames =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "Arial"
        };

        private void Awake()
        {
            ApplyMenuFont();
        }

        public void StartGame()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError("Start menu gameplay scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        private void ApplyMenuFont()
        {
            if (preferredFontNames == null || preferredFontNames.Length == 0)
                return;

            Font menuFont = Font.CreateDynamicFontFromOSFont(preferredFontNames, 72);
            if (menuFont == null)
                return;

            Text[] labels = menuLabels != null && menuLabels.Length > 0
                ? menuLabels
                : GetComponentsInChildren<Text>(true);

            foreach (Text label in labels)
            {
                if (label == null)
                    continue;

                label.font = menuFont;
            }
        }
    }
}
