using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 局内流程控制器，负责任务状态、撤离进度、失败重开和撤离结算
/// </summary>
public class RaidFlowController : MonoBehaviour
{
    private const string LegacyPlayerExtractionId = "Player";
    private const string SuccessScreenResourcePath = "UI/Pfb_RaidExtractionSuccessScreen";
    private const string SharedStorageAgentId = "default_player";
    private const int SettlementStorageColumns = 6;
    private const int SettlementStorageRows = 10;
    private const int SettlementStorageMinimumPageCount = 10;

    public static RaidFlowController Instance { get; private set; }

    public string MissionName = "MVP Raid";
    public KeyCode RestartKey = KeyCode.R;

    [Header("Settlement UI")]
    [SerializeField] private RaidExtractionSuccessScreen _successScreenPrefab;

    private int _initialEnemyCount;
    private int _enemiesKilledCount;
    private int _lootCollectedCount;
    private bool _isMissionCompleted;
    private bool _isMissionFailed;
    private ExtractionPointController _activeExtractionPoint;
    private float _extractionProgressSeconds;
    private string _missionFailureDetail = "Player is down";
    private GUIStyle _worldPromptStyle;
    private readonly Dictionary<string, AgentExtractionProgress> _activeExtractionProgressByAgentId =
        new Dictionary<string, AgentExtractionProgress>();
    private readonly HashSet<string> _extractedAgentIds = new HashSet<string>();
    private readonly HashSet<string> _requiredExtractionAgentIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private readonly HashSet<string> _settledExtractionAgentIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private readonly List<string> _completedExtractionAgentIds = new List<string>();
    private bool _requiredExtractionAgentsCaptured;
    private float _missionStartTime;
    private bool _successScreenShown;
    private RaidExtractionSuccessScreen _successScreenInstance;
    private bool _hasSettledExtractionInventory;
    private int _settledExtractionItemCount;
    private int _settledExtractionTotalValue;

    public bool IsInputLocked => _isMissionCompleted || _isMissionFailed;
    public int RemainingEnemyCount => Mathf.Max(0, _initialEnemyCount - _enemiesKilledCount);
    public int LootCollectedCount => _lootCollectedCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        EnsureMinimapExists();
    }

    private void Start()
    {
        _missionStartTime = Time.time;
        _initialEnemyCount = FindObjectsOfType<EnemyHealthController>().Length;
        TryCaptureRequiredExtractionAgents();
        PlayerStatusHudController.EnsureRuntimeInstance();
    }

    private void Update()
    {
        if (IsInputLocked)
        {
            if (Input.GetKeyDown(RestartKey))
            {
                RestartCurrentScene();
            }

            return;
        }

        TickExtractionProgress();
    }

    private void OnGUI()
    {
        DrawExtractionWorldPrompt();
        DrawMissionResult();
    }

    /// <summary>
    /// 通知流程控制器有敌人死亡
    /// </summary>
    /// <param name="enemyName">敌人名称</param>
    public void NotifyEnemyKilled(string enemyName)
    {
        _enemiesKilledCount++;
    }

    /// <summary>
    /// 通知流程控制器有战利品被拾取
    /// </summary>
    /// <param name="itemName">物品名称</param>
    public void NotifyLootCollected(string itemName)
    {
        _lootCollectedCount++;
    }

    /// <summary>
    /// 通知流程控制器玩家死亡并进入失败状态
    /// </summary>
    public void NotifyPlayerDied()
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        DiscardAgentExtractionInventory(LegacyPlayerExtractionId);
        _isMissionFailed = true;
        _missionFailureDetail = "Player is down";
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 通知流程控制器智能体死亡，并在所有可控智能体阵亡时判定失败
    /// </summary>
    /// <param name="agent">死亡的智能体根节点</param>
    public void NotifyAgentDied(AgentPawnRoot agent)
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        if (agent != null)
        {
            DiscardAgentExtractionInventory(agent.AgentIdValue);
            ClearAgentExtractionProgress(agent.AgentIdValue);
        }

        TryFailWhenNoRemainingAgents();
    }

    private bool TryFailWhenNoRemainingAgents()
    {
        var registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry != null)
        {
            foreach (var handle in registry.RegisteredAgents)
                if (handle.IsAlive && !_extractedAgentIds.Contains(handle.AgentId.Value)) return false;
        }
        _isMissionFailed = true;
        _missionFailureDetail = _extractedAgentIds.Count > 0
            ? "Remaining agents are down; extracted loot retained" : "All agents are down";
        Time.timeScale = 0f;
        return true;
    }

    /// <summary>
    /// 兼容旧玩家对象的撤离范围进入和离开通知
    /// </summary>
    /// <param name="extractionPoint">对应撤离点</param>
    /// <param name="isInside">是否处于撤离范围内</param>
    public void SetPlayerInsideExtractionPoint(ExtractionPointController extractionPoint, bool isInside)
    {
        SetAgentInsideExtractionPoint(LegacyPlayerExtractionId, extractionPoint, isInside);
    }

    /// <summary>
    /// 设置指定智能体是否处于撤离点范围内
    /// </summary>
    /// <param name="agentId">智能体标识</param>
    /// <param name="extractionPoint">对应撤离点</param>
    /// <param name="isInside">是否处于撤离范围内</param>
    public void SetAgentInsideExtractionPoint(string agentId, ExtractionPointController extractionPoint, bool isInside)
    {
        if (_isMissionCompleted || _isMissionFailed || extractionPoint == null)
            return;

        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId))
            return;

        if (isInside)
        {
            // 第一次进入撤离点时捕获本局要求撤离的智能体列表，避免后续销毁导致目标丢失
            TryCaptureRequiredExtractionAgents();
            Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance?.NotifyExtractionTouched(extractionPoint);
            if (_extractedAgentIds.Contains(normalizedAgentId))
            {
                return;
            }

            if (!_activeExtractionProgressByAgentId.TryGetValue(
                    normalizedAgentId,
                    out AgentExtractionProgress progress))
            {
                progress = new AgentExtractionProgress();
                _activeExtractionProgressByAgentId.Add(normalizedAgentId, progress);
            }

            if (progress.ExtractionPoint != extractionPoint)
            {
                progress.ProgressSeconds = 0f;
            }

            progress.ExtractionPoint = extractionPoint;
            _activeExtractionPoint = extractionPoint;
            return;
        }

        if (_activeExtractionProgressByAgentId.TryGetValue(
                normalizedAgentId,
                out AgentExtractionProgress activeProgress) &&
            activeProgress.ExtractionPoint == extractionPoint)
        {
            _activeExtractionProgressByAgentId.Remove(normalizedAgentId);
            RefreshActiveExtractionPoint();
        }
    }

    private void TickExtractionProgress()
    {
        if (_activeExtractionProgressByAgentId.Count <= 0)
        {
            _activeExtractionPoint = null;
            _extractionProgressSeconds = 0f;
            return;
        }

        _completedExtractionAgentIds.Clear();
        _extractionProgressSeconds = 0f;

        // 允许多个智能体同时读条，用完成列表延迟移除避免遍历时修改字典
        foreach (KeyValuePair<string, AgentExtractionProgress> pair in _activeExtractionProgressByAgentId)
        {
            AgentExtractionProgress progress = pair.Value;
            if (progress == null || progress.ExtractionPoint == null)
            {
                _completedExtractionAgentIds.Add(pair.Key);
                continue;
            }

            progress.ProgressSeconds += Time.deltaTime;
            _extractionProgressSeconds = Mathf.Max(_extractionProgressSeconds, progress.ProgressSeconds);

            float duration = Mathf.Max(0.05f, progress.ExtractionPoint.ExtractionDurationSeconds);
            if (progress.ProgressSeconds >= duration)
                _completedExtractionAgentIds.Add(pair.Key);
        }

        for (int i = 0; i < _completedExtractionAgentIds.Count; i++)
        {
            string completedAgentId = _completedExtractionAgentIds[i];
            if (!_activeExtractionProgressByAgentId.TryGetValue(
                    completedAgentId,
                    out AgentExtractionProgress completedProgress))
            {
                continue;
            }

            ExtractionPointController completionPoint = completedProgress.ExtractionPoint;
            if (!TrySettleExtractedAgentInventory(completedAgentId))
            {
                _isMissionFailed = true;
                _missionFailureDetail = "Extraction settlement failed; inventory retained";
                Time.timeScale = 0f;
                return;
            }
            _activeExtractionProgressByAgentId.Remove(completedAgentId);
            _extractedAgentIds.Add(completedAgentId);

            // 先判断是否全部撤离，再销毁当前智能体，避免注册表变更影响完成判定
            bool allRequiredAgentsExtracted = AreAllRequiredAgentsExtracted();
            DestroyExtractedAgent(completedAgentId);

            if (allRequiredAgentsExtracted)
            {
                CompleteExtraction(completionPoint);
                return;
            }
            // Destroy 延迟到帧末，检查时排除已提交撤离的角色，避免伤亡后永久等人。
            if (TryFailWhenNoRemainingAgents()) return;
        }

        RefreshActiveExtractionPoint();
    }

    private void CompleteExtraction(ExtractionPointController extractionPoint)
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        _isMissionCompleted = true;
        Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance?.NotifyExtractionCompleted(
            extractionPoint != null ? extractionPoint : _activeExtractionPoint);
        ShowExtractionSuccessScreen(CreateExtractionSummary());
        Time.timeScale = 0f;
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void DrawExtractionWorldPrompt()
    {
        if (_activeExtractionPoint == null || _isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(_activeExtractionPoint.GetWorldPromptPosition());
        if (screenPosition.z <= 0f)
        {
            return;
        }

        EnsureWorldPromptStyle();

        string promptText = GetExtractionPromptText();

        const float width = 176f;
        const float height = 28f;
        Rect rect = new Rect(
            screenPosition.x - width * 0.5f,
            Screen.height - screenPosition.y - height * 0.5f,
            width,
            height);

        GUI.Box(rect, string.Empty);
        GUI.Label(rect, promptText, _worldPromptStyle);
    }

    private void EnsureWorldPromptStyle()
    {
        if (_worldPromptStyle != null)
        {
            return;
        }

        _worldPromptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        _worldPromptStyle.normal.textColor = Color.white;
    }

    private void DrawMissionResult()
    {
        if (!_isMissionFailed)
        {
            return;
        }

        float width = 360f;
        float height = 140f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panelRect, string.Empty);
        string title = "MISSION FAILED";
        string detail = _missionFailureDetail;

        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 24f, panelRect.width - 48f, 26f), title);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 56f, panelRect.width - 48f, 24f), detail);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 92f, panelRect.width - 48f, 24f), $"Press {RestartKey} to restart");
    }

    private RaidExtractionSummary CreateExtractionSummary()
    {
        float elapsedSeconds = Mathf.Max(0f, Time.time - _missionStartTime);
        if (_hasSettledExtractionInventory)
        {
            return new RaidExtractionSummary(
                elapsedSeconds,
                _settledExtractionItemCount,
                _settledExtractionTotalValue);
        }

        int lootItemCount = _lootCollectedCount;
        int totalValue = 0;

        InventoryScreenController inventory = InventoryScreenController.Instance;
        if (inventory != null &&
            inventory.TryGetExtractionInventorySummary(
                _requiredExtractionAgentIds,
                out int carriedItemCount,
                out int carriedValue))
        {
            // 撤离结算以实际携带背包为准，普通拾取计数只作为兜底
            lootItemCount = carriedItemCount;
            totalValue = carriedValue;
        }

        return new RaidExtractionSummary(elapsedSeconds, lootItemCount, totalValue);
    }

    private void ShowExtractionSuccessScreen(RaidExtractionSummary summary)
    {
        if (_successScreenShown)
        {
            return;
        }

        _successScreenShown = true;
        if (_successScreenInstance == null)
        {
            RaidExtractionSuccessScreen prefab = _successScreenPrefab != null
                ? _successScreenPrefab
                : LoadSuccessScreenPrefab();

            if (prefab == null)
            {
                Debug.LogError(
                    $"Missing extraction success settlement prefab at Resources/{SuccessScreenResourcePath}.",
                    this);
                return;
            }

            _successScreenInstance = Instantiate(prefab);
        }

        _successScreenInstance.Show(summary, RestartCurrentScene);
    }

    private static RaidExtractionSuccessScreen LoadSuccessScreenPrefab()
    {
        GameObject prefabObject = Resources.Load<GameObject>(SuccessScreenResourcePath);
        return prefabObject != null ? prefabObject.GetComponent<RaidExtractionSuccessScreen>() : null;
    }

    private static void EnsureMinimapExists()
    {
        if (FindObjectOfType<StorageScreenController>(true) != null)
        {
            return;
        }

        if (FindObjectOfType<RaidMinimapController>() != null)
        {
            return;
        }

        GameObject minimapObject = new GameObject("RaidMinimapController");
        minimapObject.AddComponent<RaidMinimapController>();
    }

    private bool TrySettleExtractedAgentInventory(string agentId)
    {
        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId)) return false;
        if (_settledExtractionAgentIds.Contains(normalizedAgentId)) return true;

        InventoryScreenController inventory = InventoryScreenController.Instance;
        // 无背包组件的旧白盒关卡按空库存兼容，有组件却缺角色快照不能假报结算成功。
        if (inventory == null)
        {
            _settledExtractionAgentIds.Add(normalizedAgentId);
            return true;
        }
        if (!inventory.TryCollectExtractableItemsForAgent(
                normalizedAgentId,
                out List<ContainerItemSaveData> settlementItems,
                out int itemCount,
                out int totalValue))
        {
            Debug.LogError($"[RaidFlowController] Missing extraction inventory snapshot for agent '{normalizedAgentId}'.", this);
            return false;
        }

        bool storageSucceeded = true;
        if (settlementItems.Count > 0)
        {
            PlayerStorageService storageService = EnsureSettlementStorageService();
            storageSucceeded =
                storageService != null &&
                storageService.TryAppendItemsToAgentStorage(
                    SharedStorageAgentId,
                    settlementItems,
                    out itemCount,
                    out totalValue);
        }

        if (!storageSucceeded)
        {
            Debug.LogError(
                $"[RaidFlowController] Failed to append extraction inventory for agent '{normalizedAgentId}' to storage.",
                this);
            return false;
        }

        _hasSettledExtractionInventory = true;
        _settledExtractionItemCount += itemCount;
        _settledExtractionTotalValue += totalValue;
        inventory.DiscardExtractableItemsForAgent(normalizedAgentId);
        _settledExtractionAgentIds.Add(normalizedAgentId);
        return true;
    }

    private void DiscardAgentExtractionInventory(string agentId)
    {
        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId) ||
            _settledExtractionAgentIds.Contains(normalizedAgentId))
        {
            return;
        }

        InventoryScreenController.Instance?.DiscardExtractableItemsForAgent(normalizedAgentId);
    }

    private static PlayerStorageService EnsureSettlementStorageService()
    {
        PlayerStorageService storageService = PlayerStorageService.Instance;
        if (storageService == null)
        {
            GameObject storageObject = new GameObject("RuntimeExtractionStorageService");
            storageObject.hideFlags = HideFlags.HideAndDontSave;
            storageService = storageObject.AddComponent<PlayerStorageService>();
        }

        storageService.FallbackAgentId = SharedStorageAgentId;
        storageService.Configure(
            SettlementStorageColumns,
            SettlementStorageRows,
            SettlementStorageMinimumPageCount);
        return storageService;
    }

    private void ClearAgentExtractionProgress(string agentId)
    {
        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId))
            return;

        _activeExtractionProgressByAgentId.Remove(normalizedAgentId);
        RefreshActiveExtractionPoint();
    }

    private bool AreAllRequiredAgentsExtracted()
    {
        if (!TryCaptureRequiredExtractionAgents())
            return _extractedAgentIds.Contains(LegacyPlayerExtractionId);

        foreach (string requiredAgentId in _requiredExtractionAgentIds)
        {
            if (!_extractedAgentIds.Contains(requiredAgentId))
                return false;
        }

        return _requiredExtractionAgentIds.Count > 0;
    }

    private int GetRequiredExtractionAgentCount()
    {
        return TryCaptureRequiredExtractionAgents()
            ? Mathf.Max(1, _requiredExtractionAgentIds.Count)
            : 1;
    }

    private int GetExtractedRequiredAgentCount()
    {
        if (!TryCaptureRequiredExtractionAgents())
            return _extractedAgentIds.Contains(LegacyPlayerExtractionId) ? 1 : 0;

        int count = 0;
        foreach (string requiredAgentId in _requiredExtractionAgentIds)
        {
            if (_extractedAgentIds.Contains(requiredAgentId))
                count++;
        }

        return count;
    }

    private bool TryCaptureRequiredExtractionAgents()
    {
        if (_requiredExtractionAgentsCaptured)
            return _requiredExtractionAgentIds.Count > 0;

        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || registry.AgentCount <= 0)
            return false;

        IReadOnlyList<AgentRuntimeHandle> registeredAgents = registry.RegisteredAgents;
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            // 只捕获当前已注册且有效的智能体，防止撤离目标在运行中漂移
            AgentRuntimeHandle handle = registeredAgents[i];
            string agentId = handle.IsValid ? NormalizeExtractionAgentId(handle.AgentId.Value) : string.Empty;
            if (!string.IsNullOrEmpty(agentId))
                _requiredExtractionAgentIds.Add(agentId);
        }

        _requiredExtractionAgentsCaptured = _requiredExtractionAgentIds.Count > 0;
        return _requiredExtractionAgentsCaptured;
    }

    private static void DestroyExtractedAgent(string agentId)
    {
        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId) ||
            string.Equals(normalizedAgentId, LegacyPlayerExtractionId, System.StringComparison.Ordinal))
        {
            return;
        }

        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null ||
            !registry.TryGetHandle(normalizedAgentId, out AgentRuntimeHandle handle) ||
            handle.PawnRoot == null)
        {
            return;
        }

        // 撤离成功后从场景移除对应智能体，流程层只保留已撤离标识
        AgentSfxEmitter sfxEmitter = handle.PawnRoot.GetComponent<AgentSfxEmitter>();
        if (sfxEmitter != null)
            sfxEmitter.PlayExtract();
        else
            global::GameSfxPlayer.PlayAiExtract(handle.PawnRoot.transform.position);
        UnityEngine.Object.Destroy(handle.PawnRoot.gameObject);
    }

    private string GetExtractionPromptText()
    {
        float remainingTime = GetActiveExtractionRemainingSeconds();
        return $"撤离 {GetExtractedRequiredAgentCount()}/{GetRequiredExtractionAgentCount()}  等待 {remainingTime:0.0}s";
    }

    private float GetActiveExtractionRemainingSeconds()
    {
        float remainingTime = 0f;
        bool hasActiveProgress = false;

        foreach (KeyValuePair<string, AgentExtractionProgress> pair in _activeExtractionProgressByAgentId)
        {
            AgentExtractionProgress progress = pair.Value;
            if (progress == null || progress.ExtractionPoint == null)
                continue;

            float duration = Mathf.Max(0.05f, progress.ExtractionPoint.ExtractionDurationSeconds);
            float candidateRemaining = Mathf.Max(0f, duration - progress.ProgressSeconds);
            if (!hasActiveProgress || candidateRemaining < remainingTime)
            {
                remainingTime = candidateRemaining;
                hasActiveProgress = true;
            }
        }

        if (hasActiveProgress)
            return remainingTime;

        return _activeExtractionPoint != null
            ? Mathf.Max(0f, _activeExtractionPoint.ExtractionDurationSeconds - _extractionProgressSeconds)
            : 0f;
    }

    private void RefreshActiveExtractionPoint()
    {
        _activeExtractionPoint = null;
        _extractionProgressSeconds = 0f;

        foreach (KeyValuePair<string, AgentExtractionProgress> pair in _activeExtractionProgressByAgentId)
        {
            AgentExtractionProgress progress = pair.Value;
            if (progress == null || progress.ExtractionPoint == null)
                continue;

            _activeExtractionPoint = progress.ExtractionPoint;
            _extractionProgressSeconds = Mathf.Max(_extractionProgressSeconds, progress.ProgressSeconds);
            break;
        }
    }

    private static string NormalizeExtractionAgentId(string agentId)
    {
        return string.IsNullOrWhiteSpace(agentId) ? string.Empty : agentId.Trim();
    }

    private sealed class AgentExtractionProgress
    {
        public ExtractionPointController ExtractionPoint;
        public float ProgressSeconds;
    }
}
