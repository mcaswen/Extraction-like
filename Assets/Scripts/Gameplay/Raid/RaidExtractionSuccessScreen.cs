using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 撤离成功结算页需要展示的统计数据
/// </summary>
public struct RaidExtractionSummary
{
    public readonly float ElapsedSeconds;
    public readonly int LootItemCount;
    public readonly int TotalValue;

    /// <summary>
    /// 创建撤离结算统计数据，并把负数输入夹到有效范围
    /// </summary>
    /// <param name="elapsedSeconds">本局用时秒数</param>
    /// <param name="lootItemCount">撤离携带物品数量</param>
    /// <param name="totalValue">撤离物品总价值</param>
    public RaidExtractionSummary(float elapsedSeconds, int lootItemCount, int totalValue)
    {
        ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        LootItemCount = Mathf.Max(0, lootItemCount);
        TotalValue = Mathf.Max(0, totalValue);
    }
}

/// <summary>
/// 撤离成功后的结算界面控制器
/// </summary>
public sealed class RaidExtractionSuccessScreen : MonoBehaviour
{
    [Header("Copy")]
    [SerializeField] private string _titleTop = "EXTRACTION";
    [SerializeField] private string _titleBottom = "SUCCESS";
    [SerializeField] private string _timeLabel = "TIME";
    [SerializeField] private string _lootLabel = "LOOT ITEMS";
    [SerializeField] private string _valueLabel = "TOTAL VALUE";
    [SerializeField] private string _confirmLabel = "CONFIRM";

    [Header("Animation")]
    [SerializeField] private float _whiteFadeInSeconds = 0.55f;
    [SerializeField] private float _whiteFadeOutSeconds = 0.65f;
    [SerializeField] private float _rowFadeSeconds = 0.32f;
    [SerializeField] private float _rowStaggerSeconds = 0.08f;

    [Header("Runtime References")]
    [SerializeField] private CanvasGroup _contentGroup;
    [SerializeField] private Image _whiteFlashImage;
    [SerializeField] private Text _titleTopText;
    [SerializeField] private Text _titleBottomText;
    [SerializeField] private Text _timeText;
    [SerializeField] private Text _lootText;
    [SerializeField] private Text _valueText;
    [SerializeField] private Text _confirmText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private CanvasGroup[] _revealRows;

    private Action _confirmAction;
    private Coroutine _playRoutine;

    private void Awake()
    {
        CacheReferences();
        BindConfirmButton();
        SetImmediateHidden();
    }

    /// <summary>
    /// 显示结算页并绑定确认按钮回调
    /// </summary>
    /// <param name="summary">本次撤离统计数据</param>
    /// <param name="confirmAction">确认按钮触发回调</param>
    public void Show(RaidExtractionSummary summary, Action confirmAction)
    {
        _confirmAction = confirmAction;
        CacheReferences();
        BindConfirmButton();
        EnsureEventSystemExists();
        ApplySummary(summary);

        gameObject.SetActive(true);

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private void CacheReferences()
    {
        if (_contentGroup == null)
            _contentGroup = FindComponent<CanvasGroup>("Content");

        if (_whiteFlashImage == null)
            _whiteFlashImage = FindComponent<Image>("WhiteFlash");

        if (_titleTopText == null)
            _titleTopText = FindComponent<Text>("TitleExtraction");

        if (_titleBottomText == null)
            _titleBottomText = FindComponent<Text>("TitleSuccess");

        if (_timeText == null)
            _timeText = FindComponent<Text>("TimeRow");

        if (_lootText == null)
            _lootText = FindComponent<Text>("LootRow");

        if (_valueText == null)
            _valueText = FindComponent<Text>("ValueRow");

        if (_confirmText == null)
            _confirmText = FindComponent<Text>("ConfirmLabel");

        if (_confirmButton == null)
            _confirmButton = FindComponent<Button>("ConfirmButton");

        if (_revealRows == null || _revealRows.Length == 0)
        {
            _revealRows = new[]
            {
                FindComponent<CanvasGroup>("TimeRow"),
                FindComponent<CanvasGroup>("LootRow"),
                FindComponent<CanvasGroup>("ValueRow"),
                FindComponent<CanvasGroup>("ConfirmButton")
            };
        }
    }

    private void BindConfirmButton()
    {
        if (_confirmButton == null)
            return;

        _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        _confirmButton.onClick.AddListener(HandleConfirmClicked);
    }

    private void ApplySummary(RaidExtractionSummary summary)
    {
        SetText(_titleTopText, _titleTop);
        SetText(_titleBottomText, _titleBottom);
        SetText(_timeText, $"{_timeLabel}: {FormatDuration(summary.ElapsedSeconds)}");
        SetText(_lootText, $"{_lootLabel}: {summary.LootItemCount}");
        SetText(_valueText, $"{_valueLabel}: {summary.TotalValue:N0}");
        SetText(_confirmText, _confirmLabel);
    }

    private IEnumerator PlayRoutine()
    {
        SetContentAlpha(0f);
        SetWhiteAlpha(0f);
        SetRowsAlpha(0f);
        SetConfirmInteractable(false);

        // 先白闪再逐行显示数据，让结算页在暂停时间下仍能播放节奏
        yield return FadeWhite(0f, 1f, _whiteFadeInSeconds);

        SetContentAlpha(1f);

        yield return FadeWhite(1f, 0f, _whiteFadeOutSeconds);

        for (int i = 0; i < _revealRows.Length; i++)
        {
            CanvasGroup row = _revealRows[i];
            if (row == null)
                continue;

            yield return FadeCanvasGroup(row, 0f, 1f, _rowFadeSeconds);

            if (_rowStaggerSeconds > 0f)
                yield return new WaitForSecondsRealtime(_rowStaggerSeconds);
        }

        SetConfirmInteractable(true);
        _playRoutine = null;
    }

    private IEnumerator FadeWhite(float from, float to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetWhiteAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetWhiteAlpha(to);
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private void SetImmediateHidden()
    {
        SetContentAlpha(0f);
        SetWhiteAlpha(0f);
        SetRowsAlpha(0f);
        SetConfirmInteractable(false);
    }

    private void SetContentAlpha(float alpha)
    {
        if (_contentGroup == null)
            return;

        _contentGroup.alpha = alpha;
        _contentGroup.interactable = alpha > 0.99f;
        _contentGroup.blocksRaycasts = alpha > 0.99f;
    }

    private void SetWhiteAlpha(float alpha)
    {
        if (_whiteFlashImage == null)
            return;

        Color color = _whiteFlashImage.color;
        color.a = alpha;
        _whiteFlashImage.color = color;
    }

    private void SetRowsAlpha(float alpha)
    {
        if (_revealRows == null)
            return;

        for (int i = 0; i < _revealRows.Length; i++)
        {
            if (_revealRows[i] != null)
                _revealRows[i].alpha = alpha;
        }
    }

    private void SetConfirmInteractable(bool interactable)
    {
        if (_confirmButton != null)
            _confirmButton.interactable = interactable;
    }

    private void HandleConfirmClicked()
    {
        _confirmAction?.Invoke();
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform target = FindChildRecursive(transform, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void SetText(Text target, string text)
    {
        if (target != null)
            target.text = text;
    }

    private static string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds / 60 % 60;
        int secs = totalSeconds % 60;

        return hours > 0
            ? $"{hours:00}:{minutes:00}:{secs:00}"
            : $"{minutes:00}:{secs:00}";
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
            return;

        // 结算页可能由运行时动态生成，缺少事件系统时按钮无法接收输入
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
