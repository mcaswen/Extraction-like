using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BoardGame.Views
{
    /// <summary>
    /// 升级选项单个槽位视图
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardGameLevelUpOptionView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMP_Text _descriptionText;

        /// <summary>
        /// 绑定单个升级选项的标题、描述、图标和点击事件
        /// </summary>
        /// <param name="description"></param>
        /// <param name="accentColor"></param>
        /// <param name="onClick"></param>
        public void Bind(
            string description,
            Color accentColor,
            UnityAction onClick)
        {
            EnsureRuntimeUi();
            gameObject.SetActive(true);

            if (_descriptionText != null)
            {
                _descriptionText.text = description;
            }

            if (_backgroundImage != null)
            {
                _backgroundImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.16f);
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(onClick);
                _button.interactable = true;

                ColorBlock colors = _button.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0.98f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
                _button.colors = colors;
            }
        }

        /// <summary>
        /// 清空当前选项并隐藏槽位
        /// </summary>
        public void Clear()
        {
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
            }

            gameObject.SetActive(false);
        }

        private void EnsureRuntimeUi()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();

                if (_button == null)
                {
                    _button = gameObject.AddComponent<Button>();
                }
            }

            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();

                if (_backgroundImage == null)
                {
                    _backgroundImage = gameObject.AddComponent<Image>();
                }
            }

            _backgroundImage.color = new Color(1f, 1f, 1f, 0.12f);
            _button.targetGraphic = _backgroundImage;

            RectTransform rootRect = GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0f, 100f);

            if (_descriptionText == null)
            {
                _descriptionText = CreateText("DescriptionText", new Vector2(28f, 0f), new Vector2(-28f, 56f), 28f, FontStyles.Bold);
                _descriptionText.enableWordWrapping = true;
                _descriptionText.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private TMP_Text CreateText(
            string objectName,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            FontStyles fontStyle)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.offsetMin = new Vector2(anchoredPosition.x, anchoredPosition.y - sizeDelta.y * 0.5f);
            rectTransform.offsetMax = new Vector2(sizeDelta.x, anchoredPosition.y + sizeDelta.y * 0.5f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
