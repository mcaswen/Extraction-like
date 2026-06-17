using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让屏幕空间界面在不同比例和小窗口下保持可读尺寸
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class AdaptiveCanvasScaler : MonoBehaviour
{
    [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float _matchWidthOrHeight = 0.5f;
    [SerializeField, Min(0.1f)] private float _minimumScale = 0.75f;
    [SerializeField, Min(0.1f)] private float _maximumScale = 2f;
    [SerializeField] private bool _clampScale = true;
    [SerializeField] private bool _applyEveryFrame;

    private CanvasScaler _scaler;
    private Vector2Int _lastScreenSize = new Vector2Int(-1, -1);

    /// <summary>
    /// 为指定画布缩放器创建或更新自适应缩放组件
    /// </summary>
    /// <param name="scaler">目标画布缩放器</param>
    /// <param name="referenceResolution">设计参考分辨率</param>
    /// <param name="matchWidthOrHeight">宽高匹配权重</param>
    /// <param name="minimumScale">最小缩放倍率</param>
    /// <param name="maximumScale">最大缩放倍率</param>
    /// <returns>创建或复用的自适应缩放组件</returns>
    public static AdaptiveCanvasScaler Configure(
        CanvasScaler scaler,
        Vector2 referenceResolution,
        float matchWidthOrHeight = 0.5f,
        float minimumScale = 0.75f,
        float maximumScale = 2f)
    {
        if (scaler == null)
        {
            return null;
        }

        AdaptiveCanvasScaler adaptiveScaler = scaler.GetComponent<AdaptiveCanvasScaler>();
        if (adaptiveScaler == null)
        {
            adaptiveScaler = scaler.gameObject.AddComponent<AdaptiveCanvasScaler>();
        }

        adaptiveScaler._referenceResolution = SanitizeResolution(referenceResolution);
        adaptiveScaler._matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
        adaptiveScaler._minimumScale = Mathf.Max(0.1f, minimumScale);
        adaptiveScaler._maximumScale = Mathf.Max(adaptiveScaler._minimumScale, maximumScale);
        adaptiveScaler._clampScale = true;
        adaptiveScaler.ApplyNow(true);
        return adaptiveScaler;
    }

    private void Awake()
    {
        ApplyNow(true);
    }

    private void OnEnable()
    {
        ApplyNow(true);
    }

    private void Update()
    {
        if (_applyEveryFrame)
        {
            ApplyNow(true);
            return;
        }

        if (HasScreenSizeChanged())
        {
            ApplyNow(false);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _referenceResolution = SanitizeResolution(_referenceResolution);
        _matchWidthOrHeight = Mathf.Clamp01(_matchWidthOrHeight);
        _minimumScale = Mathf.Max(0.1f, _minimumScale);
        _maximumScale = Mathf.Max(_minimumScale, _maximumScale);
    }
#endif

    /// <summary>
    /// 立即把当前屏幕尺寸对应的缩放参数写回画布缩放器
    /// </summary>
    /// <param name="force">是否跳过屏幕尺寸变化判断并强制应用</param>
    public void ApplyNow(bool force)
    {
        if (!force && !HasScreenSizeChanged())
        {
            return;
        }

        CanvasScaler scaler = ResolveScaler();
        if (scaler == null)
        {
            return;
        }

        Vector2 screenSize = GetScreenSize();
        Vector2 baseReferenceResolution = SanitizeResolution(_referenceResolution);
        float match = Mathf.Clamp01(_matchWidthOrHeight);
        Vector2 effectiveReferenceResolution = baseReferenceResolution;

        if (_clampScale)
        {
            // 画布缩放器只能通过参考分辨率间接夹住缩放值，这里把目标缩放反推回有效参考分辨率
            float unclampedScale = CalculateScale(screenSize, baseReferenceResolution, match);
            float targetScale = Mathf.Clamp(
                unclampedScale,
                Mathf.Max(0.1f, _minimumScale),
                Mathf.Max(_minimumScale, _maximumScale));

            if (!Mathf.Approximately(unclampedScale, targetScale))
            {
                float referenceMultiplier = Mathf.Max(0.01f, unclampedScale / targetScale);
                effectiveReferenceResolution = baseReferenceResolution * referenceMultiplier;
            }
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = effectiveReferenceResolution;
        scaler.matchWidthOrHeight = match;

        _lastScreenSize = new Vector2Int(Mathf.RoundToInt(screenSize.x), Mathf.RoundToInt(screenSize.y));
    }

    private CanvasScaler ResolveScaler()
    {
        if (_scaler == null)
        {
            _scaler = GetComponent<CanvasScaler>();
        }

        return _scaler;
    }

    private bool HasScreenSizeChanged()
    {
        return Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y;
    }

    private static Vector2 GetScreenSize()
    {
        return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
    }

    private static Vector2 SanitizeResolution(Vector2 resolution)
    {
        return new Vector2(Mathf.Max(1f, resolution.x), Mathf.Max(1f, resolution.y));
    }

    private static float CalculateScale(Vector2 screenSize, Vector2 referenceResolution, float matchWidthOrHeight)
    {
        float widthScale = Mathf.Max(0.01f, screenSize.x / referenceResolution.x);
        float heightScale = Mathf.Max(0.01f, screenSize.y / referenceResolution.y);
        float logWidth = Mathf.Log(widthScale, 2f);
        float logHeight = Mathf.Log(heightScale, 2f);
        float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
        return Mathf.Pow(2f, logWeightedAverage);
    }
}
