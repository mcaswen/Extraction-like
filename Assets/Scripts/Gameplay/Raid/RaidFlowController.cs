using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 最小可运行战斗流程控制器。
/// 负责统计击杀、搜刮、撤离和失败状态，并提供最简 HUD 与重开入口。
/// </summary>
public class RaidFlowController : MonoBehaviour
{
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
    private float _recentEventTimer;

    public bool IsInputLocked => _isMissionCompleted || _isMissionFailed;
    public int RemainingEnemyCount => Mathf.Max(0, _initialEnemyCount - _enemiesKilledCount);
    public int LootCollectedCount => _lootCollectedCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        _initialEnemyCount = FindObjectsOfType<EnemyHealthController>().Length;
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
        Time.timeScale = 0f;
    }

    public void SetPlayerInsideExtractionPoint(ExtractionPointController extractionPoint, bool isInside)
    {
        if (isInside)
        {
            _activeExtractionPoint = extractionPoint;
            _extractionProgressSeconds = 0f;
            return;
        }

        if (_activeExtractionPoint == extractionPoint)
        {
            _activeExtractionPoint = null;
            _extractionProgressSeconds = 0f;
        }
    }

    private void TickExtractionProgress()
    {
        if (_activeExtractionPoint == null)
        {
            _extractionProgressSeconds = 0f;
            return;
        }

        if (RequireLootBeforeExtraction && _lootCollectedCount <= 0)
        {
            _extractionProgressSeconds = 0f;
            return;
        }

        _extractionProgressSeconds += Time.deltaTime;
        if (_extractionProgressSeconds >= _activeExtractionPoint.ExtractionDurationSeconds)
        {
            CompleteExtraction();
        }
    }

    private void CompleteExtraction()
    {
        if (_isMissionCompleted || _isMissionFailed)
        {
            return;
        }

        _isMissionCompleted = true;
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
        GUI.Box(new Rect(16f, 16f, 280f, 140f), string.Empty);
        GUI.Label(new Rect(28f, 28f, 240f, 24f), $"任务: {MissionName}");
        GUI.Label(new Rect(28f, 54f, 240f, 22f), $"剩余敌人: {RemainingEnemyCount}");
        GUI.Label(new Rect(28f, 76f, 240f, 22f), $"已获取战利品: {_lootCollectedCount}");

        if (_activeExtractionPoint == null)
        {
            GUI.Label(new Rect(28f, 98f, 240f, 22f), "目标: 前往撤离点");
        }
        else if (RequireLootBeforeExtraction && _lootCollectedCount <= 0)
        {
            GUI.Label(new Rect(28f, 98f, 240f, 22f), "撤离条件: 至少带走一件战利品");
        }
        else
        {
            float remainingTime = Mathf.Max(0f, _activeExtractionPoint.ExtractionDurationSeconds - _extractionProgressSeconds);
            GUI.Label(new Rect(28f, 98f, 240f, 22f), $"撤离中: {remainingTime:0.0}s");
        }

        if (!string.IsNullOrEmpty(_recentEventMessage))
        {
            GUI.Label(new Rect(28f, 120f, 240f, 22f), _recentEventMessage);
        }
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
            : "主角已阵亡";

        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 24f, panelRect.width - 48f, 26f), title);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 56f, panelRect.width - 48f, 24f), detail);
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.y + 92f, panelRect.width - 48f, 24f), $"按 {RestartKey} 重新开始");
    }
}
