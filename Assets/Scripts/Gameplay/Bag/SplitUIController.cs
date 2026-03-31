using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 堆叠拆分面板。
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
    /// 打开拆分窗口并绑定目标物品。
    /// </summary>
    public void OpenSplitWindow(DraggableItemUI item)
    {
        if (item == null || item.CurrentAmount <= 1)
        {
            return;
        }

        _targetItem = item;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PositionNextToItem(item);

        SplitSlider.minValue = 1;
        SplitSlider.maxValue = item.CurrentAmount - 1;
        SplitSlider.value = Mathf.FloorToInt(item.CurrentAmount / 2f);
        UpdateAmountText();
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
        _targetItem = null;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_parentCanvas == null)
        {
            return;
        }

        _rectTransform.anchoredPosition += eventData.delta / _parentCanvas.scaleFactor;
    }

    private void OnSliderValueChanged(float _)
    {
        UpdateAmountText();
    }

    private void OnConfirmClicked()
    {
        if (_targetItem != null)
        {
            _targetItem.ExecuteSplit((int)SplitSlider.value);
        }

        CloseWindow();
    }

    private void UpdateAmountText()
    {
        if (_targetItem == null || AmountText == null || SplitSlider == null)
        {
            return;
        }

        AmountText.text = $"拆分: {SplitSlider.value} / {_targetItem.CurrentAmount}";
    }

    private void PositionNextToItem(DraggableItemUI item)
    {
        RectTransform itemRect = item.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners);

        transform.position = corners[2];
        _rectTransform.anchoredPosition += new Vector2(10f, 0f);
    }
}
