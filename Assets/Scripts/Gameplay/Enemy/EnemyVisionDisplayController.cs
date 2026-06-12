using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 敌人视野显示开关控制器。
/// 提供全局可见性状态、快捷键切换和运行时 UI Toggle 绑定。
/// </summary>
public sealed class EnemyVisionDisplayController : MonoBehaviour
{
    private static EnemyVisionDisplayController _instance;

    [SerializeField]
    private bool _showEnemyVision = true;

    [SerializeField]
    private KeyCode _toggleKey = KeyCode.V;

    [SerializeField]
    private bool _createRuntimeToggle = true;

    private Toggle _runtimeToggle;
    private bool _isUpdatingToggle;

    /// <summary>
    /// 视野显示控制器单例；场景中不存在时会运行时创建。
    /// </summary>
    public static EnemyVisionDisplayController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EnemyVisionDisplayController>();
            }

            if (_instance == null)
            {
                GameObject controllerObject = new GameObject("EnemyVisionDisplayController");
                _instance = controllerObject.AddComponent<EnemyVisionDisplayController>();
            }

            return _instance;
        }
    }

    /// <summary>
    /// 当前敌人视野扇形是否全局可见。
    /// </summary>
    public static bool IsVisionVisible => Instance._showEnemyVision;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (_createRuntimeToggle)
        {
            EnsureRuntimeToggle();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            SetVisionVisible(!_showEnemyVision);
        }
    }

    /// <summary>
    /// 设置敌人视野扇形的全局可见状态。
    /// </summary>
    /// <param name="isVisible">是否显示敌人视野。</param>
    public void SetVisionVisible(bool isVisible)
    {
        if (_showEnemyVision == isVisible)
        {
            SyncToggleState();
            return;
        }

        _showEnemyVision = isVisible;
        SyncToggleState();
    }

    /// <summary>
    /// 切换敌人视野扇形的全局可见状态。
    /// </summary>
    public void ToggleVisionVisible()
    {
        SetVisionVisible(!_showEnemyVision);
    }

    /// <summary>
    /// 将外部 UI Toggle 绑定到敌人视野显示开关。
    /// </summary>
    /// <param name="toggle">用于控制视野显示的 Toggle。</param>
    public void BindToggle(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        if (_runtimeToggle != null)
        {
            _runtimeToggle.onValueChanged.RemoveListener(SetVisionVisible);
        }

        _runtimeToggle = toggle;
        _runtimeToggle.onValueChanged.RemoveListener(SetVisionVisible);
        _runtimeToggle.onValueChanged.AddListener(SetVisionVisible);
        SyncToggleState();
    }

    private void EnsureRuntimeToggle()
    {
        if (_runtimeToggle != null)
        {
            return;
        }

        Canvas canvas = null;
        GameObject existingCanvasObject = GameObject.Find("EnemyVisionCanvas");
        if (existingCanvasObject != null)
        {
            canvas = existingCanvasObject.GetComponent<Canvas>();
        }

        if (canvas == null)
        {
            // 没有设计器配置的 UI 时，创建一个轻量运行时开关方便测试视野。
            GameObject canvasObject = new GameObject(
                "EnemyVisionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            AdaptiveCanvasScaler.Configure(scaler, new Vector2(1920f, 1080f));
        }

        GameObject root = new GameObject(
            "EnemyVisionToggle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Toggle));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(20f, -72f);
        rootRect.sizeDelta = new Vector2(36f, 36f);

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);

        GameObject checkmarkObject = new GameObject(
            "Checkmark",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.SetParent(rootRect, false);
        checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
        checkmarkRect.pivot = new Vector2(0f, 0.5f);
        checkmarkRect.anchoredPosition = new Vector2(9f, 0f);
        checkmarkRect.sizeDelta = new Vector2(20f, 20f);

        Image checkmark = checkmarkObject.GetComponent<Image>();
        checkmark.color = new Color(0.18f, 0.86f, 1f, 0.9f);

        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkmark;
        BindToggle(toggle);
    }

    private void SyncToggleState()
    {
        if (_runtimeToggle == null || _isUpdatingToggle)
        {
            return;
        }

        _isUpdatingToggle = true;
        _runtimeToggle.isOn = _showEnemyVision;
        _isUpdatingToggle = false;
    }
}
