using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 堆叠拆分面板
/// </summary>
public class SplitUIController : MonoBehaviour, IDragHandler
{
    public static SplitUIController Instance { get; private set; }

    [Header("View References")]
    public Slider SplitSlider;
    public Text AmountText;
    public Button ConfirmButton;
    public Button CloseButton;

    private DraggableItemUI _targetItem;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;
    private int _openedFrame = -1;

    private void Awake()
    {
        Instance = this;
        _rectTransform = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        if (SplitSlider != null)
        {
            SplitSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (CloseButton != null)
        {
            CloseButton.onClick.AddListener(CloseWindow);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 打开拆分窗口并绑定目标物品
    /// </summary>
    /// <param name="item">要进行拆分的物品</param>
    public void OpenSplitWindow(DraggableItemUI item)
    {
        if (item == null || item.CurrentAmount <= 1)
        {
            return;
        }

        _targetItem = item;
        gameObject.SetActive(true);
        _openedFrame = Time.frameCount;
        transform.SetAsLastSibling();
        PositionNextToItem(item);

        SplitSlider.minValue = 1;
        SplitSlider.maxValue = item.CurrentAmount - 1;
        SplitSlider.value = Mathf.FloorToInt(item.CurrentAmount / 2f);
        UpdateAmountText();
    }

    private void Update()
    {
        if (_targetItem == null ||
            Time.frameCount == _openedFrame ||
            !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsPointerInsideWindow())
        {
            return;
        }

        CloseWindow();
    }

    /// <summary>
    /// 关闭拆分窗口并清理当前目标物品引用
    /// </summary>
    public void CloseWindow()
    {
        gameObject.SetActive(false);
        _targetItem = null;
    }

    /// <summary>
    /// 处理拆分面板拖拽，让窗口可以在画布内移动
    /// </summary>
    /// <param name="eventData">当前拖拽事件数据</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (_parentCanvas == null)
        {
            return;
        }

        _rectTransform.anchoredPosition += eventData.delta / _parentCanvas.scaleFactor;
    }

    // 滑条变化时同步刷新拆分数量文案
    private void OnSliderValueChanged(float _)
    {
        UpdateAmountText();
    }

    // 确认拆分后把当前滑条值交给目标物品执行拆分
    private void OnConfirmClicked()
    {
        if (_targetItem != null)
        {
            _targetItem.ExecuteSplit((int)SplitSlider.value);
        }

        CloseWindow();
    }

    private bool IsPointerInsideWindow()
    {
        if (_rectTransform == null)
        {
            return false;
        }

        Camera eventCamera = ResolveEventCamera();
        return RectTransformUtility.RectangleContainsScreenPoint(
            _rectTransform,
            Input.mousePosition,
            eventCamera);
    }

    private Camera ResolveEventCamera()
    {
        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        if (_parentCanvas == null || _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return _parentCanvas.worldCamera != null ? _parentCanvas.worldCamera : Camera.main;
    }

    // 根据当前滑条值刷新面板上的拆分数量显示
    private void UpdateAmountText()
    {
        if (_targetItem == null || AmountText == null || SplitSlider == null)
        {
            return;
        }

        AmountText.text = $"拆分: {SplitSlider.value} / {_targetItem.CurrentAmount}";
    }

    // 把拆分窗口摆到目标物品右上侧，降低首次打开时遮挡物品的概率
    private void PositionNextToItem(DraggableItemUI item)
    {
        RectTransform itemRect = item.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners);

        transform.position = corners[2];
        _rectTransform.anchoredPosition += new Vector2(10f, 0f);
    }
}
