using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RaidFlowController : MonoBehaviour
{
    private const string LegacyPlayerExtractionId = "Player";

    public static RaidFlowController Instance { get; private set; }

    [Header("Flow Rules")]
    public bool RequireLootBeforeExtraction = true;
    public string MissionName = "MVP Raid";
    public KeyCode RestartKey = KeyCode.R;

    private int _initialEnemyCount;
    private int _enemiesKilledCount;
    private int _lootCollectedCount;
    private bool _isMissionCompleted;
    private bool _isMissionFailed;
    private ExtractionPointController _activeExtractionPoint;
    private float _extractionProgressSeconds;
    private string _recentEventMessage = string.Empty;
    private string _missionFailureDetail = "主角已阵亡";
    private float _recentEventTimer;
    private GUIStyle _worldPromptStyle;
    private readonly Dictionary<string, AgentExtractionProgress> _activeExtractionProgressByAgentId =
        new Dictionary<string, AgentExtractionProgress>();
    private readonly HashSet<string> _extractedAgentIds = new HashSet<string>();
    private readonly List<string> _completedExtractionAgentIds = new List<string>();

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
        _initialEnemyCount = FindObjectsOfType<EnemyHealthController>().Length;
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
        _missionFailureDetail = "主角已阵亡";
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
        _missionFailureDetail = "所有 Agent 已阵亡";
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

        if (RequireLootBeforeExtraction && _lootCollectedCount <= 0)
        {
            foreach (KeyValuePair<string, AgentExtractionProgress> pair in _activeExtractionProgressByAgentId)
                pair.Value.ProgressSeconds = 0f;

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

            if (AreAllRequiredAgentsExtracted())
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
        else if (RequireLootBeforeExtraction && _lootCollectedCount <= 0)
        {
            GUI.Label(new Rect(28f, 98f, 240f, 22f), "撤离条件: 至少带走一件战利品");
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

        string promptText;
        if (RequireLootBeforeExtraction && _lootCollectedCount <= 0)
        {
            promptText = "需至少带走 1 件战利品";
        }
        else
        {
            promptText = GetExtractionPromptText();
        }

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
        if (!_isMissionCompleted && !_isMissionFailed)
        {
            return;
        }

        float width = 360f;
        float height = 140f;
        Rect panelRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.Box(panelRect, string.Empty);
        string title = _isMissionCompleted ? "撤离成功" : "任务失败";
        string detail = _isMissionCompleted
            ? $"你带走了 {_lootCollectedCount} 件战利品"
            : _missionFailureDetail;

        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 24f, panelRect.width - 48f, 26f), title);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 56f, panelRect.width - 48f, 24f), detail);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 92f, panelRect.width - 48f, 24f), $"按 {RestartKey} 重新开始");
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
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || registry.AgentCount <= 0)
            return _extractedAgentIds.Contains(LegacyPlayerExtractionId);

        bool hasRequiredAgent = false;
        IReadOnlyList<AgentRuntimeHandle> registeredAgents = registry.RegisteredAgents;
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            AgentRuntimeHandle handle = registeredAgents[i];
            if (!handle.IsValid)
                continue;

            hasRequiredAgent = true;
            if (!_extractedAgentIds.Contains(handle.AgentId.Value))
                return false;
        }

        return hasRequiredAgent;
    }

    private int GetRequiredExtractionAgentCount()
    {
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || registry.AgentCount <= 0)
            return 1;

        int count = 0;
        IReadOnlyList<AgentRuntimeHandle> registeredAgents = registry.RegisteredAgents;
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            if (registeredAgents[i].IsValid)
                count++;
        }

        return Mathf.Max(1, count);
    }

    private int GetExtractedRequiredAgentCount()
    {
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || registry.AgentCount <= 0)
            return _extractedAgentIds.Contains(LegacyPlayerExtractionId) ? 1 : 0;

        int count = 0;
        IReadOnlyList<AgentRuntimeHandle> registeredAgents = registry.RegisteredAgents;
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            AgentRuntimeHandle handle = registeredAgents[i];
            if (handle.IsValid && _extractedAgentIds.Contains(handle.AgentId.Value))
                count++;
        }

        return count;
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
