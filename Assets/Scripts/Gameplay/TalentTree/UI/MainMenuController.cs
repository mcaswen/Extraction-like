using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = "Scene_PreparationInterface";
        [SerializeField] private Font menuFont;
        [SerializeField] private Text[] menuLabels;

        private void Awake()
        {
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

        public void StartGame()
        {
            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogError("Main menu next scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(nextSceneName);
        }

        public void OpenSettings()
        {
            Debug.Log("Settings menu is not implemented yet.", this);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
