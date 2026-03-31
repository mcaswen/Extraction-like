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
    /// 顶层 HUD 刷新控制器
    /// </summary>
    public sealed class BoardGameHudController : MonoBehaviour
    {
        // AI 当前生命值显示文本
        [SerializeField] private TMP_Text _healthText;
        // AI 当前等级显示文本
        [SerializeField] private TMP_Text _levelText;
        // AI 当前经验值显示文本
        [SerializeField] private TMP_Text _experienceText;
        // 当前已携带总收益显示文本
        [SerializeField] private TMP_Text _valueText;
        // 当前背包容量占用显示文本
        [SerializeField] private TMP_Text _capacityText;
        // 当前动作类型显示文本
        [SerializeField] private TMP_Text _actionText;
        // 当前锁定目标节点显示文本
        [SerializeField] private TMP_Text _targetText;
        // 当前路径节点串显示文本
        [SerializeField] private TMP_Text _pathText;
        // 最近一次系统状态消息显示文本
        [SerializeField] private TMP_Text _statusText;
        // 当前是否处于玩家重定向模式的显示文本
        [SerializeField] private TMP_Text _redirectStateText;
        // 当前动作公共进度条填充图
        [SerializeField] private Image _actionProgressFillImage;
        // 当前经验条填充图
        [SerializeField] private Image _experienceProgressFillImage;

        private BoardGamePrototypeController _prototypeController;

        /// <summary>
        /// 绑定运行时总控
        /// </summary>
        public void Bind(BoardGamePrototypeController prototypeController)
        {
            _prototypeController = prototypeController;

            if (_prototypeController.IsProgressionEnabled)
            {
                EnsureProgressionWidgets();
            }
            else
            {
                SetProgressionWidgetsVisible(false);
            }

            _prototypeController.SessionChanged += Refresh;
            _prototypeController.SelectionChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// 刷新 HUD 文本和进度条
        /// </summary>
        private void Refresh()
        {
            if (_prototypeController == null)
            {
                return;
            }

            BoardGameSessionState sessionState = _prototypeController.SessionState;
            BoardAgentState agentState = sessionState.AgentState;
            BoardNodeRuntimeState targetNode = _prototypeController.GetNodeState(agentState.CurrentTargetNodeId);
            bool progressionEnabled = _prototypeController.IsProgressionEnabled;

            if (_healthText != null)
            {
                _healthText.text = $"HP: {agentState.CurrentHealth}/{agentState.MaxHealth}";
            }

            if (_levelText != null)
            {
                _levelText.text = progressionEnabled ? $"Lv: {agentState.Level}" : string.Empty;
            }

            if (_experienceText != null)
            {
                _experienceText.text = progressionEnabled
                    ? $"XP: {agentState.CurrentExperience}/{agentState.RequiredExperienceToNextLevel}"
                    : string.Empty;
            }

            if (_valueText != null)
            {
                _valueText.text = $"Value: {agentState.InventoryState.TotalValue}";
            }

            if (_capacityText != null)
            {
                _capacityText.text = $"Capacity: {agentState.InventoryState.UsedCapacity:0.0}/{agentState.InventoryState.MaxCapacity:0.0}";
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
                        BoardNodeRuntimeState nodeState = _prototypeController.GetNodeState(nodeId);
                        return nodeState != null ? nodeState.NodeId : nodeId;
                    }));
                _pathText.text = $"Path: {path}";
            }

            if (_statusText != null)
            {
                _statusText.text = sessionState.StatusMessage;
            }

            if (_redirectStateText != null)
            {
                _redirectStateText.text = _prototypeController.IsAwaitingLevelUpChoice
                    ? "Level Up: Press 1/2/3 or choose an upgrade"
                    : "Control: Hover and click a node to redirect";
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

        private void EnsureProgressionWidgets()
        {
            if (_prototypeController != null && !_prototypeController.IsProgressionEnabled)
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
            }

            if (_experienceText == null)
            {
                _experienceText = CreateRuntimeText(
                    "T_Experience",
                    new Vector2(-684.97906f, 102f),
                    new Vector2(484.042f, 36f),
                    templateText,
                    32f);
            }

            if (_experienceProgressFillImage == null)
            {
                _experienceProgressFillImage = CreateRuntimeProgressBar(
                    "XP_ProgressBar",
                    new Vector2(-684.97906f, 66f),
                    new Vector2(484.042f, 16f));
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

        private Image CreateRuntimeProgressBar(string objectName, Vector2 anchoredPosition, Vector2 sizeDelta)
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

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backgroundObject.transform, false);

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.29f, 0.84f, 0.62f, 0.95f);
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
