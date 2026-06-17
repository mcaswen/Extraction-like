using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI
{
    public sealed class PreparationInterfaceController : MonoBehaviour
    {
        [SerializeField] private string storeSceneName = "ShopCanvasTest";
        [SerializeField] private string attributeSelectionSceneName = "Scene_ElementSelectionMenu";

        public void OpenStore()
        {
            LoadConfiguredScene(storeSceneName, "store");
        }

        public void StartAttributeSelection()
        {
            LoadConfiguredScene(attributeSelectionSceneName, "attribute selection");
        }

        public void OpenEnhance()
        {
            Debug.Log("Enhance screen is not implemented yet.", this);
        }

        public void OpenSettings()
        {
            Debug.Log("Settings screen is not implemented yet.", this);
        }

        private void LoadConfiguredScene(string sceneName, string label)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("Preparation interface " + label + " scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
