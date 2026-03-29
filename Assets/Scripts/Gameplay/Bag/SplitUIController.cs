using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 必须引入以支持拖拽

public class SplitUIController : MonoBehaviour, IDragHandler
{
    public static SplitUIController Instance { get; private set; }

    [Header("UI 组件引用")]
    public Slider SplitSlider;
    public Text AmountText;
    public Button ConfirmButton;
    public Button CloseButton;

    private DraggableItemUI _targetItem;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    void Awake()
    {
        Instance = this;
        _rectTransform = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        SplitSlider.onValueChanged.AddListener(OnSliderValueChanged);
        ConfirmButton.onClick.AddListener(OnConfirmClicked);

        if (CloseButton != null) CloseButton.onClick.AddListener(CloseWindow);

        gameObject.SetActive(false);
    }

    public void OpenSplitWindow(DraggableItemUI item)
    {
        _targetItem = item;

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 浮在最上层

        // =========================================================
        // 【精准定位法】：获取物品在屏幕上的四个绝对顶角坐标
        // corners[0]=左下, [1]=左上, [2]=右上, [3]=右下
        // =========================================================
        RectTransform itemRect = item.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners);

        // 将窗口的左上角，直接对齐到物品的右上角 (corners[2])，并向右偏移 10 个像素防遮挡
        transform.position = corners[2];
        _rectTransform.anchoredPosition += new Vector2(10, 0);

        // 初始化滑块
        SplitSlider.minValue = 1;
        SplitSlider.maxValue = item.CurrentAmount - 1;
        SplitSlider.value = Mathf.FloorToInt(item.CurrentAmount / 2f);

        UpdateAmountText();
    }

    private void OnSliderValueChanged(float value)
    {
        UpdateAmountText();
    }

    private void UpdateAmountText()
    {
        if (_targetItem != null && AmountText != null)
        {
            AmountText.text = $"拆分: {SplitSlider.value} / {_targetItem.CurrentAmount}";
        }
    }

    private void OnConfirmClicked()
    {
        if (_targetItem != null)
        {
            int splitAmount = (int)SplitSlider.value;
            _targetItem.ExecuteSplit(splitAmount);
        }
        CloseWindow();
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
    }

    // =========================================================
    // 【悬浮窗拖拽算法】：无视层级，直接累加鼠标物理移动距离
    // =========================================================
    public void OnDrag(PointerEventData eventData)
    {
        if (_parentCanvas == null) return;

        // 按照 Canvas 的缩放比例完美移动窗口，绝对跟手！
        _rectTransform.anchoredPosition += eventData.delta / _parentCanvas.scaleFactor;
    }
}