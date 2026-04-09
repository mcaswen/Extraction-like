using BoardGame.Runtime;
using BoardGame.Runtime.State;
using BoardGame.Presentation;
using TMPro;
using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// AI 角色表现组件
    /// </summary>
    public sealed class BoardGameAgentView : MonoBehaviour
    {
        private static readonly Color SelectionHaloColor = new Color(0.22f, 0.62f, 1f, 1f);
        private const float CombatFloatingTextLifetimeSeconds = 0.95f;
        private const float CombatFloatingTextRiseDistance = 0.55f;

        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private TMP_Text _labelText;
        [Header("Combat Floating Text")]
        [SerializeField] private TMP_Text _combatFloatingTextTemplateText;
        [SerializeField] private RectTransform _combatFloatingTextAnchor;

        private string _agentId;
        private BoardGameSelectionHalo _selectionHalo;
        private readonly BoardGameFloatingTextPresenter _combatFloatingTextPresenter =
            new BoardGameFloatingTextPresenter(CombatFloatingTextLifetimeSeconds, CombatFloatingTextRiseDistance);
        private int _floatingTextSequenceId;

        public string AgentId => _agentId;

        private void Awake()
        {
            EnsureSelectionHalo();
            EnsureCombatFloatingTextAnchor();

            if (_combatFloatingTextTemplateText != null && _combatFloatingTextTemplateText.gameObject.activeSelf)
            {
                _combatFloatingTextTemplateText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            _combatFloatingTextPresenter.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 初始化 Agent 视图的静态身份信息
        /// </summary>
        public void Initialize(string agentId)
        {
            _agentId = agentId ?? string.Empty;
        }

        /// <summary>
        /// 刷新角色当前位置与动作展示
        /// </summary>
        public void Refresh(BoardAgentState agentState, Vector3 displayPosition, bool isSelected)
        {
            transform.position = displayPosition;

            _selectionHalo?.Refresh(isSelected, SelectionHaloColor);

            if (_labelText != null)
            {
                _labelText.text = isSelected
                    ? $"{agentState.DisplayName}\n{BuildFocusedStatusLabel(agentState)}"
                    : agentState.DisplayName;
            }

            if (_bodyRenderer != null)
            {
                _bodyRenderer.color = agentState.IsAlive
                    ? agentState.AgentColor
                    : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        public void HandleCombatHealthDelta(int healthDelta, BoardActionType currentActionType, BoardActionType previousActionType)
        {
            if (healthDelta == 0)
            {
                return;
            }

            if (!IsCombatAction(currentActionType) && !IsCombatAction(previousActionType))
            {
                return;
            }

            SpawnCombatFloatingText(healthDelta > 0 ? $"+{healthDelta}" : $"{healthDelta}");
        }

        private void SpawnCombatFloatingText(string valueText)
        {
            RectTransform anchor = _combatFloatingTextAnchor;
            TMP_Text floatingText = CreateCombatFloatingText(anchor);

            if (floatingText == null)
            {
                return;
            }

            Vector3 startLocalPosition = new Vector3(
                Random.Range(-0.18f, 0.18f),
                Random.Range(-0.04f, 0.06f),
                0f);
            floatingText.text = valueText;
            _combatFloatingTextPresenter.Add(floatingText, startLocalPosition, floatingText.color);
        }

        private TMP_Text CreateCombatFloatingText(RectTransform anchor)
        {
            TMP_Text templateText = _combatFloatingTextTemplateText != null ? _combatFloatingTextTemplateText : _labelText;

            if (templateText == null)
            {
                return null;
            }

            _floatingTextSequenceId += 1;
            TMP_Text floatingText = Instantiate(templateText, anchor);
            floatingText.gameObject.name = $"CombatFloatingText_{_floatingTextSequenceId}";
            floatingText.gameObject.SetActive(true);
            floatingText.raycastTarget = false;
            floatingText.transform.SetAsLastSibling();
            return floatingText;
        }

        /// <summary>
        /// 只有当前焦点 Agent 显示详细动作和进度
        /// 非焦点 Agent 只保留名字，避免多人同节点时信息过载
        /// </summary>
        private static string BuildFocusedStatusLabel(BoardAgentState agentState)
        {
            if (agentState == null)
            {
                return string.Empty;
            }

            string actionLabel = BoardGameTypes.GetActionLabel(agentState.CurrentActionType);

            if (agentState.CurrentActionType == BoardActionType.Idle ||
                agentState.CurrentActionType == BoardActionType.Completed ||
                agentState.CurrentActionType == BoardActionType.Downed)
            {
                return $"{actionLabel}  HP {agentState.CurrentHealth}/{agentState.MaxHealth}";
            }

            return $"{actionLabel} {Mathf.Clamp01(agentState.CurrentActionProgress):P0}  HP {agentState.CurrentHealth}/{agentState.MaxHealth}";
        }

        private static bool IsCombatAction(BoardActionType actionType)
        {
            return actionType == BoardActionType.FightingEnemy ||
                   actionType == BoardActionType.FightingBoss;
        }

        /// <summary>
        /// 自动创建角色蓝色高光外圈
        /// 不再依赖 prefab 额外配置选中渲染器
        /// </summary>
        private void EnsureSelectionHalo()
        {
            if (_bodyRenderer == null)
            {
                return;
            }

            Transform haloTransform = transform.Find("SelectionHalo");

            if (haloTransform == null)
            {
                GameObject haloObject = new GameObject("SelectionHalo");
                haloObject.transform.SetParent(transform, false);
                _selectionHalo = haloObject.AddComponent<BoardGameSelectionHalo>();
            }
            else
            {
                _selectionHalo = haloTransform.GetComponent<BoardGameSelectionHalo>();

                if (_selectionHalo == null)
                {
                    _selectionHalo = haloTransform.gameObject.AddComponent<BoardGameSelectionHalo>();
                }
            }

            _selectionHalo.Initialize(_bodyRenderer, 0.14f, 0.09f);
        }

        private void EnsureCombatFloatingTextAnchor()
        {
            if (_combatFloatingTextAnchor != null)
            {
                return;
            }

            RectTransform canvasRect = ResolveCombatCanvasRect();

            if (canvasRect == null)
            {
                return;
            }

            Transform existingAnchorTransform = canvasRect.Find("CombatFloatingTextAnchor");

            RectTransform anchorRect;

            if (existingAnchorTransform == null)
            {
                GameObject anchorObject = new GameObject("CombatFloatingTextAnchor", typeof(RectTransform));
                anchorObject.transform.SetParent(canvasRect, false);
                anchorRect = anchorObject.GetComponent<RectTransform>();
            }
            else
            {
                anchorRect = existingAnchorTransform as RectTransform;

                if (anchorRect == null)
                {
                    return;
                }
            }

            if (anchorRect.anchoredPosition == Vector2.zero &&
                anchorRect.sizeDelta == Vector2.zero)
            {
                anchorRect.anchorMin = new Vector2(0.5f, 0.5f);
                anchorRect.anchorMax = new Vector2(0.5f, 0.5f);
                anchorRect.pivot = new Vector2(0.5f, 0.5f);
                anchorRect.anchoredPosition = new Vector2(0f, ResolveDefaultFloatingTextHeight());
                anchorRect.sizeDelta = new Vector2(1f, 1f);
            }

            _combatFloatingTextAnchor = anchorRect;
        }

        private float ResolveDefaultFloatingTextHeight()
        {
            if (_bodyRenderer != null && _bodyRenderer.sprite != null)
            {
                return Mathf.Max(0.75f, _bodyRenderer.sprite.bounds.extents.y + 0.45f);
            }

            return 0.9f;
        }

        private RectTransform ResolveCombatCanvasRect()
        {
            if (_labelText is TextMeshProUGUI labelTextUi &&
                labelTextUi.rectTransform != null &&
                labelTextUi.rectTransform.parent is RectTransform labelParentRect)
            {
                return labelParentRect;
            }

            Canvas canvas = GetComponentInChildren<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : null;
        }
    }
}
