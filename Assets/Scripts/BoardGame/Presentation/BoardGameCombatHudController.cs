using System.Collections.Generic;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 运行时战斗信息面板
    /// 展示当前焦点 AI 所在战斗的整队对敌信息、轻量飘字和滚动战报
    /// </summary>
    public sealed class BoardGameCombatHudController : MonoBehaviour
    {
        private const int MaxLogLineCount = 10;
        private const float OverlayFadeSpeed = 9f;
        private const float ImmediateFillSpeed = 10f;
        private const float FloatingTextLifetimeSeconds = 1.1f;
        private const float FloatingTextRiseSpeed = 54f;

        private static readonly string[] SquadAttackTemplates =
        {
            "{0} led a coordinated burst for {1} damage.",
            "{0} opened a firing lane and stripped {1} HP.",
            "{0} pressed the squad advantage, dealing {1} damage.",
            "{0} broke the hostile guard for {1} damage.",
            "{0} called the volley clean. Enemy lost {1} HP."
        };

        private static readonly string[] EnemyAttackTemplates =
        {
            "{0} countered hard. Squad took {1} damage.",
            "{0} answered with suppressive fire for {1} damage.",
            "{0} found an opening and hit the squad for {1}.",
            "{0} pushed back, forcing {1} damage onto the squad.",
            "{0} snapped into range and dealt {1} damage."
        };

        private static readonly string[] HealingTemplates =
        {
            "{0} used a field patch. Squad recovered {1} HP.",
            "{0} stabilized the line and restored {1} HP.",
            "{0} burned a med supply for {1} healing."
        };

        private static readonly string[] VictoryTemplates =
        {
            "{0} collapsed under the pressure.",
            "{0} is neutralized. The lane is clear.",
            "{0} was overwhelmed. Combat zone secured."
        };

        private static readonly string[] BossIntroTemplates =
        {
            "Boss signature acquired. All units brace.",
            "Boss contact confirmed. Hold formation.",
            "Heavy hostile detected. Squad is committing."
        };

        private static readonly string[] EnemyIntroTemplates =
        {
            "Hostile contact confirmed. Squad entering combat.",
            "Enemy presence locked. Friendly squad pushing in.",
            "Combat channel open. Friendly squad engaging."
        };

        [Header("Root")]
        [SerializeField] private GameObject _overlayRoot;
        private CanvasGroup _overlayCanvasGroup;
        private TMP_Text _headerText;
        private TMP_Text _subHeaderText;

        [Header("Friendly")]
        [SerializeField] private TMP_Text _friendlyAttackText;
        [SerializeField] private TMP_Text _friendlyDefenseText;
        [SerializeField] private Image _friendlyHealthFillImage;
        [SerializeField] private RectTransform _friendlyFloatingAnchor;
        private TMP_Text _friendlyTitleText;
        private TMP_Text _friendlySubtitleText;
        private TMP_Text _friendlyHealthText;

        [Header("Enemy")]
        [SerializeField] private TMP_Text _enemyAttackText;
        [SerializeField] private TMP_Text _enemyDefenseText;
        [SerializeField] private Image _enemyHealthFillImage;
        [SerializeField] private RectTransform _enemyFloatingAnchor;
        private TMP_Text _enemyTitleText;
        private TMP_Text _enemySubtitleText;
        private TMP_Text _enemyHealthText;

        [Header("Combat Log")]
        [SerializeField] private RectTransform _combatLogContainer;
        [SerializeField] private TextMeshProUGUI _combatLogTemplateText;

        private readonly List<TMP_Text> _logLineTexts = new List<TMP_Text>();
        private readonly List<string> _logLines = new List<string>();
        private readonly List<FloatingCombatTextEntry> _floatingTextEntries = new List<FloatingCombatTextEntry>();

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private Canvas _parentCanvas;
        private CombatSideWidgets _friendlyWidgets;
        private CombatSideWidgets _enemyWidgets;

        private CombatSnapshot _currentSnapshot;
        private string _activeEncounterKey = string.Empty;

        private float _friendlyDisplayedFill = 1f;
        private float _enemyDisplayedFill = 1f;
        private float _targetOverlayAlpha;
        private int _messageTemplateCursor;

        /// <summary>
        /// 绑定只读运行时查询，并准备运行时战斗 UI
        /// </summary>
        public void Bind(BoardGameRuntimeQueryController runtimeQueryController, Canvas parentCanvas)
        {
            if (_runtimeQueryController != null)
            {
                _runtimeQueryController.Changed -= Refresh;
            }

            _runtimeQueryController = runtimeQueryController;
            _parentCanvas = parentCanvas;
            EnsureRuntimeUi();

            if (_runtimeQueryController != null)
            {
                _runtimeQueryController.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_runtimeQueryController != null)
            {
                _runtimeQueryController.Changed -= Refresh;
            }
        }

        /// <summary>
        /// 推进面板透明度、血条缓动和飘字动画
        /// </summary>
        private void Update()
        {
            if (_overlayRoot == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            TickOverlayVisibility(deltaTime);
            TickHealthBarAnimation(deltaTime);
            TickFloatingTexts(deltaTime);
        }

        /// <summary>
        /// 根据当前焦点 AI 的战斗状态刷新面板
        /// </summary>
        private void Refresh()
        {
            if (_runtimeQueryController == null)
            {
                return;
            }

            EnsureRuntimeUi();

            CombatSnapshot nextSnapshot = BuildFocusedCombatSnapshot();

            if (nextSnapshot == null)
            {
                _currentSnapshot = null;
                _activeEncounterKey = string.Empty;
                _targetOverlayAlpha = 0f;
                return;
            }

            if (_overlayRoot != null && !_overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(true);
            }

            _overlayRoot.transform.SetAsLastSibling();
            _targetOverlayAlpha = 1f;

            bool isNewEncounter = _currentSnapshot == null || _activeEncounterKey != nextSnapshot.EncounterKey;

            if (isNewEncounter)
            {
                _logLines.Clear();
                ResetDisplayedHealth(nextSnapshot);
                AppendLogLine(nextSnapshot.IsBoss
                    ? GetNextTemplate(BossIntroTemplates)
                    : GetNextTemplate(EnemyIntroTemplates));
                AppendLogLine(nextSnapshot.SquadRosterText);
            }
            else
            {
                AppendLogsFromSnapshotDiff(_currentSnapshot, nextSnapshot);
            }

            _activeEncounterKey = nextSnapshot.EncounterKey;
            _currentSnapshot = nextSnapshot;
            ApplySnapshotToUi(nextSnapshot);
        }

        /// <summary>
        /// 从当前聚焦 AI 推导战斗展示快照
        /// 只在焦点 AI 处于战斗动作时返回有效结果
        /// </summary>
        private CombatSnapshot BuildFocusedCombatSnapshot()
        {
            BoardAgentState focusedAgentState = _runtimeQueryController.GetFocusedAgentState();

            if (focusedAgentState == null)
            {
                return null;
            }

            bool isBossFight = focusedAgentState.CurrentActionType == BoardActionType.FightingBoss;
            bool isEnemyFight = focusedAgentState.CurrentActionType == BoardActionType.FightingEnemy;

            if (!isBossFight && !isEnemyFight)
            {
                return null;
            }

            if (string.IsNullOrEmpty(focusedAgentState.CurrentNodeId))
            {
                return null;
            }

            BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(focusedAgentState.CurrentNodeId);

            if (nodeState == null)
            {
                return null;
            }

            bool isBoss = isBossFight;
            List<string> participantNames = new List<string>();
            List<string> aliveParticipantNames = new List<string>();
            int squadCurrentHealth = 0;
            int squadMaxHealth = 0;
            int squadAttack = 0;
            int squadDefenseTotal = 0;
            int squadTotalCount = 0;
            int squadAliveCount = 0;

            IReadOnlyList<BoardAgentState> agentStates = _runtimeQueryController.GetAgentStates();

            for (int index = 0; index < agentStates.Count; index++)
            {
                BoardAgentState agentState = agentStates[index];

                if (agentState == null ||
                    agentState.HasExtracted ||
                    agentState.CurrentNodeId != nodeState.NodeId)
                {
                    continue;
                }

                squadTotalCount++;
                participantNames.Add(agentState.DisplayName);
                squadCurrentHealth += agentState.CurrentHealth;
                squadMaxHealth += agentState.MaxHealth;

                if (!agentState.IsAlive)
                {
                    continue;
                }

                squadAliveCount++;
                squadAttack += agentState.Attack;
                squadDefenseTotal += agentState.Defense;
                aliveParticipantNames.Add(agentState.DisplayName);
            }

            if (squadTotalCount <= 0)
            {
                return null;
            }

            int squadDefenseAverage = squadAliveCount > 0
                ? Mathf.RoundToInt((float)squadDefenseTotal / squadAliveCount)
                : 0;
            int enemyCurrentHealth = isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int enemyMaxHealth = Mathf.Max(1, isBoss ? nodeState.BossMaxHealth : nodeState.EnemyMaxHealth);
            int enemyAttack = isBoss ? nodeState.BossAttack : nodeState.EnemyAttack;
            int enemyDefense = isBoss ? nodeState.BossDefense : nodeState.EnemyDefense;
            string enemyLabel = isBoss
                ? "Boss Target"
                : $"{BoardGameTypes.GetDangerLabel(nodeState.DangerTier)} Threat Hostile";
            string rosterText = participantNames.Count > 0
                ? $"Squad online: {string.Join(" / ", participantNames)}"
                : "Squad online";

            return new CombatSnapshot(
                $"{nodeState.NodeId}_{(isBoss ? "boss" : "enemy")}",
                nodeState.NodeId,
                isBoss,
                rosterText,
                squadCurrentHealth,
                Mathf.Max(1, squadMaxHealth),
                squadAttack,
                squadDefenseAverage,
                squadTotalCount,
                squadAliveCount,
                enemyCurrentHealth,
                enemyMaxHealth,
                enemyAttack,
                enemyDefense,
                enemyLabel,
                participantNames,
                aliveParticipantNames);
        }

        /// <summary>
        /// 根据前后两帧快照差分生成战报和飘字
        /// </summary>
        private void AppendLogsFromSnapshotDiff(CombatSnapshot previousSnapshot, CombatSnapshot nextSnapshot)
        {
            if (previousSnapshot == null || nextSnapshot == null)
            {
                return;
            }

            if (nextSnapshot.SquadTotalCount > previousSnapshot.SquadTotalCount)
            {
                AppendLogLine("Another squadmate reached the frontline.");
            }

            if (nextSnapshot.SquadAliveCount < previousSnapshot.SquadAliveCount)
            {
                AppendLogLine("One squadmate is down. Pressure is rising.");
            }

            int enemyDamage = Mathf.Max(0, previousSnapshot.EnemyCurrentHealth - nextSnapshot.EnemyCurrentHealth);

            if (enemyDamage > 0)
            {
                SpawnFloatingText(
                    _enemyWidgets != null ? _enemyWidgets.FloatingAnchor : null,
                    $"-{enemyDamage}",
                    new Color(1f, 0.83f, 0.29f, 0.98f));
                AppendLogLine(BuildSquadAttackLog(nextSnapshot, enemyDamage));
            }

            int squadDamage = Mathf.Max(0, previousSnapshot.SquadCurrentHealth - nextSnapshot.SquadCurrentHealth);

            if (squadDamage > 0)
            {
                SpawnFloatingText(
                    _friendlyWidgets != null ? _friendlyWidgets.FloatingAnchor : null,
                    $"-{squadDamage}",
                    new Color(1f, 0.39f, 0.35f, 0.98f));
                AppendLogLine(BuildEnemyAttackLog(nextSnapshot, squadDamage));
            }

            int squadHealing = Mathf.Max(0, nextSnapshot.SquadCurrentHealth - previousSnapshot.SquadCurrentHealth);

            if (squadHealing > 0)
            {
                SpawnFloatingText(
                    _friendlyWidgets != null ? _friendlyWidgets.FloatingAnchor : null,
                    $"+{squadHealing}",
                    new Color(0.4f, 0.94f, 0.58f, 0.98f));
                AppendLogLine(BuildHealingLog(nextSnapshot, squadHealing));
            }

            if (previousSnapshot.EnemyCurrentHealth > 0 && nextSnapshot.EnemyCurrentHealth <= 0)
            {
                AppendLogLine(string.Format(GetNextTemplate(VictoryTemplates), nextSnapshot.EnemyTitleText));
            }
        }

        /// <summary>
        /// 把快照内容映射到 UI 控件
        /// </summary>
        private void ApplySnapshotToUi(CombatSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (_headerText != null)
            {
                _headerText.text = snapshot.IsBoss
                    ? $"Boss Engagement  Node {snapshot.NodeId}"
                    : $"Squad Engagement  Node {snapshot.NodeId}";
            }

            if (_subHeaderText != null)
            {
                _subHeaderText.text = $"Focus-triggered view  {snapshot.SquadAliveCount}/{snapshot.SquadTotalCount} active  Combat log updates in real time";
            }

            ApplySideWidgets(
                _friendlyWidgets,
                "Friendly Squad",
                snapshot.SquadTitleText,
                snapshot.SquadHealthText,
                $"ATK  {snapshot.SquadAttack}",
                $"DEF(avg)  {snapshot.SquadDefenseAverage}");

            ApplySideWidgets(
                _enemyWidgets,
                snapshot.EnemyTitleText,
                snapshot.EnemySubtitleText,
                snapshot.EnemyHealthText,
                $"ATK  {snapshot.EnemyAttack}",
                $"DEF  {snapshot.EnemyDefense}");

            RefreshLogTexts();
        }

        /// <summary>
        /// 构建整套运行时战斗 UI
        /// </summary>
        private void EnsureRuntimeUi()
        {
            Canvas canvas = _parentCanvas;

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(
                    "BoardGameCombatCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            if (_overlayRoot == null)
            {
                _overlayRoot = gameObject;
            }

            RectTransform overlayRect = _overlayRoot.GetComponent<RectTransform>();

            if (overlayRect == null)
            {
                overlayRect = _overlayRoot.AddComponent<RectTransform>();
            }

            overlayRect.SetParent(canvas.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.SetAsLastSibling();

            if (_overlayCanvasGroup == null)
            {
                _overlayCanvasGroup = _overlayRoot.GetComponent<CanvasGroup>();
            }

            if (_overlayCanvasGroup == null)
            {
                _overlayCanvasGroup = _overlayRoot.AddComponent<CanvasGroup>();
            }

            _overlayCanvasGroup.alpha = 0f;
            _overlayCanvasGroup.interactable = false;
            _overlayCanvasGroup.blocksRaycasts = false;

            if (HasManualUiBindings())
            {
                BindManualUiReferences();
                _overlayRoot.SetActive(false);
                return;
            }

            if (_friendlyWidgets != null &&
                _enemyWidgets != null &&
                _logLineTexts.Count == MaxLogLineCount)
            {
                _overlayRoot.SetActive(false);
                return;
            }

            if (transform.childCount == 0)
            {
                GameObject backdropObject = CreatePanel(
                    "Backdrop",
                    _overlayRoot.transform,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                Image backdropImage = backdropObject.GetComponent<Image>();
                backdropImage.color = new Color(0.02f, 0.04f, 0.07f, 0.18f);
                RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;

                GameObject windowObject = CreatePanel(
                    "Window",
                    _overlayRoot.transform,
                    new Vector2(0f, -40f),
                    new Vector2(1320f, 620f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                Image windowImage = windowObject.GetComponent<Image>();
                windowImage.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

                _headerText = CreateText(
                    "Header",
                    windowObject.transform,
                    new Vector2(0f, 266f),
                    new Vector2(1180f, 42f),
                    34f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center);

                _subHeaderText = CreateText(
                    "SubHeader",
                    windowObject.transform,
                    new Vector2(0f, 226f),
                    new Vector2(1180f, 28f),
                    18f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Center);
                _subHeaderText.color = new Color(0.74f, 0.81f, 0.89f, 0.92f);

                _friendlyWidgets = CreateSidePanel(
                    "FriendlyPanel",
                    windowObject.transform,
                    new Vector2(-318f, 76f),
                    new Color(0.22f, 0.77f, 0.52f, 1f),
                    "Friendly Squad",
                    "AI");

                _enemyWidgets = CreateSidePanel(
                    "EnemyPanel",
                    windowObject.transform,
                    new Vector2(318f, 76f),
                    new Color(0.94f, 0.33f, 0.27f, 1f),
                    "Enemy Contact",
                    "EN");

                TMP_Text versusText = CreateText(
                    "Versus",
                    windowObject.transform,
                    new Vector2(0f, 72f),
                    new Vector2(120f, 120f),
                    42f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center);
                versusText.text = "VS";
                versusText.color = new Color(0.96f, 0.92f, 0.68f, 0.98f);

                GameObject logPanelObject = CreatePanel(
                    "CombatLogPanel",
                    windowObject.transform,
                    new Vector2(0f, -170f),
                    new Vector2(1160f, 240f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                Image logPanelImage = logPanelObject.GetComponent<Image>();
                logPanelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.96f);

                GameObject logTitleObject = new GameObject("LogTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                logTitleObject.transform.SetParent(logPanelObject.transform, false);
                TMP_Text logTitleText = logTitleObject.GetComponent<TextMeshProUGUI>();
                logTitleText.font = TMP_Settings.defaultFontAsset;
                logTitleText.fontSize = 18f;
                logTitleText.fontStyle = FontStyles.Bold;
                logTitleText.alignment = TextAlignmentOptions.Left;
                logTitleText.color = new Color(0.93f, 0.95f, 0.98f, 0.94f);
                logTitleText.text = "Combat Feed";
                RectTransform logTitleRect = logTitleObject.GetComponent<RectTransform>();
                logTitleRect.anchorMin = new Vector2(0f, 1f);
                logTitleRect.anchorMax = new Vector2(1f, 1f);
                logTitleRect.pivot = new Vector2(0.5f, 1f);
                logTitleRect.anchoredPosition = new Vector2(0f, -16f);
                logTitleRect.offsetMin = new Vector2(24f, 0f);
                logTitleRect.offsetMax = new Vector2(-24f, 0f);
                logTitleRect.sizeDelta = new Vector2(0f, 24f);

                GameObject logContainerObject = new GameObject(
                    "LogLines",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                logContainerObject.transform.SetParent(logPanelObject.transform, false);
                RectTransform logContainerRect = logContainerObject.GetComponent<RectTransform>();
                logContainerRect.anchorMin = new Vector2(0f, 0f);
                logContainerRect.anchorMax = new Vector2(1f, 1f);
                logContainerRect.offsetMin = new Vector2(24f, 18f);
                logContainerRect.offsetMax = new Vector2(-24f, -48f);

                VerticalLayoutGroup layoutGroup = logContainerObject.GetComponent<VerticalLayoutGroup>();
                layoutGroup.spacing = 4f;
                layoutGroup.padding = new RectOffset(0, 0, 0, 0);
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = false;

                ContentSizeFitter contentSizeFitter = logContainerObject.GetComponent<ContentSizeFitter>();
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                _logLineTexts.Clear();

                for (int index = 0; index < MaxLogLineCount; index++)
                {
                    TMP_Text logLineText = CreateText(
                        $"LogLine{index + 1}",
                        logContainerObject.transform,
                        Vector2.zero,
                        new Vector2(0f, 18f),
                        18f,
                        FontStyles.Normal,
                        TextAlignmentOptions.Left);
                    RectTransform logLineRect = logLineText.rectTransform;
                    logLineRect.anchorMin = new Vector2(0f, 1f);
                    logLineRect.anchorMax = new Vector2(1f, 1f);
                    logLineRect.pivot = new Vector2(0.5f, 1f);
                    logLineRect.sizeDelta = new Vector2(0f, 18f);
                    _logLineTexts.Add(logLineText);
                }
            }

            _overlayRoot.SetActive(false);
        }

        private bool HasManualUiBindings()
        {
            return _friendlyAttackText != null ||
                   _friendlyDefenseText != null ||
                   _friendlyHealthFillImage != null ||
                   _friendlyFloatingAnchor != null ||
                   _enemyAttackText != null ||
                   _enemyDefenseText != null ||
                   _enemyHealthFillImage != null ||
                   _enemyFloatingAnchor != null ||
                   _combatLogContainer != null ||
                   _combatLogTemplateText != null;
        }

        private void BindManualUiReferences()
        {
            _friendlyWidgets = new CombatSideWidgets(
                _friendlyTitleText,
                _friendlySubtitleText,
                _friendlyHealthText,
                _friendlyAttackText,
                _friendlyDefenseText,
                _friendlyHealthFillImage,
                _friendlyFloatingAnchor);

            _enemyWidgets = new CombatSideWidgets(
                _enemyTitleText,
                _enemySubtitleText,
                _enemyHealthText,
                _enemyAttackText,
                _enemyDefenseText,
                _enemyHealthFillImage,
                _enemyFloatingAnchor);

            EnsureManualLogTexts();
        }

        private void EnsureManualLogTexts()
        {
            if (_combatLogContainer == null || _combatLogTemplateText == null)
            {
                return;
            }

            if (_logLineTexts.Count == MaxLogLineCount)
            {
                return;
            }

            _logLineTexts.Clear();

            if (_combatLogTemplateText.transform.parent != _combatLogContainer)
            {
                _combatLogTemplateText.transform.SetParent(_combatLogContainer, false);
            }

            _combatLogTemplateText.gameObject.SetActive(true);
            _combatLogTemplateText.name = "LogLine1";
            _logLineTexts.Add(_combatLogTemplateText);

            for (int index = 1; index < MaxLogLineCount; index++)
            {
                TextMeshProUGUI clone = Instantiate(_combatLogTemplateText, _combatLogContainer);
                clone.name = $"LogLine{index + 1}";
                clone.text = string.Empty;
                _logLineTexts.Add(clone);
            }
        }

        /// <summary>
        /// 构建单侧战斗信息卡片
        /// </summary>
        private CombatSideWidgets CreateSidePanel(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Color accentColor,
            string title,
            string iconFallbackLabel)
        {
            GameObject panelObject = CreatePanel(
                objectName,
                parent,
                anchoredPosition,
                new Vector2(462f, 272f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(accentColor.r * 0.22f, accentColor.g * 0.22f, accentColor.b * 0.22f, 0.96f);

            TMP_Text titleText = CreateText(
                "Title",
                panelObject.transform,
                new Vector2(44f, 92f),
                new Vector2(240f, 30f),
                26f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            titleText.text = title;

            TMP_Text subtitleText = CreateText(
                "Subtitle",
                panelObject.transform,
                new Vector2(44f, 54f),
                new Vector2(260f, 44f),
                17f,
                FontStyles.Normal,
                TextAlignmentOptions.Left);
            subtitleText.color = new Color(0.78f, 0.84f, 0.92f, 0.92f);

            GameObject iconPlateObject = CreatePanel(
                "IconPlate",
                panelObject.transform,
                new Vector2(-144f, 54f),
                new Vector2(118f, 118f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Image iconPlateImage = iconPlateObject.GetComponent<Image>();
            iconPlateImage.color = accentColor;

            TMP_Text iconFallbackText = CreateText(
                "IconFallback",
                iconPlateObject.transform,
                Vector2.zero,
                new Vector2(90f, 90f),
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            iconFallbackText.color = new Color(0.04f, 0.06f, 0.08f, 0.9f);
            iconFallbackText.text = iconFallbackLabel;

            TMP_Text healthText = CreateText(
                "Health",
                panelObject.transform,
                new Vector2(0f, 6f),
                new Vector2(376f, 28f),
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);

            Image healthFillImage = CreateHealthBar(
                "HealthBar",
                panelObject.transform,
                new Vector2(0f, -28f),
                new Vector2(376f, 18f),
                accentColor);

            TMP_Text attackText = CreateStatText(
                "Attack",
                panelObject.transform,
                new Vector2(-82f, -76f),
                accentColor);

            TMP_Text defenseText = CreateStatText(
                "Defense",
                panelObject.transform,
                new Vector2(108f, -76f),
                accentColor);

            GameObject floatingAnchorObject = new GameObject("FloatingAnchor", typeof(RectTransform));
            floatingAnchorObject.transform.SetParent(panelObject.transform, false);
            RectTransform floatingAnchorRect = floatingAnchorObject.GetComponent<RectTransform>();
            floatingAnchorRect.anchorMin = new Vector2(0.5f, 0.5f);
            floatingAnchorRect.anchorMax = new Vector2(0.5f, 0.5f);
            floatingAnchorRect.pivot = new Vector2(0.5f, 0.5f);
            floatingAnchorRect.anchoredPosition = new Vector2(0f, 10f);
            floatingAnchorRect.sizeDelta = new Vector2(340f, 130f);

            return new CombatSideWidgets(
                titleText,
                subtitleText,
                healthText,
                attackText,
                defenseText,
                healthFillImage,
                floatingAnchorRect);
        }

        private static Image CreateHealthBar(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color accentColor)
        {
            GameObject backgroundObject = CreatePanel(
                objectName,
                parent,
                anchoredPosition,
                sizeDelta,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0.11f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backgroundObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = accentColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            return fillImage;
        }

        private static TMP_Text CreateStatText(string objectName, Transform parent, Vector2 anchoredPosition, Color accentColor)
        {
            TMP_Text text = CreateText(
                objectName,
                parent,
                anchoredPosition,
                new Vector2(154f, 28f),
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            text.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.98f);
            return text;
        }

        private static GameObject CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            return panelObject;
        }

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
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private void ApplySideWidgets(
            CombatSideWidgets widgets,
            string title,
            string subtitle,
            string healthText,
            string attackText,
            string defenseText)
        {
            if (widgets == null)
            {
                return;
            }

            if (widgets.TitleText != null)
            {
                widgets.TitleText.text = title;
            }

            if (widgets.SubtitleText != null)
            {
                widgets.SubtitleText.text = subtitle;
            }

            if (widgets.HealthText != null)
            {
                widgets.HealthText.text = healthText;
            }

            if (widgets.AttackText != null)
            {
                widgets.AttackText.text = attackText;
            }

            if (widgets.DefenseText != null)
            {
                widgets.DefenseText.text = defenseText;
            }
        }

        private void TickOverlayVisibility(float deltaTime)
        {
            if (_overlayCanvasGroup == null || _overlayRoot == null)
            {
                return;
            }

            if (_targetOverlayAlpha > 0f && !_overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(true);
            }

            _overlayCanvasGroup.alpha = Mathf.MoveTowards(
                _overlayCanvasGroup.alpha,
                _targetOverlayAlpha,
                deltaTime * OverlayFadeSpeed);

            if (_overlayCanvasGroup.alpha <= 0.001f && _targetOverlayAlpha <= 0f && _overlayRoot.activeSelf)
            {
                _overlayRoot.SetActive(false);
            }
        }

        private void TickHealthBarAnimation(float deltaTime)
        {
            if (_currentSnapshot == null)
            {
                return;
            }

            float friendlyTarget = _currentSnapshot.SquadCurrentHealth01;
            float enemyTarget = _currentSnapshot.EnemyCurrentHealth01;

            _friendlyDisplayedFill = Mathf.MoveTowards(_friendlyDisplayedFill, friendlyTarget, deltaTime * ImmediateFillSpeed);
            _enemyDisplayedFill = Mathf.MoveTowards(_enemyDisplayedFill, enemyTarget, deltaTime * ImmediateFillSpeed);

            if (_friendlyWidgets != null && _friendlyWidgets.HealthFillImage != null)
            {
                _friendlyWidgets.HealthFillImage.fillAmount = _friendlyDisplayedFill;
            }

            if (_enemyWidgets != null && _enemyWidgets.HealthFillImage != null)
            {
                _enemyWidgets.HealthFillImage.fillAmount = _enemyDisplayedFill;
            }
        }

        private void ResetDisplayedHealth(CombatSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _friendlyDisplayedFill = snapshot.SquadCurrentHealth01;
            _enemyDisplayedFill = snapshot.EnemyCurrentHealth01;

            if (_friendlyWidgets != null && _friendlyWidgets.HealthFillImage != null)
            {
                _friendlyWidgets.HealthFillImage.fillAmount = _friendlyDisplayedFill;
            }

            if (_enemyWidgets != null && _enemyWidgets.HealthFillImage != null)
            {
                _enemyWidgets.HealthFillImage.fillAmount = _enemyDisplayedFill;
            }
        }

        private void SpawnFloatingText(RectTransform anchor, string valueText, Color color)
        {
            if (anchor == null)
            {
                return;
            }

            TMP_Text floatingText = CreateText(
                $"FloatingText_{_floatingTextEntries.Count + 1}",
                anchor,
                new Vector2(Random.Range(-44f, 44f), Random.Range(-18f, 18f)),
                new Vector2(160f, 34f),
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            floatingText.text = valueText;
            floatingText.color = color;

            _floatingTextEntries.Add(new FloatingCombatTextEntry(
                floatingText,
                floatingText.rectTransform.anchoredPosition,
                color));
        }

        private void TickFloatingTexts(float deltaTime)
        {
            for (int index = _floatingTextEntries.Count - 1; index >= 0; index--)
            {
                FloatingCombatTextEntry entry = _floatingTextEntries[index];

                if (entry == null || entry.Label == null)
                {
                    _floatingTextEntries.RemoveAt(index);
                    continue;
                }

                entry.ElapsedSeconds += deltaTime;
                float progress01 = Mathf.Clamp01(entry.ElapsedSeconds / FloatingTextLifetimeSeconds);
                Vector2 anchoredPosition = entry.StartAnchoredPosition + Vector2.up * (FloatingTextRiseSpeed * progress01);
                entry.Label.rectTransform.anchoredPosition = anchoredPosition;

                Color color = entry.BaseColor;
                color.a *= 1f - progress01;
                entry.Label.color = color;

                if (entry.ElapsedSeconds < FloatingTextLifetimeSeconds)
                {
                    continue;
                }

                Destroy(entry.Label.gameObject);
                _floatingTextEntries.RemoveAt(index);
            }
        }

        private void AppendLogLine(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _logLines.Add(message);

            if (_logLines.Count > MaxLogLineCount)
            {
                _logLines.RemoveAt(0);
            }

            RefreshLogTexts();
        }

        private void RefreshLogTexts()
        {
            for (int index = 0; index < _logLineTexts.Count; index++)
            {
                TMP_Text lineText = _logLineTexts[index];

                if (lineText == null)
                {
                    continue;
                }

                if (index >= _logLines.Count)
                {
                    lineText.text = string.Empty;
                    continue;
                }

                lineText.text = _logLines[index];
                float emphasis = Mathf.InverseLerp(0f, Mathf.Max(1f, _logLines.Count - 1f), index);
                lineText.color = Color.Lerp(
                    new Color(0.62f, 0.7f, 0.8f, 0.58f),
                    new Color(0.95f, 0.97f, 0.99f, 0.98f),
                    emphasis);
            }
        }

        private string BuildSquadAttackLog(CombatSnapshot snapshot, int damageValue)
        {
            string actorName = ChooseFriendlyActor(snapshot);
            return string.Format(GetNextTemplate(SquadAttackTemplates), actorName, damageValue);
        }

        private string BuildEnemyAttackLog(CombatSnapshot snapshot, int damageValue)
        {
            return string.Format(GetNextTemplate(EnemyAttackTemplates), snapshot.EnemyTitleText, damageValue);
        }

        private string BuildHealingLog(CombatSnapshot snapshot, int healValue)
        {
            string actorName = ChooseFriendlyActor(snapshot);
            return string.Format(GetNextTemplate(HealingTemplates), actorName, healValue);
        }

        private string ChooseFriendlyActor(CombatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AliveParticipantNames.Count == 0)
            {
                return "The squad";
            }

            int nameIndex = _messageTemplateCursor % snapshot.AliveParticipantNames.Count;
            return snapshot.AliveParticipantNames[nameIndex];
        }

        private string GetNextTemplate(string[] templates)
        {
            if (templates == null || templates.Length == 0)
            {
                return string.Empty;
            }

            int templateIndex = _messageTemplateCursor % templates.Length;
            _messageTemplateCursor++;
            return templates[templateIndex];
        }

        private sealed class CombatSnapshot
        {
            public CombatSnapshot(
                string encounterKey,
                string nodeId,
                bool isBoss,
                string squadRosterText,
                int squadCurrentHealth,
                int squadMaxHealth,
                int squadAttack,
                int squadDefenseAverage,
                int squadTotalCount,
                int squadAliveCount,
                int enemyCurrentHealth,
                int enemyMaxHealth,
                int enemyAttack,
                int enemyDefense,
                string enemyTitleText,
                List<string> participantNames,
                List<string> aliveParticipantNames)
            {
                EncounterKey = encounterKey;
                NodeId = nodeId;
                IsBoss = isBoss;
                SquadRosterText = squadRosterText ?? string.Empty;
                SquadCurrentHealth = Mathf.Max(0, squadCurrentHealth);
                SquadMaxHealth = Mathf.Max(1, squadMaxHealth);
                SquadAttack = Mathf.Max(0, squadAttack);
                SquadDefenseAverage = Mathf.Max(0, squadDefenseAverage);
                SquadTotalCount = Mathf.Max(0, squadTotalCount);
                SquadAliveCount = Mathf.Clamp(squadAliveCount, 0, SquadTotalCount);
                EnemyCurrentHealth = Mathf.Max(0, enemyCurrentHealth);
                EnemyMaxHealth = Mathf.Max(1, enemyMaxHealth);
                EnemyAttack = Mathf.Max(0, enemyAttack);
                EnemyDefense = Mathf.Max(0, enemyDefense);
                EnemyTitleText = string.IsNullOrEmpty(enemyTitleText) ? "Enemy Contact" : enemyTitleText;
                ParticipantNames = participantNames ?? new List<string>();
                AliveParticipantNames = aliveParticipantNames ?? new List<string>();
            }

            public string EncounterKey { get; }
            public string NodeId { get; }
            public bool IsBoss { get; }
            public string SquadRosterText { get; }
            public int SquadCurrentHealth { get; }
            public int SquadMaxHealth { get; }
            public int SquadAttack { get; }
            public int SquadDefenseAverage { get; }
            public int SquadTotalCount { get; }
            public int SquadAliveCount { get; }
            public int EnemyCurrentHealth { get; }
            public int EnemyMaxHealth { get; }
            public int EnemyAttack { get; }
            public int EnemyDefense { get; }
            public string EnemyTitleText { get; }
            public List<string> ParticipantNames { get; }
            public List<string> AliveParticipantNames { get; }
            public float SquadCurrentHealth01 => SquadMaxHealth <= 0 ? 0f : (float)SquadCurrentHealth / SquadMaxHealth;
            public float EnemyCurrentHealth01 => EnemyMaxHealth <= 0 ? 0f : (float)EnemyCurrentHealth / EnemyMaxHealth;
            public string SquadTitleText => $"Units in node  {SquadAliveCount}/{SquadTotalCount} combat-ready";
            public string SquadHealthText => $"HP  {SquadCurrentHealth}/{SquadMaxHealth}";
            public string EnemySubtitleText => IsBoss ? "High priority hostile" : "Shared node encounter";
            public string EnemyHealthText => $"HP  {EnemyCurrentHealth}/{EnemyMaxHealth}";
        }

        private sealed class CombatSideWidgets
        {
            public CombatSideWidgets(
                TMP_Text titleText,
                TMP_Text subtitleText,
                TMP_Text healthText,
                TMP_Text attackText,
                TMP_Text defenseText,
                Image healthFillImage,
                RectTransform floatingAnchor)
            {
                TitleText = titleText;
                SubtitleText = subtitleText;
                HealthText = healthText;
                AttackText = attackText;
                DefenseText = defenseText;
                HealthFillImage = healthFillImage;
                FloatingAnchor = floatingAnchor;
            }

            public TMP_Text TitleText { get; }
            public TMP_Text SubtitleText { get; }
            public TMP_Text HealthText { get; }
            public TMP_Text AttackText { get; }
            public TMP_Text DefenseText { get; }
            public Image HealthFillImage { get; }
            public RectTransform FloatingAnchor { get; }
        }

        private sealed class FloatingCombatTextEntry
        {
            public FloatingCombatTextEntry(TMP_Text label, Vector2 startAnchoredPosition, Color baseColor)
            {
                Label = label;
                StartAnchoredPosition = startAnchoredPosition;
                BaseColor = baseColor;
            }

            public TMP_Text Label { get; }
            public Vector2 StartAnchoredPosition { get; }
            public Color BaseColor { get; }
            public float ElapsedSeconds { get; set; }
        }
    }
}
