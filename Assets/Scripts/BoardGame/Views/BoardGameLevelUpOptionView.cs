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
        [SerializeField] private Image _iconPlateImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _iconText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;

        /// <summary>
        /// 绑定单个升级选项的标题、描述、图标和点击事件
        /// </summary>
        /// <param name="description"></param>
        /// <param name="accentColor"></param>
        /// <param name="onClick"></param>
        public void Bind(
            string title,
            string description,
            string iconText,
            Sprite iconSprite,
            Color accentColor,
            UnityAction onClick)
        {
            EnsureRuntimeUi();
            gameObject.SetActive(true);

            if (_titleText != null)
            {
                _titleText.text = title;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = description;
            }

            if (_backgroundImage != null)
            {
                _backgroundImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.16f);
            }

            if (_iconPlateImage != null)
            {
                _iconPlateImage.color = accentColor;
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = iconSprite;
                _iconImage.enabled = iconSprite != null;
            }

            if (_iconText != null)
            {
                _iconText.text = iconSprite == null ? iconText : string.Empty;
                _iconText.color = Color.white;
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
            if (_button != null &&
                _backgroundImage != null &&
                _iconPlateImage != null &&
                _iconText != null &&
                _titleText != null &&
                _descriptionText != null)
            {
                return;
            }

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
            rootRect.sizeDelta = new Vector2(0f, 126f);

            if (_iconPlateImage == null)
            {
                _iconPlateImage = CreateImage("IconPlate", new Vector2(22f, 0f), new Vector2(98f, 98f), new Vector2(0f, 0.5f));
            }

            if (_iconImage == null)
            {
                _iconImage = CreateImage("Icon", new Vector2(22f, 0f), new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            }

            if (_iconText == null)
            {
                _iconText = CreateText("IconText", new Vector2(22f, 0f), new Vector2(98f, 60f), 30f, FontStyles.Bold);
                _iconText.alignment = TextAlignmentOptions.Center;
            }

            if (_titleText == null)
            {
                _titleText = CreateText("TitleText", new Vector2(138f, 28f), new Vector2(-154f, 34f), 30f, FontStyles.Bold);
            }

            if (_descriptionText == null)
            {
                _descriptionText = CreateText("DescriptionText", new Vector2(138f, -18f), new Vector2(-154f, 56f), 23f, FontStyles.Normal);
                _descriptionText.enableWordWrapping = true;
                _descriptionText.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        private Image CreateImage(string objectName, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMinMax)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(transform, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMinMax;
            rectTransform.anchorMax = anchorMinMax;
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
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
