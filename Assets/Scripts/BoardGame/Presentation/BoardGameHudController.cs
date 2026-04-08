using System.Linq;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// HUD controller.
    /// </summary>
    public sealed class BoardGameHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _experienceText;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private TMP_Text _capacityText;
        [SerializeField] private TMP_Text _actionText;
        [SerializeField] private TMP_Text _targetText;
        [SerializeField] private TMP_Text _pathText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _redirectStateText;
        [SerializeField] private Image _healthProgressFillImage;
        [SerializeField] private Image _capacityProgressFillImage;
        [SerializeField] private Image _actionProgressFillImage;
        [SerializeField] private Image _experienceProgressFillImage;

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameHudContextFormatter _contextFormatter;
        private bool _createdRuntimeLevelText;
        private bool _createdRuntimeExperienceText;

        public void Bind(BoardGameRuntimeQueryController runtimeQueryController)
        {
            _runtimeQueryController = runtimeQueryController;
            _contextFormatter = new BoardGameHudContextFormatter(runtimeQueryController);

            if (_runtimeQueryController.IsProgressionEnabled)
            {
                EnsureProgressionWidgets();
            }
            else
            {
                SetProgressionWidgetsVisible(false);
            }

            EnsureAttributeWidgets();
            _runtimeQueryController.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (_runtimeQueryController == null)
            {
                return;
            }

            BoardGameSessionState sessionState = _runtimeQueryController.SessionState;
            BoardAgentState agentState = _runtimeQueryController.GetFocusedAgentState();

            if (agentState == null)
            {
                return;
            }

            BoardNodeRuntimeState targetNode = _runtimeQueryController.GetNodeState(agentState.CurrentTargetNodeId);
            bool progressionEnabled = _runtimeQueryController.IsProgressionEnabled;

            if (_healthProgressFillImage != null)
            {
                _healthProgressFillImage.fillAmount = agentState.MaxHealth > 0
                    ? Mathf.Clamp01((float)agentState.CurrentHealth / agentState.MaxHealth)
                    : 0f;
            }
            else if (_healthText != null)
            {
                _healthText.text = $"HP: {agentState.CurrentHealth}/{agentState.MaxHealth}";
            }

            if (_levelText != null)
            {
                _levelText.text = progressionEnabled
                    ? $"Lv: {agentState.Level}"
                    : string.Empty;
            }

            if (_experienceText != null)
            {
                _experienceText.text = progressionEnabled
                    ? (_createdRuntimeExperienceText
                        ? $"{agentState.CurrentExperience}/{agentState.RequiredExperienceToNextLevel}"
                        : $"XP: {agentState.CurrentExperience}/{agentState.RequiredExperienceToNextLevel}")
                    : string.Empty;
            }

            if (_valueText != null)
            {
                _valueText.text = $"Value: {agentState.InventoryState.TotalValue}";
            }

            float capacityFillAmount = GetCapacityFillAmount(agentState);

            if (_capacityProgressFillImage != null)
            {
                _capacityProgressFillImage.fillAmount = capacityFillAmount;
            }
            else if (_capacityText != null)
            {
                if (_runtimeQueryController.IsBagSystemEnabled)
                {
                    int totalSlots = _runtimeQueryController.BagLayoutSettings.PlayerInventoryColumns *
                                     _runtimeQueryController.BagLayoutSettings.PlayerInventoryRows;
                    int occupiedSlots = agentState.InventoryState.Items.Count(item => item != null);
                    _capacityText.text = $"Capacity: {occupiedSlots}/{totalSlots}";
                }
                else
                {
                    _capacityText.text = $"Capacity: {agentState.InventoryState.UsedCapacity:0.#}/{agentState.InventoryState.MaxCapacity:0.#}";
                }
            }

            if (_actionText != null)
            {
                _actionText.text = $"Action: {BoardGameTypes.GetActionLabel(agentState.CurrentActionType)}";
            }

            if (_targetText != null)
            {
                _targetText.text = $"Target: {(targetNode != null ? targetNode.NodeId : "None")}";
            }

            if (_pathText != null)
            {
                string path = agentState.RemainingPathNodeIds.Count == 0
                    ? "None"
                    : string.Join(" -> ", agentState.RemainingPathNodeIds.Select(nodeId =>
                    {
                        BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(nodeId);
                        return nodeState != null ? nodeState.NodeId : nodeId;
                    }));
                _pathText.text = $"Path: {path}";
            }

            if (_statusText != null)
            {
                _statusText.text = _contextFormatter.BuildStatusText(sessionState);
            }

            if (_redirectStateText != null)
            {
                _redirectStateText.text = _contextFormatter.BuildRedirectHint();
            }

            SetProgressionWidgetsVisible(progressionEnabled);

            if (_actionProgressFillImage != null)
            {
                _actionProgressFillImage.fillAmount = Mathf.Clamp01(agentState.CurrentActionProgress);
            }

            if (_experienceProgressFillImage != null)
            {
                _experienceProgressFillImage.fillAmount = progressionEnabled && agentState.RequiredExperienceToNextLevel > 0
                    ? Mathf.Clamp01((float)agentState.CurrentExperience / agentState.RequiredExperienceToNextLevel)
                    : 0f;
            }
        }

        private void EnsureAttributeWidgets()
        {
            TMP_Text templateText = _healthText != null ? _healthText : GetComponentInChildren<TMP_Text>(true);

            if (_healthProgressFillImage == null)
            {
                _healthProgressFillImage = CreateRuntimeAttributeBar(
                    "HP_ProgressBar",
                    _healthText,
                    templateText,
                    "HP",
                    new Color(0.89f, 0.25f, 0.29f, 0.95f));
            }

            if (_capacityProgressFillImage == null)
            {
                _capacityProgressFillImage = CreateRuntimeAttributeBar(
                    "Capacity_ProgressBar",
                    _capacityText,
                    templateText,
                    "CAP",
                    new Color(0.93f, 0.68f, 0.18f, 0.95f));
            }

            if (_healthProgressFillImage != null)
            {
                SetWidgetVisible(_healthText, false);
            }

            if (_capacityProgressFillImage != null)
            {
                SetWidgetVisible(_capacityText, false);
            }
        }

        private void EnsureProgressionWidgets()
        {
            if (_runtimeQueryController != null && !_runtimeQueryController.IsProgressionEnabled)
            {
                return;
            }

            if (_levelText != null && _experienceText != null && _experienceProgressFillImage != null)
            {
                return;
            }

            TMP_Text templateText = _healthText != null ? _healthText : GetComponentInChildren<TMP_Text>();

            if (templateText == null)
            {
                return;
            }

            if (_levelText == null)
            {
                _levelText = CreateRuntimeText(
                    "T_Level",
                    new Vector2(-684.97906f, 146f),
                    new Vector2(484.042f, 48f),
                    templateText,
                    42f);
                _createdRuntimeLevelText = true;
            }

            if (_experienceText == null)
            {
                _experienceText = CreateRuntimeText(
                    "T_Experience",
                    new Vector2(-684.97906f, 102f),
                    new Vector2(484.042f, 36f),
                    templateText,
                    32f);
                _createdRuntimeExperienceText = true;
            }

            if (_experienceProgressFillImage == null)
            {
                _experienceProgressFillImage = CreateRuntimeProgressBar(
                    "XP_ProgressBar",
                    new Vector2(-684.97906f, 66f),
                    new Vector2(484.042f, 16f),
                    new Color(0.29f, 0.84f, 0.62f, 0.95f));
            }
        }

        private TMP_Text CreateRuntimeText(
            string objectName,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TMP_Text templateText,
            float fontSize)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = templateText.font;
            text.fontSharedMaterial = templateText.fontSharedMaterial;
            text.fontSize = fontSize;
            text.color = templateText.color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private float GetCapacityFillAmount(BoardAgentState agentState)
        {
            if (agentState == null || agentState.InventoryState == null)
            {
                return 0f;
            }

            if (_runtimeQueryController != null && _runtimeQueryController.IsBagSystemEnabled)
            {
                int totalSlots = _runtimeQueryController.BagLayoutSettings.PlayerInventoryColumns *
                                 _runtimeQueryController.BagLayoutSettings.PlayerInventoryRows;
                int occupiedSlots = agentState.InventoryState.Items.Count(item => item != null);
                return totalSlots > 0 ? Mathf.Clamp01((float)occupiedSlots / totalSlots) : 0f;
            }

            float maxCapacity = agentState.InventoryState.MaxCapacity;
            return maxCapacity > Mathf.Epsilon
                ? Mathf.Clamp01(agentState.InventoryState.UsedCapacity / maxCapacity)
                : 0f;
        }

        private Image CreateRuntimeAttributeBar(
            string objectName,
            TMP_Text anchorText,
            TMP_Text templateText,
            string label,
            Color fillColor)
        {
            if (anchorText == null)
            {
                return null;
            }

            RectTransform anchorRect = anchorText.rectTransform;
            float width = anchorRect != null
                ? Mathf.Max(220f, Mathf.Max(anchorRect.rect.width, anchorRect.sizeDelta.x))
                : 220f;
            Vector2 anchoredPosition = anchorRect != null ? anchorRect.anchoredPosition : Vector2.zero;
            TMP_Text labelTemplate = templateText != null ? templateText : anchorText;
            return CreateRuntimeProgressBar(
                objectName,
                anchoredPosition,
                new Vector2(width, 18f),
                fillColor,
                labelTemplate,
                label);
        }

        private Image CreateRuntimeProgressBar(
            string objectName,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color fillColor,
            TMP_Text labelTemplate = null,
            string label = null)
        {
            GameObject backgroundObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(transform, false);

            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = anchoredPosition;
            backgroundRect.sizeDelta = sizeDelta;

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0.16f);
            backgroundImage.raycastTarget = false;

            if (labelTemplate != null && !string.IsNullOrEmpty(label))
            {
                TMP_Text labelText = CreateRuntimeText(
                    "Label",
                    Vector2.zero,
                    sizeDelta,
                    labelTemplate,
                    Mathf.Max(16f, labelTemplate.fontSize * 0.58f));
                labelText.transform.SetParent(backgroundObject.transform, false);

                RectTransform labelRect = labelText.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(1f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.offsetMin = new Vector2(12f, -sizeDelta.y * 0.5f);
                labelRect.offsetMax = new Vector2(-12f, sizeDelta.y * 0.5f);

                labelText.alignment = TextAlignmentOptions.Center;
                labelText.text = label;
            }

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backgroundObject.transform, false);
            fillObject.transform.SetAsFirstSibling();

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            return fillImage;
        }

        private void SetProgressionWidgetsVisible(bool isVisible)
        {
            SetWidgetVisible(_levelText, isVisible);
            SetWidgetVisible(_experienceText, isVisible);
            SetProgressBarVisible(_experienceProgressFillImage, isVisible);
        }

        private static void SetWidgetVisible(Component component, bool isVisible)
        {
            if (component == null)
            {
                return;
            }

            if (component.gameObject.activeSelf != isVisible)
            {
                component.gameObject.SetActive(isVisible);
            }
        }

        private static void SetProgressBarVisible(Image fillImage, bool isVisible)
        {
            if (fillImage == null)
            {
                return;
            }

            Transform parent = fillImage.transform.parent;
            GameObject target = fillImage.gameObject;

            if (parent != null &&
                parent.GetComponent<Image>() != null &&
                parent.childCount == 1)
            {
                target = parent.gameObject;
            }

            if (target.activeSelf != isVisible)
            {
                target.SetActive(isVisible);
            }
        }
    }
}
