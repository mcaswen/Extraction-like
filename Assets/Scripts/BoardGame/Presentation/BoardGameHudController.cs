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
        [SerializeField] private Image _actionProgressFillImage;
        [SerializeField] private Image _experienceProgressFillImage;

        private BoardGameRuntimeQueryController _runtimeQueryController;

        /// <summary>
        /// 绑定运行时只读查询控制器
        /// </summary>
        public void Bind(BoardGameRuntimeQueryController runtimeQueryController)
        {
            _runtimeQueryController = runtimeQueryController;

            if (_runtimeQueryController.IsProgressionEnabled)
            {
                EnsureProgressionWidgets();
            }
            else
            {
                SetProgressionWidgetsVisible(false);
            }

            _runtimeQueryController.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// 刷新 HUD 文本和进度条
        /// </summary>
        private void Refresh()
        {
            if (_runtimeQueryController == null)
            {
                return;
            }

            BoardGameSessionState sessionState = _runtimeQueryController.SessionState;
            BoardAgentState agentState = sessionState.AgentState;
            BoardNodeRuntimeState targetNode = _runtimeQueryController.GetNodeState(agentState.CurrentTargetNodeId);
            bool progressionEnabled = _runtimeQueryController.IsProgressionEnabled;

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
                if (_runtimeQueryController.IsBagSystemEnabled)
                {
                    // 开背包系统时，Capacity 展示的是格子占用，而不是旧版数字容量
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
                _statusText.text = sessionState.StatusMessage;
            }

            if (_redirectStateText != null)
            {
                // 右下角提示优先反映当前是否被升级或 loot 交互锁住，避免玩家误判控制状态
                _redirectStateText.text = _runtimeQueryController.IsAwaitingLevelUpChoice
                    ? "Level Up: Press 1/2/3 or choose an upgrade"
                    : (_runtimeQueryController.IsAwaitingLootInteraction
                        ? "Loot: Press F to open or continue searching"
                        : "Control: Hover and click a node to redirect");
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

        /// <summary>
        /// 在运行时补齐升级相关的 HUD 文本与进度条
        /// </summary>
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
                // 运行时补建时沿用 HUD 现有层级，避免再维护一份单独的升级 UI 预制
                _experienceProgressFillImage = CreateRuntimeProgressBar(
                    "XP_ProgressBar",
                    new Vector2(-684.97906f, 66f),
                    new Vector2(484.042f, 16f));
            }
        }

        /// <summary>
        /// 基于现有文本样式克隆一个运行时文本控件
        /// </summary>
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

        /// <summary>
        /// 创建一个简单的运行时横向填充进度条
        /// </summary>
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

        /// <summary>
        /// 批量切换升级相关 HUD 控件的显隐
        /// </summary>
        private void SetProgressionWidgetsVisible(bool isVisible)
        {
            SetWidgetVisible(_levelText, isVisible);
            SetWidgetVisible(_experienceText, isVisible);
            SetProgressBarVisible(_experienceProgressFillImage, isVisible);
        }

        /// <summary>
        /// 切换单个控件的显隐状态
        /// </summary>
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

        /// <summary>
        /// 切换经验条显示状态
        /// 若填充图有独立背景，则连同背景一起切换
        /// </summary>
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
