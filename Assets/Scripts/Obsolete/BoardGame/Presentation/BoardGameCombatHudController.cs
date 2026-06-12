using System.Collections.Generic;
using System;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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
        private const float ResolvedEncounterHoldSeconds = 1f;
        private static readonly Color CombatTextColor = new Color(0f, 0f, 0f, 0.98f);

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
        private TMP_Text _friendlyTitleText = null;
        private TMP_Text _friendlySubtitleText = null;
        private TMP_Text _friendlyHealthText = null;

        [Header("Enemy")]
        [SerializeField] private TMP_Text _enemyAttackText;
        [SerializeField] private TMP_Text _enemyDefenseText;
        [SerializeField] private Image _enemyHealthFillImage;
        [SerializeField] private RectTransform _enemyFloatingAnchor;
        [SerializeField] private Image _enemyIconImage;
        [SerializeField] private TMP_Text _enemyIconFallbackText;
        [SerializeField] private List<Sprite> _enemyIconSpriteOptions = new List<Sprite>();
        private TMP_Text _enemyTitleText = null;
        private TMP_Text _enemySubtitleText = null;
        private TMP_Text _enemyHealthText = null;

        [Header("Combat Log")]
        [SerializeField] private RectTransform _combatLogContainer;
        [SerializeField] private TextMeshProUGUI _combatLogTemplateText;

        [Header("Floating Text")]
        [SerializeField] private TextMeshProUGUI _floatingTextTemplateText;

        private readonly List<TMP_Text> _logLineTexts = new List<TMP_Text>();
        private readonly List<string> _logLines = new List<string>();
        private readonly BoardGameFloatingTextPresenter _floatingTextPresenter =
            new BoardGameFloatingTextPresenter(FloatingTextLifetimeSeconds, FloatingTextRiseSpeed);

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private Canvas _parentCanvas;
        private CombatSideWidgets _friendlyWidgets;
        private CombatSideWidgets _enemyWidgets;

        private CombatSnapshot _currentSnapshot;
        private string _activeEncounterKey = string.Empty;
        private string _activeEncounterIconKey = string.Empty;
        private Sprite _activeEncounterEnemyIcon;

        private float _friendlyDisplayedFill = 1f;
        private float _enemyDisplayedFill = 1f;
        private float _targetOverlayAlpha;
        private float _resolvedEncounterHoldRemainingSeconds;
        private int _messageTemplateCursor;
        private int _floatingTextSequenceId;
        private bool _isFeatureEnabled = true;
        private bool _isRuntimeUiInitialized;
        private string _activeResolvedEncounterKey = string.Empty;
        private string _consumedResolvedEncounterKey = string.Empty;

        /// <summary>
        /// 绑定只读运行时查询，并准备运行时战斗 UI
        /// </summary>
        public void Bind(BoardGameRuntimeQueryController runtimeQueryController, Canvas parentCanvas)
        {
            if (!_isFeatureEnabled)
            {
                return;
            }

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

        public void SetFeatureEnabled(bool isEnabled)
        {
            _isFeatureEnabled = isEnabled;

            if (_isFeatureEnabled)
            {
                return;
            }

            _currentSnapshot = null;
            _activeEncounterKey = string.Empty;
            _targetOverlayAlpha = 0f;
            _resolvedEncounterHoldRemainingSeconds = 0f;
            _activeResolvedEncounterKey = string.Empty;

            if (_overlayCanvasGroup != null)
            {
                _overlayCanvasGroup.alpha = 0f;
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(false);
            }
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
            if (!_isFeatureEnabled || _overlayRoot == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            TickResolvedEncounterHold(deltaTime);
            TickOverlayVisibility(deltaTime);
            TickHealthBarAnimation(deltaTime);
            TickFloatingTexts(deltaTime);
        }

        /// <summary>
        /// 根据当前焦点 AI 的战斗状态刷新面板
        /// </summary>
        private void Refresh()
        {
            if (!_isFeatureEnabled || _runtimeQueryController == null)
            {
                return;
            }

            EnsureRuntimeUi();

            CombatSnapshot nextSnapshot = BuildFocusedCombatSnapshot();
            bool isResolvedEncounterHoldSnapshot = false;

            if (nextSnapshot == null)
            {
                nextSnapshot = BuildResolvedEncounterHoldSnapshot();
                isResolvedEncounterHoldSnapshot = nextSnapshot != null;
            }

            if (nextSnapshot == null)
            {
                _currentSnapshot = null;
                _activeEncounterKey = string.Empty;
                _activeEncounterIconKey = string.Empty;
                _activeEncounterEnemyIcon = null;
                _targetOverlayAlpha = 0f;
                return;
            }

            if (isResolvedEncounterHoldSnapshot)
            {
                BeginOrRefreshResolvedEncounterHold(nextSnapshot.EncounterKey);
            }
            else
            {
                _resolvedEncounterHoldRemainingSeconds = 0f;
                _activeResolvedEncounterKey = string.Empty;
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
                CacheEncounterEnemyIcon(nextSnapshot);
                AppendLogLine(nextSnapshot.IsBoss
                    ? GetNextTemplate(BossIntroTemplates)
                    : GetNextTemplate(EnemyIntroTemplates));
                AppendLogLine(nextSnapshot.SquadRosterText);
            }
            else
            {
                AppendLogsFromSnapshotDiff(_currentSnapshot, nextSnapshot);
            }

            if (_activeEncounterIconKey != nextSnapshot.EncounterKey)
            {
                CacheEncounterEnemyIcon(nextSnapshot);
            }

            _activeEncounterKey = nextSnapshot.EncounterKey;
            _currentSnapshot = nextSnapshot;
            ApplySnapshotToUi(nextSnapshot);
        }

        private CombatSnapshot BuildResolvedEncounterHoldSnapshot()
        {
            if (_currentSnapshot == null)
            {
                return null;
            }

            bool isContinuingActiveHold =
                _activeResolvedEncounterKey == _currentSnapshot.EncounterKey &&
                _resolvedEncounterHoldRemainingSeconds > 0f;
            bool canStartNewHold =
                string.IsNullOrEmpty(_activeResolvedEncounterKey) &&
                _currentSnapshot.EncounterKey != _consumedResolvedEncounterKey;

            if (!isContinuingActiveHold && !canStartNewHold)
            {
                return null;
            }

            BoardNodeRuntimeState nodeState = _runtimeQueryController.GetNodeState(_currentSnapshot.NodeId);

            if (nodeState == null || !IsEncounterResolved(nodeState, _currentSnapshot.IsBoss))
            {
                return null;
            }

            return BuildEncounterSnapshotFromNodeState(nodeState, _currentSnapshot.IsBoss);
        }

        private CombatSnapshot BuildEncounterSnapshotFromNodeState(BoardNodeRuntimeState nodeState, bool isBoss)
        {
            if (nodeState == null)
            {
                return null;
            }

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

        private void BeginOrRefreshResolvedEncounterHold(string encounterKey)
        {
            if (string.IsNullOrEmpty(encounterKey))
            {
                return;
            }

            if (_activeResolvedEncounterKey != encounterKey)
            {
                _activeResolvedEncounterKey = encounterKey;
                _resolvedEncounterHoldRemainingSeconds = ResolvedEncounterHoldSeconds;
            }
        }

        private void TickResolvedEncounterHold(float deltaTime)
        {
            if (string.IsNullOrEmpty(_activeResolvedEncounterKey) || _resolvedEncounterHoldRemainingSeconds <= 0f)
            {
                return;
            }

            _resolvedEncounterHoldRemainingSeconds = Mathf.Max(0f, _resolvedEncounterHoldRemainingSeconds - deltaTime);

            if (_resolvedEncounterHoldRemainingSeconds > 0f)
            {
                return;
            }

            _consumedResolvedEncounterKey = _activeResolvedEncounterKey;
            _activeResolvedEncounterKey = string.Empty;
            _targetOverlayAlpha = 0f;
        }

        private static bool IsEncounterResolved(BoardNodeRuntimeState nodeState, bool isBoss)
        {
            if (nodeState == null)
            {
                return false;
            }

            return isBoss
                ? nodeState.BossCurrentHealth <= 0 || nodeState.BossState == BoardBossStateType.Defeated
                : nodeState.EnemyCurrentHealth <= 0 || nodeState.EnemyState == BoardEnemyStateType.Cleared;
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
                    $"-{enemyDamage}");
                AppendLogLine(BuildSquadAttackLog(nextSnapshot, enemyDamage));
            }

            int squadDamage = Mathf.Max(0, previousSnapshot.SquadCurrentHealth - nextSnapshot.SquadCurrentHealth);

            if (squadDamage > 0)
            {
                SpawnFloatingText(
                    _friendlyWidgets != null ? _friendlyWidgets.FloatingAnchor : null,
                    $"-{squadDamage}");
                AppendLogLine(BuildEnemyAttackLog(nextSnapshot, squadDamage));
            }

            int squadHealing = Mathf.Max(0, nextSnapshot.SquadCurrentHealth - previousSnapshot.SquadCurrentHealth);

            if (squadHealing > 0)
            {
                SpawnFloatingText(
                    _friendlyWidgets != null ? _friendlyWidgets.FloatingAnchor : null,
                    $"+{squadHealing}");
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
                ApplyTextColor(_headerText);
            }

            if (_subHeaderText != null)
            {
                _subHeaderText.text = $"Focus-triggered view  {snapshot.SquadAliveCount}/{snapshot.SquadTotalCount} active  Combat log updates in real time";
                ApplyTextColor(_subHeaderText);
            }

            ApplySideWidgets(
                _friendlyWidgets,
                "Friendly Squad",
                snapshot.SquadTitleText,
                snapshot.SquadHealthText,
                $"{snapshot.SquadAttack}",
                $"{snapshot.SquadDefenseAverage}");

            ApplySideWidgets(
                _enemyWidgets,
                snapshot.EnemyTitleText,
                snapshot.EnemySubtitleText,
                snapshot.EnemyHealthText,
                $"{snapshot.EnemyAttack}",
                $"{snapshot.EnemyDefense}");
            ApplyEnemyIcon(_activeEncounterEnemyIcon);

            RefreshLogTexts();
        }

        private void CacheEncounterEnemyIcon(CombatSnapshot snapshot)
        {
            if (snapshot == null)
            {
                _activeEncounterIconKey = string.Empty;
                _activeEncounterEnemyIcon = null;
                return;
            }

            _activeEncounterIconKey = snapshot.EncounterKey ?? string.Empty;
            _activeEncounterEnemyIcon = PickRandomEnemyIcon();
        }

        private Sprite PickRandomEnemyIcon()
        {
            if (_enemyIconSpriteOptions == null || _enemyIconSpriteOptions.Count == 0)
            {
                return null;
            }

            int validIconCount = 0;

            for (int index = 0; index < _enemyIconSpriteOptions.Count; index++)
            {
                if (_enemyIconSpriteOptions[index] != null)
                {
                    validIconCount++;
                }
            }

            if (validIconCount <= 0)
            {
                return null;
            }

            int pickedValidIndex = Random.Range(0, validIconCount);
            int currentValidIndex = 0;

            for (int index = 0; index < _enemyIconSpriteOptions.Count; index++)
            {
                Sprite candidateSprite = _enemyIconSpriteOptions[index];

                if (candidateSprite == null)
                {
                    continue;
                }

                if (currentValidIndex == pickedValidIndex)
                {
                    return candidateSprite;
                }

                currentValidIndex++;
            }

            return null;
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

            if (_floatingTextTemplateText != null && _floatingTextTemplateText.gameObject.activeSelf)
            {
                _floatingTextTemplateText.gameObject.SetActive(false);
            }

            if (_isRuntimeUiInitialized)
            {
                return;
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
                _isRuntimeUiInitialized = true;
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
                _subHeaderText.color = CombatTextColor;

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
                versusText.color = CombatTextColor;

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
                logTitleText.color = CombatTextColor;
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

            _isRuntimeUiInitialized = true;
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
            _friendlyHealthFillImage = ResolveManualHealthFillImage(_friendlyHealthFillImage);
            _enemyHealthFillImage = ResolveManualHealthFillImage(_enemyHealthFillImage);

            _friendlyWidgets = new CombatSideWidgets(
                _friendlyTitleText,
                _friendlySubtitleText,
                _friendlyHealthText,
                _friendlyAttackText,
                _friendlyDefenseText,
                _friendlyHealthFillImage,
                ResolveHealthFillBaseSize(_friendlyHealthFillImage),
                ResolveHealthFillLeftEdge(_friendlyHealthFillImage),
                null,
                null,
                _friendlyFloatingAnchor);

            _enemyWidgets = new CombatSideWidgets(
                _enemyTitleText,
                _enemySubtitleText,
                _enemyHealthText,
                _enemyAttackText,
                _enemyDefenseText,
                _enemyHealthFillImage,
                ResolveHealthFillBaseSize(_enemyHealthFillImage),
                ResolveHealthFillLeftEdge(_enemyHealthFillImage),
                _enemyIconImage,
                _enemyIconFallbackText,
                _enemyFloatingAnchor);

            EnsureManualLogTexts();
        }

        private static Image ResolveManualHealthFillImage(Image assignedImage)
        {
            if (assignedImage == null)
            {
                return null;
            }

            Transform searchRoot = assignedImage.transform.parent;

            if (searchRoot != null)
            {
                Image namedFillImage = null;

                foreach (Image candidateImage in searchRoot.GetComponentsInChildren<Image>(true))
                {
                    if (candidateImage == null)
                    {
                        continue;
                    }

                    if (candidateImage.name.IndexOf("fill", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        namedFillImage = candidateImage;
                        break;
                    }
                }

                if (namedFillImage != null)
                {
                    assignedImage = namedFillImage;
                }
            }

            return assignedImage;
        }

        private static Vector2 ResolveHealthFillBaseSize(Image healthFillImage)
        {
            if (healthFillImage == null || healthFillImage.rectTransform == null)
            {
                return Vector2.zero;
            }

            RectTransform rectTransform = healthFillImage.rectTransform;
            Vector2 sizeDelta = rectTransform.sizeDelta;

            if (sizeDelta.x <= Mathf.Epsilon)
            {
                sizeDelta.x = rectTransform.rect.width;
            }

            if (sizeDelta.y <= Mathf.Epsilon)
            {
                sizeDelta.y = rectTransform.rect.height;
            }

            return sizeDelta;
        }

        private static float ResolveHealthFillLeftEdge(Image healthFillImage)
        {
            if (healthFillImage == null || healthFillImage.rectTransform == null)
            {
                return 0f;
            }

            RectTransform rectTransform = healthFillImage.rectTransform;
            Vector2 baseSize = ResolveHealthFillBaseSize(healthFillImage);
            return rectTransform.anchoredPosition.x - (baseSize.x * rectTransform.pivot.x);
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
            EnsureManualLogContainerLayout();

            if (_combatLogTemplateText.transform.parent != _combatLogContainer)
            {
                _combatLogTemplateText.transform.SetParent(_combatLogContainer, false);
            }

            float preferredHeight = ResolveManualLogLineHeight(_combatLogTemplateText);
            _combatLogTemplateText.gameObject.SetActive(true);
            _combatLogTemplateText.name = "LogLine1";
            ConfigureManualLogLine(_combatLogTemplateText, preferredHeight);
            _logLineTexts.Add(_combatLogTemplateText);

            for (int index = 1; index < MaxLogLineCount; index++)
            {
                TextMeshProUGUI clone = Instantiate(_combatLogTemplateText, _combatLogContainer);
                clone.name = $"LogLine{index + 1}";
                clone.text = string.Empty;
                ConfigureManualLogLine(clone, preferredHeight);
                clone.gameObject.SetActive(false);
                _logLineTexts.Add(clone);
            }
        }

        private void EnsureManualLogContainerLayout()
        {
            if (_combatLogContainer == null)
            {
                return;
            }

            VerticalLayoutGroup layoutGroup = _combatLogContainer.GetComponent<VerticalLayoutGroup>();

            if (layoutGroup == null)
            {
                layoutGroup = _combatLogContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layoutGroup.spacing = 4f;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            if (_combatLogContainer.GetComponent<RectMask2D>() == null)
            {
                _combatLogContainer.gameObject.AddComponent<RectMask2D>();
            }
        }

        private static float ResolveManualLogLineHeight(TMP_Text templateText)
        {
            if (templateText == null)
            {
                return 24f;
            }

            float rectHeight = templateText.rectTransform.rect.height;

            if (rectHeight > 0.01f)
            {
                return rectHeight;
            }

            return Mathf.Max(18f, templateText.fontSize * 1.25f);
        }

        private static void ConfigureManualLogLine(TMP_Text lineText, float preferredHeight)
        {
            if (lineText == null)
            {
                return;
            }

            RectTransform rectTransform = lineText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(0f, preferredHeight);

            LayoutElement layoutElement = lineText.GetComponent<LayoutElement>();

            if (layoutElement == null)
            {
                layoutElement = lineText.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = 1f;
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
            subtitleText.color = CombatTextColor;

            GameObject iconPlateObject = CreatePanel(
                "IconPlate",
                panelObject.transform,
                new Vector2(-144f, 54f),
                new Vector2(118f, 118f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Image iconPlateImage = iconPlateObject.GetComponent<Image>();
            iconPlateImage.color = accentColor;

            GameObject iconImageObject = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
            iconImageObject.transform.SetParent(iconPlateObject.transform, false);
            RectTransform iconImageRect = iconImageObject.GetComponent<RectTransform>();
            iconImageRect.anchorMin = Vector2.zero;
            iconImageRect.anchorMax = Vector2.one;
            iconImageRect.offsetMin = new Vector2(8f, 8f);
            iconImageRect.offsetMax = new Vector2(-8f, -8f);
            Image iconImage = iconImageObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;

            TMP_Text iconFallbackText = CreateText(
                "IconFallback",
                iconPlateObject.transform,
                Vector2.zero,
                new Vector2(90f, 90f),
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            iconFallbackText.color = CombatTextColor;
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
                ResolveHealthFillBaseSize(healthFillImage),
                ResolveHealthFillLeftEdge(healthFillImage),
                iconImage,
                iconFallbackText,
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
            text.color = CombatTextColor;
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
            text.color = CombatTextColor;
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
                ApplyTextColor(widgets.TitleText);
            }

            if (widgets.SubtitleText != null)
            {
                widgets.SubtitleText.text = subtitle;
                ApplyTextColor(widgets.SubtitleText);
            }

            if (widgets.HealthText != null)
            {
                widgets.HealthText.text = healthText;
                ApplyTextColor(widgets.HealthText);
            }

            if (widgets.AttackText != null)
            {
                widgets.AttackText.text = attackText;
                ApplyTextColor(widgets.AttackText);
            }

            if (widgets.DefenseText != null)
            {
                widgets.DefenseText.text = defenseText;
                ApplyTextColor(widgets.DefenseText);
            }
        }

        private void ApplyEnemyIcon(Sprite iconSprite)
        {
            if (_enemyWidgets == null)
            {
                return;
            }

            if (_enemyWidgets.IconImage != null)
            {
                _enemyWidgets.IconImage.sprite = iconSprite;
                _enemyWidgets.IconImage.enabled = iconSprite != null;
            }

            if (_enemyWidgets.IconFallbackText != null)
            {
                _enemyWidgets.IconFallbackText.gameObject.SetActive(iconSprite == null);
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

            ApplyHealthFillAmount(_friendlyWidgets, _friendlyDisplayedFill);
            ApplyHealthFillAmount(_enemyWidgets, _enemyDisplayedFill);
        }

        private void ResetDisplayedHealth(CombatSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _friendlyDisplayedFill = snapshot.SquadCurrentHealth01;
            _enemyDisplayedFill = snapshot.EnemyCurrentHealth01;

            ApplyHealthFillAmount(_friendlyWidgets, _friendlyDisplayedFill);
            ApplyHealthFillAmount(_enemyWidgets, _enemyDisplayedFill);
        }

        private static void ApplyHealthFillAmount(CombatSideWidgets widgets, float fillAmount)
        {
            if (widgets == null || widgets.HealthFillImage == null)
            {
                return;
            }

            Image healthFillImage = widgets.HealthFillImage;
            float clampedFillAmount = Mathf.Clamp01(fillAmount);

            if (healthFillImage.type == Image.Type.Filled)
            {
                healthFillImage.fillAmount = clampedFillAmount;
                return;
            }

            RectTransform rectTransform = healthFillImage.rectTransform;

            if (rectTransform == null)
            {
                return;
            }

            Vector2 baseSize = widgets.HealthFillBaseSize;

            if (baseSize.x <= Mathf.Epsilon)
            {
                baseSize = new Vector2(Mathf.Max(1f, rectTransform.rect.width), rectTransform.sizeDelta.y);
            }

            rectTransform.pivot = new Vector2(0f, rectTransform.pivot.y);
            rectTransform.sizeDelta = new Vector2(baseSize.x * clampedFillAmount, baseSize.y);
            rectTransform.anchoredPosition = new Vector2(widgets.HealthFillLeftEdge, rectTransform.anchoredPosition.y);
        }

        private void SpawnFloatingText(RectTransform anchor, string valueText)
        {
            if (anchor == null)
            {
                return;
            }

            TMP_Text floatingText = CreateFloatingText(anchor);
            Vector3 startLocalPosition = new Vector3(Random.Range(-44f, 44f), Random.Range(-18f, 18f), 0f);
            floatingText.text = valueText;
            _floatingTextPresenter.Add(floatingText, startLocalPosition, floatingText.color);
        }

        private TMP_Text CreateFloatingText(RectTransform anchor)
        {
            _floatingTextSequenceId += 1;

            if (_floatingTextTemplateText != null)
            {
                TextMeshProUGUI floatingText = Instantiate(_floatingTextTemplateText, anchor);
                floatingText.gameObject.name = $"FloatingText_{_floatingTextSequenceId}";
                floatingText.gameObject.SetActive(true);
                floatingText.raycastTarget = false;
                return floatingText;
            }

            return CreateText(
                $"FloatingText_{_floatingTextSequenceId}",
                anchor,
                Vector2.zero,
                new Vector2(160f, 34f),
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
        }

        private void TickFloatingTexts(float deltaTime)
        {
            _floatingTextPresenter.Tick(deltaTime);
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
                    if (lineText.gameObject.activeSelf)
                    {
                        lineText.gameObject.SetActive(false);
                    }
                    continue;
                }

                if (!lineText.gameObject.activeSelf)
                {
                    lineText.gameObject.SetActive(true);
                }

                lineText.text = _logLines[index];
                lineText.color = CombatTextColor;
            }
        }

        private static void ApplyTextColor(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.color = CombatTextColor;
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
                Vector2 healthFillBaseSize,
                float healthFillLeftEdge,
                Image iconImage,
                TMP_Text iconFallbackText,
                RectTransform floatingAnchor)
            {
                TitleText = titleText;
                SubtitleText = subtitleText;
                HealthText = healthText;
                AttackText = attackText;
                DefenseText = defenseText;
                HealthFillImage = healthFillImage;
                HealthFillBaseSize = healthFillBaseSize;
                HealthFillLeftEdge = healthFillLeftEdge;
                IconImage = iconImage;
                IconFallbackText = iconFallbackText;
                FloatingAnchor = floatingAnchor;
            }

            public TMP_Text TitleText { get; }
            public TMP_Text SubtitleText { get; }
            public TMP_Text HealthText { get; }
            public TMP_Text AttackText { get; }
            public TMP_Text DefenseText { get; }
            public Image HealthFillImage { get; }
            public Vector2 HealthFillBaseSize { get; }
            public float HealthFillLeftEdge { get; }
            public Image IconImage { get; }
            public TMP_Text IconFallbackText { get; }
            public RectTransform FloatingAnchor { get; }
        }

    }
}
