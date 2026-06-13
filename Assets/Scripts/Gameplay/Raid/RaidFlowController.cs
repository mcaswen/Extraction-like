using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RaidFlowController : MonoBehaviour
{
    private const string LegacyPlayerExtractionId = "Player";
    private const string SuccessScreenResourcePath = "UI/Pfb_RaidExtractionSuccessScreen";

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
    private string _recentEventMessage = string.Empty;
    private string _missionFailureDetail = "Player is down";
    private float _recentEventTimer;
    private GUIStyle _worldPromptStyle;
    private readonly Dictionary<string, AgentExtractionProgress> _activeExtractionProgressByAgentId =
        new Dictionary<string, AgentExtractionProgress>();
    private readonly HashSet<string> _extractedAgentIds = new HashSet<string>();
    private readonly HashSet<string> _requiredExtractionAgentIds =
        new HashSet<string>(System.StringComparer.Ordinal);
    private readonly List<string> _completedExtractionAgentIds = new List<string>();
    private bool _requiredExtractionAgentsCaptured;
    private float _missionStartTime;
    private bool _successScreenShown;
    private RaidExtractionSuccessScreen _successScreenInstance;

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
        if (_recentEventTimer > 0f)
        {
            _recentEventTimer -= Time.unscaledDeltaTime;
            if (_recentEventTimer <= 0f)
            {
                _recentEventMessage = string.Empty;
            }
        }

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
        DrawMissionHud();
        DrawExtractionWorldPrompt();
        DrawMissionResult();
    }

    public void NotifyEnemyKilled(string enemyName)
    {
        _enemiesKilledCount++;
        PushEventMessage($"已击败敌人: {enemyName}");
    }

    public void NotifyLootCollected(string itemName)
    {
        _lootCollectedCount++;
        PushEventMessage($"已获取战利品: {itemName}");
    }

    public void NotifyPlayerDied()
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        _isMissionFailed = true;
        _missionFailureDetail = "Player is down";
        Time.timeScale = 0f;
    }

    public void NotifyAgentDied(AgentPawnRoot agent)
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        if (agent != null)
            ClearAgentExtractionProgress(agent.AgentIdValue);

        string agentLabel = agent != null && !string.IsNullOrWhiteSpace(agent.AgentIdValue)
            ? $"Agent {agent.AgentIdValue}"
            : "Agent";

        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry != null && registry.TryGetPrimaryHandle(out AgentRuntimeHandle livingAgent))
        {
            string nextAgentLabel = !string.IsNullOrWhiteSpace(livingAgent.AgentId.Value)
                ? $"Agent {livingAgent.AgentId.Value}"
                : "其他 Agent";
            PushEventMessage($"{agentLabel} 已阵亡，当前焦点: {nextAgentLabel}");
            return;
        }

        PushEventMessage("所有 Agent 已阵亡");
        _isMissionFailed = true;
        _missionFailureDetail = "All agents are down";
        Time.timeScale = 0f;
    }

    public void SetPlayerInsideExtractionPoint(ExtractionPointController extractionPoint, bool isInside)
    {
        SetAgentInsideExtractionPoint(LegacyPlayerExtractionId, extractionPoint, isInside);
    }

    public void SetAgentInsideExtractionPoint(string agentId, ExtractionPointController extractionPoint, bool isInside)
    {
        if (_isMissionCompleted || _isMissionFailed || extractionPoint == null)
            return;

        string normalizedAgentId = NormalizeExtractionAgentId(agentId);
        if (string.IsNullOrEmpty(normalizedAgentId))
            return;

        if (isInside)
        {
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
            _activeExtractionProgressByAgentId.Remove(completedAgentId);
            if (_extractedAgentIds.Add(completedAgentId))
                PushEventMessage($"{FormatExtractionAgentLabel(completedAgentId)} 已撤离");

            bool allRequiredAgentsExtracted = AreAllRequiredAgentsExtracted();
            DestroyExtractedAgent(completedAgentId);

            if (allRequiredAgentsExtracted)
            {
                CompleteExtraction(completionPoint);
                return;
            }
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

    private void PushEventMessage(string message)
    {
        _recentEventMessage = message;
        _recentEventTimer = 2.25f;
    }

    private void DrawMissionHud()
    {
        const float panelWidth = 280f;
        const float panelHeight = 140f;
        const float panelMargin = 16f;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Translate(new Vector3(
            Screen.width - panelWidth - panelMargin - 16f,
            Screen.height - panelHeight - panelMargin - 16f,
            0f)) * previousMatrix;

        GUI.Box(new Rect(16f, 16f, 280f, 140f), string.Empty);
        GUI.Label(new Rect(28f, 28f, 240f, 24f), $"任务: {MissionName}");
        GUI.Label(new Rect(28f, 54f, 240f, 22f), $"剩余敌人: {RemainingEnemyCount}");
        GUI.Label(new Rect(28f, 76f, 240f, 22f), $"已获取战利品: {_lootCollectedCount}");

        if (_activeExtractionPoint == null)
        {
            int extractedCount = GetExtractedRequiredAgentCount();
            string extractionText = extractedCount > 0
                ? $"已撤离: {extractedCount}/{GetRequiredExtractionAgentCount()}"
                : "目标: 前往撤离点";
            GUI.Label(new Rect(28f, 98f, 240f, 22f), extractionText);
        }
        else
        {
            GUI.Label(new Rect(28f, 98f, 240f, 22f), GetExtractionHudText());
        }

        if (!string.IsNullOrEmpty(_recentEventMessage))
        {
            GUI.Label(new Rect(28f, 120f, 240f, 22f), _recentEventMessage);
        }

        GUI.matrix = previousMatrix;
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
        int lootItemCount = _lootCollectedCount;
        int totalValue = 0;

        InventoryScreenController inventory = InventoryScreenController.Instance;
        if (inventory != null &&
            inventory.TryGetExtractionInventorySummary(
                _requiredExtractionAgentIds,
                out int carriedItemCount,
                out int carriedValue))
        {
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
        if (FindObjectOfType<RaidMinimapController>() != null)
        {
            return;
        }

        GameObject minimapObject = new GameObject("RaidMinimapController");
        minimapObject.AddComponent<RaidMinimapController>();
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

        UnityEngine.Object.Destroy(handle.PawnRoot.gameObject);
    }

    private string GetExtractionHudText()
    {
        float remainingTime = GetActiveExtractionRemainingSeconds();
        return $"撤离中: {GetExtractedRequiredAgentCount()}/{GetRequiredExtractionAgentCount()}  {remainingTime:0.0}s";
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

    private static string FormatExtractionAgentLabel(string agentId)
    {
        return string.Equals(agentId, LegacyPlayerExtractionId, System.StringComparison.Ordinal)
            ? "Player"
            : $"Agent {agentId}";
    }

    private sealed class AgentExtractionProgress
    {
        public ExtractionPointController ExtractionPoint;
        public float ProgressSeconds;
    }
}
