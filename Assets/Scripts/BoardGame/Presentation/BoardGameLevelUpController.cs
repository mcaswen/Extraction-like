using System.Collections.Generic;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using BoardGame.Runtime;
using BoardGame.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 升级弹窗控制器
    /// </summary>
    public sealed class BoardGameLevelUpController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private List<BoardGameLevelUpOptionView> _optionViews = new List<BoardGameLevelUpOptionView>();
        [SerializeField] private Sprite _attackIconSprite;
        [SerializeField] private Sprite _defenseIconSprite;
        [SerializeField] private Sprite _healthIconSprite;

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameProgressionController _progressionController;

        /// <summary>
        /// 绑定升级所需的只读查询和流程控制器
        /// </summary>
        public void Bind(
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameProgressionController progressionController)
        {
            _runtimeQueryController = runtimeQueryController;
            _progressionController = progressionController;

            if (_runtimeQueryController.IsProgressionEnabled)
            {
                EnsureRuntimeUi();
            }
            else
            {
                SetVisible(false);
            }

            _runtimeQueryController.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// 根据当前升级状态刷新弹窗显隐和三个选项的展示内容
        /// </summary>
        private void Refresh()
        {
            if (_runtimeQueryController == null || _progressionController == null)
            {
                return;
            }

            if (!_runtimeQueryController.IsProgressionEnabled)
            {
                ClearOptions();
                SetVisible(false);
                return;
            }

            EnsureRuntimeUi();

            BoardGameSessionState sessionState = _runtimeQueryController.SessionState;
            BoardAgentState activeLevelUpAgentState = _runtimeQueryController.GetActiveLevelUpAgentState();
            bool isVisible = activeLevelUpAgentState != null &&
                             sessionState.IsAwaitingLevelUpChoice &&
                             sessionState.PendingLevelUpChoices.Count > 0;
            SetVisible(isVisible);

            if (!isVisible)
            {
                ClearOptions();
                return;
            }

            if (_titleText != null)
            {
                _titleText.text = $"Level Up! {activeLevelUpAgentState.DisplayName} Lv {activeLevelUpAgentState.Level}";
            }

            if (_hintText != null)
            {
                _hintText.text = $"Game paused for {activeLevelUpAgentState.DisplayName}  Choose 1 of 3 upgrades";
            }

            // UI 固定只有三个槽位，所以这里按索引把当前待选项逐个映射进去
            for (int index = 0; index < _optionViews.Count; index++)
            {
                if (_optionViews[index] == null)
                {
                    continue;
                }

                if (index >= sessionState.PendingLevelUpChoices.Count)
                {
                    _optionViews[index].Clear();
                    continue;
                }

                BoardLevelUpChoice choice = sessionState.PendingLevelUpChoices[index];
                int capturedIndex = index;
                BoardLevelUpOptionPresentation optionPresentation = BuildOptionPresentation(choice);
                _optionViews[index].Bind(
                    optionPresentation.Title,
                    optionPresentation.Description,
                    optionPresentation.IconText,
                    optionPresentation.IconSprite,
                    optionPresentation.AccentColor,
                    () => _progressionController.TryApplyLevelUpChoice(capturedIndex));
            }
        }

        /// <summary>
        /// 清空全部选项槽位
        /// </summary>
        private void ClearOptions()
        {
            foreach (BoardGameLevelUpOptionView optionView in _optionViews)
            {
                optionView?.Clear();
            }
        }

        /// <summary>
        /// 切换升级面板显隐
        /// </summary>
        private void SetVisible(bool isVisible)
        {
            GameObject target = _panelRoot != null ? _panelRoot : gameObject;
            target.SetActive(isVisible);
        }

        /// <summary>
        /// 在场景未预摆升级 UI 时，运行时构造一套可用的弹窗层级
        /// </summary>
        private void EnsureRuntimeUi()
        {
            if (_panelRoot != null && _titleText != null && _hintText != null && _optionViews.Count > 0)
            {
                return;
            }

            if (_panelRoot == null)
            {
                _panelRoot = gameObject;
            }

            RectTransform overlayRect = GetComponent<RectTransform>();

            if (overlayRect == null)
            {
                overlayRect = gameObject.AddComponent<RectTransform>();
            }

            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);

            Image overlayImage = _panelRoot.GetComponent<Image>();

            if (overlayImage == null)
            {
                overlayImage = _panelRoot.AddComponent<Image>();
            }

            overlayImage.color = new Color(0.05f, 0.07f, 0.09f, 0.82f);
            overlayImage.raycastTarget = true;

            GameObject windowObject = CreatePanel("Window", _panelRoot.transform, new Vector2(780f, 560f));
            Image windowImage = windowObject.GetComponent<Image>();
            windowImage.color = new Color(0.11f, 0.14f, 0.18f, 0.96f);

            _titleText = CreateText(
                "TitleText",
                windowObject.transform,
                new Vector2(0f, 216f),
                new Vector2(660f, 52f),
                44f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

            _hintText = CreateText(
                "HintText",
                windowObject.transform,
                new Vector2(0f, 166f),
                new Vector2(660f, 34f),
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

            // 选项列表交给 LayoutGroup 自动排版，避免后续增删卡片时还要手调位置
            GameObject optionsContainer = new GameObject(
                "Options",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            optionsContainer.transform.SetParent(windowObject.transform, false);

            RectTransform optionsRect = optionsContainer.GetComponent<RectTransform>();
            optionsRect.anchorMin = new Vector2(0.5f, 0.5f);
            optionsRect.anchorMax = new Vector2(0.5f, 0.5f);
            optionsRect.pivot = new Vector2(0.5f, 0.5f);
            optionsRect.anchoredPosition = new Vector2(0f, -34f);
            optionsRect.sizeDelta = new Vector2(700f, 360f);

            VerticalLayoutGroup layoutGroup = optionsContainer.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 18f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;

            ContentSizeFitter contentSizeFitter = optionsContainer.GetComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _optionViews.Clear();

            for (int index = 0; index < 3; index++)
            {
                GameObject optionObject = new GameObject(
                    $"Option{index + 1}",
                    typeof(RectTransform),
                    typeof(LayoutElement),
                    typeof(BoardGameLevelUpOptionView));
                optionObject.transform.SetParent(optionsContainer.transform, false);

                LayoutElement layoutElement = optionObject.GetComponent<LayoutElement>();
                layoutElement.preferredHeight = 126f;
                layoutElement.minHeight = 126f;

                RectTransform optionRect = optionObject.GetComponent<RectTransform>();
                optionRect.sizeDelta = new Vector2(700f, 126f);
                _optionViews.Add(optionObject.GetComponent<BoardGameLevelUpOptionView>());
            }
        }

        /// <summary>
        /// 把升级选择转换成 UI 展示需要的标题、描述、图标和主题色
        /// </summary>
        private BoardLevelUpOptionPresentation BuildOptionPresentation(BoardLevelUpChoice choice)
        {
            switch (choice.BuffType)
            {
                case BoardLevelUpBuffType.AttackFlat:
                    return new BoardLevelUpOptionPresentation(
                        "Sharpened Strikes",
                        $"Attack +{choice.Value}. Deal more damage each combat tick.",
                        "ATK",
                        _attackIconSprite,
                        new Color(0.86f, 0.33f, 0.27f, 1f));
                case BoardLevelUpBuffType.DefenseFlat:
                    return new BoardLevelUpOptionPresentation(
                        "Reinforced Armor",
                        $"Defense +{choice.Value}. Reduce the damage taken every hit.",
                        "DEF",
                        _defenseIconSprite,
                        new Color(0.24f, 0.55f, 0.9f, 1f));
                case BoardLevelUpBuffType.MaxHealthFlat:
                    return new BoardLevelUpOptionPresentation(
                        "Vital Reserve",
                        $"Max HP +{choice.Value}. Heal the same amount immediately.",
                        "HP",
                        _healthIconSprite,
                        new Color(0.26f, 0.77f, 0.5f, 1f));
                default:
                    return new BoardLevelUpOptionPresentation(
                        choice.DisplayLabel,
                        "Apply an upgrade to the agent.",
                        "UP",
                        null,
                        new Color(0.9f, 0.82f, 0.32f, 1f));
            }
        }

        /// <summary>
        /// 创建一个基础面板节点
        /// </summary>
        private static GameObject CreatePanel(string objectName, Transform parent, Vector2 sizeDelta)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = sizeDelta;
            return panelObject;
        }

        /// <summary>
        /// 创建一个运行时 TextMeshPro 文本节点
        /// </summary>
        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }

    internal readonly struct BoardLevelUpOptionPresentation
    {
        public BoardLevelUpOptionPresentation(
            string title,
            string description,
            string iconText,
            Sprite iconSprite,
            Color accentColor)
        {
            Title = title;
            Description = description;
            IconText = iconText;
            IconSprite = iconSprite;
            AccentColor = accentColor;
        }

        public string Title { get; }
        public string Description { get; }
        public string IconText { get; }
        public Sprite IconSprite { get; }
        public Color AccentColor { get; }
    }
}
