using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包物品信息面板。
/// 展示点击物品的静态配置，以及数量、搜索状态等运行时信息。
/// </summary>
public class InventoryItemInfoPanelController : MonoBehaviour, IDragHandler
{
    private static InventoryItemInfoPanelController _instance;

    public static InventoryItemInfoPanelController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<InventoryItemInfoPanelController>(true);
                if (_instance != null)
                {
                    _instance.EnsureInitialized();
                }
            }

            return _instance;
        }
        private set => _instance = value;
    }

    [Header("View References")]
    public Image BackgroundImage;
    public Image MarkerImage;
    public Image ItemIconImage;
    public Text NameText;
    public Text RarityText;
    public Text DetailText;
    public Button SplitButton;
    public Button CloseButton;

    [Header("Layout")]
    public Vector2 PreferredOffset = new Vector2(12f, 0f);
    public Vector2 ScreenPadding = new Vector2(16f, 16f);

    private readonly StringBuilder _detailBuilder = new StringBuilder(256);
    private readonly Dictionary<ItemType, string> _itemTypeLabels = new Dictionary<ItemType, string>
    {
        { ItemType.Weapon, "武器" },
        { ItemType.Ammo, "弹药" },
        { ItemType.Medical, "医疗" },
        { ItemType.Rig, "胸挂" },
        { ItemType.Bag, "背包" },
        { ItemType.Junk, "战利品" },
        { ItemType.Equipment, "装备" }
    };

    private readonly Dictionary<ItemRarity, string> _rarityLabels = new Dictionary<ItemRarity, string>
    {
        { ItemRarity.Common, "普通" },
        { ItemRarity.Uncommon, "优秀" },
        { ItemRarity.Rare, "稀有" },
        { ItemRarity.Epic, "史诗" },
        { ItemRarity.Legendary, "传说" }
    };

    private readonly Dictionary<EquipmentSlotKind, string> _equipmentKindLabels = new Dictionary<EquipmentSlotKind, string>
    {
        { EquipmentSlotKind.None, "无" },
        { EquipmentSlotKind.Head, "头部" },
        { EquipmentSlotKind.Body, "身体" },
        { EquipmentSlotKind.Face, "面部" },
        { EquipmentSlotKind.Headphone, "耳机" },
        { EquipmentSlotKind.Totem, "图腾" }
    };

    private RectTransform _rectTransform;
    private Canvas _parentCanvas;
    private DraggableItemUI _targetItem;
    private bool _isInitialized;
    private int _shownFrame = -1;

    private void Awake()
    {
        Instance = this;
        EnsureInitialized();

        if (_targetItem == null)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 显示指定物品的信息。
    /// </summary>
    /// <param name="item">被点击的物品视图。</param>
    /// <param name="eventData">当前指针事件，用于定位面板。</param>
    public void Show(DraggableItemUI item, PointerEventData eventData)
    {
        EnsureInitialized();

        if (item == null || item.ItemData == null)
        {
            Hide();
            return;
        }

        _targetItem = item;
        ApplyItemData(item);
        gameObject.SetActive(true);
        _shownFrame = Time.frameCount;
        transform.SetAsLastSibling();
        PositionNextToItem(item, eventData);
    }

    /// <summary>
    /// 隐藏面板并清理当前目标。
    /// </summary>
    public void Hide()
    {
        _targetItem = null;
        gameObject.SetActive(false);
    }

    public void HideIfTarget(DraggableItemUI item)
    {
        if (_targetItem == item)
        {
            Hide();
        }
    }

    private void Update()
    {
        if (_targetItem == null ||
            Time.frameCount == _shownFrame ||
            !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsPointerInsideRect(_rectTransform) ||
            IsPointerInsideRect(_targetItem.GetComponent<RectTransform>()))
        {
            return;
        }

        Hide();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _rectTransform = transform as RectTransform;
        _parentCanvas = GetComponentInParent<Canvas>();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        DisableRaycastTarget(BackgroundImage);
        DisableRaycastTarget(MarkerImage);
        DisableRaycastTarget(ItemIconImage);
        DisableRaycastTarget(NameText);
        DisableRaycastTarget(RarityText);
        DisableRaycastTarget(DetailText);

        if (SplitButton != null)
        {
            SplitButton.onClick.RemoveListener(OnSplitClicked);
            SplitButton.onClick.AddListener(OnSplitClicked);
        }

        if (CloseButton != null)
        {
            CloseButton.onClick.RemoveListener(Hide);
            CloseButton.gameObject.SetActive(false);
        }

        _isInitialized = true;
    }

    private static void DisableRaycastTarget(Graphic graphic)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = false;
        }
    }

    /// <summary>
    /// 允许玩家拖动信息面板，临时避开遮挡。
    /// </summary>
    /// <param name="eventData">当前拖拽事件。</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (_rectTransform == null || _parentCanvas == null)
        {
            return;
        }

        _rectTransform.anchoredPosition += eventData.delta / _parentCanvas.scaleFactor;
        ClampPanelToCanvas();
    }

    private void ApplyItemData(DraggableItemUI item)
    {
        InventoryItemData data = item.ItemData;

        if (ItemIconImage != null)
        {
            ItemIconImage.sprite = data.ItemIcon;
            ItemIconImage.enabled = data.ItemIcon != null;
            ItemIconImage.preserveAspect = true;
            ItemIconImage.color = Color.white;
        }

        if (NameText != null)
        {
            NameText.text = string.IsNullOrWhiteSpace(data.ItemName) ? data.name : data.ItemName;
        }

        if (RarityText != null)
        {
            string qualityLabel = data.TotemQuality != TotemQuality.None
                ? InventoryItemData.GetTotemQualityLabel(data.TotemQuality)
                : string.Empty;
            RarityText.text = !string.IsNullOrWhiteSpace(qualityLabel)
                ? qualityLabel
                : GetRarityLabel(data.Rarity);
            RarityText.color = GetRarityTextColor(data.Rarity);
        }

        if (DetailText != null)
        {
            DetailText.text = BuildDetailText(item);
        }

        if (SplitButton != null)
        {
            bool canSplit = data.IsStackable && item.CurrentAmount > 1;
            SplitButton.gameObject.SetActive(canSplit);
        }
    }

    private string BuildDetailText(DraggableItemUI item)
    {
        InventoryItemData data = item.ItemData;
        _detailBuilder.Clear();

        AppendLine("类型", GetItemTypeLabel(data.Type));
        if (data.Type == ItemType.Equipment && data.EquipmentKind != EquipmentSlotKind.None)
        {
            AppendLine("装备部位", GetEquipmentKindLabel(data.EquipmentKind));
        }

        AppendLine("占格", $"{Mathf.Max(1, data.Width)} x {Mathf.Max(1, data.Height)}");

        if (data.TotemQuality != TotemQuality.None)
        {
            AppendLine("图腾品质", InventoryItemData.GetTotemQualityLabel(data.TotemQuality));
        }

        int amount = Mathf.Max(1, item.CurrentAmount);
        if (data.IsStackable)
        {
            AppendLine("数量", $"{amount} / {Mathf.Max(1, data.MaxStack)}");
        }
        else
        {
            AppendLine("数量", amount.ToString());
        }

        AppendLine("售价", Mathf.Max(0, data.SellPrice).ToString());
        AppendLine("负重", FormatWeight(data.CarryWeight));
        if (amount > 1)
        {
            AppendLine("总负重", FormatWeight(data.CarryWeight * amount));
        }

        if (data.MagicUnlock != MagicUnlockType.None)
        {
            AppendLine("魔法解锁", data.MagicUnlock.ToString());
            AppendLine("符文点数", Mathf.Max(1, data.RunePatternPoints).ToString());
        }

        string totemEffectSummary = data.GetTotemEffectSummary();
        if (!string.IsNullOrWhiteSpace(totemEffectSummary))
        {
            AppendLine("图腾效果", totemEffectSummary);
        }

        if (data.RequiresSearchInLootContainer)
        {
            AppendLine("搜索耗时", $"{data.GetSearchDurationSeconds():0.##} 秒");
        }

        if (data.ContainerColumns > 0 && data.ContainerRows > 0)
        {
            AppendLine("容器容量", $"{data.ContainerColumns} x {data.ContainerRows}");
        }

        return _detailBuilder.ToString().TrimEnd();
    }

    private void AppendLine(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (_detailBuilder.Length > 0)
        {
            _detailBuilder.AppendLine();
        }

        _detailBuilder.Append(label);
        _detailBuilder.Append(": ");
        _detailBuilder.Append(value);
    }

    private void OnSplitClicked()
    {
        if (_targetItem == null)
        {
            Hide();
            return;
        }

        DraggableItemUI target = _targetItem;
        Hide();
        SplitUIController.Instance?.OpenSplitWindow(target);
    }

    private void PositionNextToItem(DraggableItemUI item, PointerEventData eventData)
    {
        if (_rectTransform == null)
        {
            return;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        itemRect.GetWorldCorners(corners);
        _rectTransform.position = corners[2] + new Vector3(PreferredOffset.x, PreferredOffset.y, 0f);
        ClampPanelToCanvas(eventData != null ? eventData.pressEventCamera : null);
    }

    private void ClampPanelToCanvas(Camera eventCamera = null)
    {
        if (_rectTransform == null)
        {
            return;
        }

        RectTransform canvasRect = ResolveCanvasRect();
        if (canvasRect == null)
        {
            return;
        }

        Vector3[] panelCorners = new Vector3[4];
        Vector3[] canvasCorners = new Vector3[4];
        _rectTransform.GetWorldCorners(panelCorners);
        canvasRect.GetWorldCorners(canvasCorners);

        Vector3 adjustment = Vector3.zero;
        float minX = canvasCorners[0].x + ScreenPadding.x;
        float maxX = canvasCorners[2].x - ScreenPadding.x;
        float minY = canvasCorners[0].y + ScreenPadding.y;
        float maxY = canvasCorners[2].y - ScreenPadding.y;

        if (panelCorners[2].x > maxX)
        {
            adjustment.x = maxX - panelCorners[2].x;
        }
        else if (panelCorners[0].x < minX)
        {
            adjustment.x = minX - panelCorners[0].x;
        }

        if (panelCorners[2].y > maxY)
        {
            adjustment.y = maxY - panelCorners[2].y;
        }
        else if (panelCorners[0].y < minY)
        {
            adjustment.y = minY - panelCorners[0].y;
        }

        if (adjustment != Vector3.zero)
        {
            _rectTransform.position += adjustment;
        }
    }

    private RectTransform ResolveCanvasRect()
    {
        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        return _parentCanvas != null ? _parentCanvas.transform as RectTransform : null;
    }

    private bool IsPointerInsideRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return false;
        }

        Camera eventCamera = ResolveEventCamera();
        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
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

    private string GetItemTypeLabel(ItemType type)
    {
        return _itemTypeLabels.TryGetValue(type, out string label) ? label : type.ToString();
    }

    private string GetRarityLabel(ItemRarity rarity)
    {
        return _rarityLabels.TryGetValue(rarity, out string label) ? label : rarity.ToString();
    }

    private string GetEquipmentKindLabel(EquipmentSlotKind kind)
    {
        return _equipmentKindLabels.TryGetValue(kind, out string label) ? label : kind.ToString();
    }

    private static string FormatWeight(float weight)
    {
        return Mathf.Max(0f, weight).ToString("0.##");
    }

    private static Color GetRarityTextColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return new Color(0.38f, 0.72f, 1f);
            case ItemRarity.Rare:
                return new Color(0.78f, 0.52f, 1f);
            case ItemRarity.Epic:
                return new Color(1f, 0.78f, 0.28f);
            case ItemRarity.Legendary:
                return new Color(1f, 0.38f, 0.32f);
            case ItemRarity.Common:
            default:
                return new Color(0.62f, 0.96f, 0.68f);
        }
    }
}
