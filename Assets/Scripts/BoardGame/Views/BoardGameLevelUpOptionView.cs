using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BoardGame.Views
{
    /// <summary>
    /// One upgrade option card.
    /// Uses only existing scene components and does not create runtime UI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardGameLevelUpOptionView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _descriptionText;

        public void Bind(
            string description,
            UnityAction onClick)
        {
            if (!TryResolveReferences())
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _descriptionText.text = description;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(onClick);
            _button.interactable = true;
            _button.transition = Selectable.Transition.None;
        }

        public void Clear()
        {
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
            }

            gameObject.SetActive(false);
        }

        private bool TryResolveReferences()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_descriptionText == null || _button == null || ResolveCardGraphic() == null)
            {
                Debug.LogWarning($"BoardGameLevelUpOptionView on {name} is missing required scene references.");
                return false;
            }

            Image cardGraphic = ResolveCardGraphic();
            cardGraphic.enabled = true;
            cardGraphic.raycastTarget = true;
            _descriptionText.raycastTarget = false;
            return true;
        }

        private Image ResolveCardGraphic()
        {
            if (_button != null && _button.targetGraphic is Image targetImage)
            {
                return targetImage;
            }

            return GetComponent<Image>();
        }
    }
}
