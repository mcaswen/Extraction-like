using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 天赋树节点悬浮目标。
/// 鼠标进入节点时显示详情面板，并让面板跟随鼠标。
/// </summary>
public sealed class TalentTreeNodeHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Hover Panel")]
    [SerializeField] private GameObject hoverPanel;
    [SerializeField] private Vector2 pointerOffset = new Vector2(24f, -24f);
    [SerializeField] private Vector2 parentPadding = new Vector2(16f, 16f);
    [SerializeField] private bool followPointer = true;
    [SerializeField] private bool clampToParent = true;
    [SerializeField] private bool hidePanelOnEnable = true;
    [SerializeField] private bool hidePanelOnExit = true;

    private RectTransform _hoverPanelRect;
    private RectTransform _hoverPanelParentRect;
    private bool _isPointerInside;

    private void OnEnable()
    {
        if (hidePanelOnEnable)
        {
            SetHoverPanelVisible(false);
        }
    }

    private void OnDisable()
    {
        _isPointerInside = false;

        if (hidePanelOnExit)
        {
            SetHoverPanelVisible(false);
        }
    }

    /// <summary>
    /// 鼠标进入节点时显示悬浮详情面板
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerInside = true;
        SetHoverPanelVisible(true);
        UpdateHoverPanelPosition(eventData);
    }

    /// <summary>
    /// 鼠标在节点上移动时同步详情面板位置
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerMove(PointerEventData eventData)
    {
        if (_isPointerInside && followPointer)
        {
            UpdateHoverPanelPosition(eventData);
        }
    }

    /// <summary>
    /// 鼠标离开节点时隐藏悬浮详情面板
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerInside = false;

        if (hidePanelOnExit)
        {
            SetHoverPanelVisible(false);
        }
    }

    private void UpdateHoverPanelPosition(PointerEventData eventData)
    {
        if (hoverPanel == null || eventData == null)
        {
            return;
        }

        CachePanelReferences();
        if (_hoverPanelRect == null || _hoverPanelParentRect == null)
        {
            return;
        }

        // 先把屏幕坐标转换到悬浮面板父节点的本地坐标系
        Camera eventCamera = eventData.pressEventCamera != null
            ? eventData.pressEventCamera
            : eventData.enterEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _hoverPanelParentRect,
                eventData.position,
                eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        // 再应用鼠标偏移和边界裁剪，避免面板压住指针或跑出画布
        Vector2 anchoredPosition = localPoint + pointerOffset;
        if (clampToParent)
        {
            anchoredPosition = ClampToParent(anchoredPosition);
        }

        _hoverPanelRect.anchoredPosition = anchoredPosition;
    }

    private void CachePanelReferences()
    {
        if (_hoverPanelRect == null)
        {
            _hoverPanelRect = hoverPanel != null ? hoverPanel.transform as RectTransform : null;
        }

        if (_hoverPanelParentRect == null && _hoverPanelRect != null)
        {
            _hoverPanelParentRect = _hoverPanelRect.parent as RectTransform;
        }
    }

    private Vector2 ClampToParent(Vector2 anchoredPosition)
    {
        Rect parentRect = _hoverPanelParentRect.rect;
        Rect panelRect = _hoverPanelRect.rect;
        Vector2 pivot = _hoverPanelRect.pivot;

        // 根据面板 pivot 计算可放置范围，保证不同 pivot 设置都能正确裁剪
        float minX = parentRect.xMin + parentPadding.x + panelRect.width * pivot.x;
        float maxX = parentRect.xMax - parentPadding.x - panelRect.width * (1f - pivot.x);
        float minY = parentRect.yMin + parentPadding.y + panelRect.height * pivot.y;
        float maxY = parentRect.yMax - parentPadding.y - panelRect.height * (1f - pivot.y);

        // 父容器比面板还小时退回居中，避免 Clamp 出现反向区间
        if (minX > maxX)
        {
            anchoredPosition.x = parentRect.center.x;
        }
        else
        {
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            anchoredPosition.y = parentRect.center.y;
        }
        else
        {
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        }

        return anchoredPosition;
    }

    private void SetHoverPanelVisible(bool visible)
    {
        if (hoverPanel == null || hoverPanel.activeSelf == visible)
        {
            return;
        }

        hoverPanel.SetActive(visible);
        if (visible)
        {
            hoverPanel.transform.SetAsLastSibling();
        }
    }
}
