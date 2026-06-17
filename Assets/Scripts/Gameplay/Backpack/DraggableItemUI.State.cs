using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class DraggableItemUI
{
    private const float RarityBackgroundAlpha = 0.62f;
    private const float ItemIconInsetRatio = 0.03f;
    private const float ItemIconAxisStretchPerAspect = 0.12f;
    private const float ItemIconMaxAxisStretch = 1.18f;

    /// <summary>
    /// 绑定物品运行时状态
    /// </summary>
    /// <param name="runtimeState">新的运行时状态</param>
    public void BindRuntimeState(InventoryItemRuntimeState runtimeState)
    {
        _runtimeState = runtimeState ?? new InventoryItemRuntimeState();
    }

    /// <summary>
    /// 初始化运行时物品视图
    /// </summary>
    /// <param name="data">静态物品配置</param>
    /// <param name="startPos">初始格子坐标</param>
    /// <param name="isRotated">初始朝向是否旋转</param>
    public void InitializeItem(InventoryItemData data, Vector2Int startPos, bool isRotated)
    {
        EnsureComponents();

        _runtimeState.ItemData = data;
        _originalGridIndex = startPos;
        _originalIsRotated = isRotated;
        _currentPreviewIsRotated = isRotated;

        ApplyItemVisual(data);

        UpdateVisualSize(isRotated);
        if (CurrentGrid != null)
        {
            _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(startPos.x, startPos.y);
        }

        UpdateAmountText();
        UpdateItemNameText();
        UpdateSearchVisualState();
    }

    /// <summary>
    /// 应用持久化的运行时状态，例如搜索进度
    /// </summary>
    /// <param name="saveData">运行时快照数据</param>
    public void ApplyContainerRuntimeState(ContainerItemSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        _runtimeState.ApplyContainerSaveData(saveData);
        _isRevealAnimating = false;
        _revealAnimationTimer = 0f;

        ApplyItemVisual(ItemData);
        UpdateAmountText();
        UpdateItemNameText();
        UpdateSearchVisualState();
    }

    /// <summary>
    /// 物品成功放入新位置后，同步网格和视图状态
    /// </summary>
    /// <param name="index">目标格子坐标</param>
    /// <param name="isRotated">目标朝向是否旋转</param>
    public void PlaceSuccessfully(Vector2Int index, bool isRotated)
    {
        if (CurrentGrid == null)
        {
            return;
        }

        CurrentGrid.GetGridController().PlaceItem(this, index.x, index.y, isRotated);
        _originalGridIndex = index;
        _originalIsRotated = isRotated;
        _currentPreviewIsRotated = isRotated;
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(index.x, index.y);
        UpdateVisualSize(isRotated);
        UpdateItemNameText();
    }

    /// <summary>
    /// 将物品恢复到拖拽前的位置或槽位
    /// </summary>
    public void BounceBack()
    {
        if (TryRestoreToGrid())
        {
            return;
        }

        if (TryRestoreToEquipmentSlot())
        {
            return;
        }

        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, false);
            _rectTransform.anchoredPosition = Vector2.zero;
            _currentPreviewIsRotated = _originalIsRotated;
            UpdateVisualSize(_originalIsRotated);
        }
    }

    /// <summary>
    /// 刷新堆叠数量文本
    /// </summary>
    public void UpdateAmountText()
    {
        if (AmountText == null)
        {
            return;
        }

        bool shouldShow = ItemData != null && ItemData.IsStackable && CurrentAmount > 0 && CanInteractWithItem();
        AmountText.gameObject.SetActive(shouldShow);
        if (shouldShow)
        {
            AmountText.text = CurrentAmount.ToString();
        }

        ConfigureItemNameTextLayout();
    }

    /// <summary>
    /// Refreshes the compact item name label shown in the lower-right corner.
    /// </summary>
    public void UpdateItemNameText()
    {
        EnsureItemNameText();
        if (ItemNameText == null)
        {
            return;
        }

        string itemName = ResolveDisplayItemName(ItemData);
        bool shouldShow = CurrentGrid != null && !string.IsNullOrEmpty(itemName) && CanInteractWithItem();
        ItemNameText.gameObject.SetActive(shouldShow);
        if (shouldShow)
        {
            ItemNameText.text = itemName;
        }

        ConfigureItemNameTextLayout();
    }

    /// <summary>
    /// 强制结束拖拽态，不改变物品最终位置
    /// </summary>
    public void ForceEndDrag()
    {
        RestoreDragVisualState();
    }

    /// <summary>
    /// 处理点击交互，支持快捷转移和拆分堆叠
    /// </summary>
    /// <param name="eventData">当前指针事件</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanInteractWithItem())
        {
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            ExecuteQuickTransfer();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteractWithItem() || CurrentlyDraggedItem != null || eventData.dragging)
        {
            return;
        }

        InventoryItemInfoPanelController.Instance?.Show(this, eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryItemInfoPanelController.Instance?.HideIfTarget(this);
    }

    /// <summary>
    /// 执行堆叠拆分
    /// </summary>
    /// <param name="splitAmount">需要拆出的数量</param>
    public void ExecuteSplit(int splitAmount)
    {
        if (CurrentGrid == null || ItemData == null)
        {
            return;
        }

        if (!CurrentGrid.GetGridController().FindSpaceAround(_originalGridIndex.x, _originalGridIndex.y, ItemData.Width, ItemData.Height, out Vector2Int position, out bool needsRotation))
        {
            return;
        }

        CurrentAmount -= splitAmount;
        UpdateAmountText();

        // 复制一个新物品视图承接拆分出的那一部分数量
        GameObject clone = Instantiate(gameObject, CurrentGrid.ItemContainer, false);
        DraggableItemUI cloneItem = clone.GetComponent<DraggableItemUI>();
        cloneItem.CurrentGrid = CurrentGrid;
        cloneItem.IsDebugItem = false;
        cloneItem.name = name + "_Split";

        CanvasGroup cloneCanvasGroup = cloneItem.GetComponent<CanvasGroup>();
        if (cloneCanvasGroup != null)
        {
            cloneCanvasGroup.alpha = 1f;
            cloneCanvasGroup.blocksRaycasts = true;
        }

        InventoryItemRuntimeState splitState = _runtimeState.DeepCopy();
        splitState.Amount = splitAmount;
        cloneItem.BindRuntimeState(splitState);
        cloneItem.InitializeItem(splitState.ItemData, position, needsRotation);
        CurrentGrid.GetGridController().PlaceItem(cloneItem, position.x, position.y, needsRotation);
    }

    /// <summary>
    /// 导出当前物品的持久化快照
    /// </summary>
    public ContainerItemSaveData CreateSaveDataSnapshot()
    {
        return _runtimeState.CreateSaveDataSnapshot(_originalGridIndex, _originalIsRotated);
    }

    /// <summary>
    /// 控制是否由组件自身自动推进搜索进度
    /// </summary>
    /// <param name="isEnabled">是否启用自动推进</param>
    public void SetSearchAutoTickEnabled(bool isEnabled)
    {
        AutoTickSearchProgress = isEnabled;
    }

    /// <summary>
    /// 手动推进搜索进度，供外部统一驱动搜索流程
    /// </summary>
    /// <param name="deltaSeconds">本次推进的时间增量</param>
    /// <returns>搜索是否在本次推进后完成</returns>
    public bool AdvanceSearchProgressManually(float deltaSeconds)
    {
        if (!RequiresSearch || IsSearched)
        {
            return false;
        }

        float previousProgress = SearchProgressSeconds;
        bool completed = _runtimeState.AdvanceSearchProgress(deltaSeconds);
        if (completed)
        {
            _isRevealAnimating = true;
            _revealAnimationTimer = 0f;
            UpdateAmountText();
            UpdateItemNameText();
        }

        if (!Mathf.Approximately(previousProgress, SearchProgressSeconds) || completed)
        {
            UpdateSearchVisualState();
        }

        return IsSearched;
    }

    /// <summary>
    /// 判断当前容器物品的内部是否完全为空
    /// </summary>
    public bool IsContainerCompletelyEmpty()
    {
        return _runtimeState.IsContainerCompletelyEmpty();
    }

    /// <summary>
    /// 判断当前物品是否允许放入目标网格
    /// </summary>
    /// <param name="targetGrid">目标网格视图</param>
    /// <returns>是否允许放入</returns>
    public bool CanBePlacedInGrid(InventoryUIController targetGrid)
    {
        if (targetGrid == null || ItemData == null)
        {
            return false;
        }

        IInventoryGridPlacementPolicy[] policies = targetGrid.GetComponents<IInventoryGridPlacementPolicy>();
        for (int i = 0; i < policies.Length; i++)
        {
            if (policies[i] != null && !policies[i].CanAcceptItem(this))
            {
                return false;
            }
        }

        if (IsBackpackGrid(targetGrid) && IsContainerItem(ItemData.Type) && !IsContainerCompletelyEmpty())
        {
            return false;
        }

        return true;
    }

    // 缓存常用组件并统一校正 RectTransform 的布局基准
    private void EnsureComponents()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_itemBackgroundImage == null)
        {
            _itemBackgroundImage = GetComponent<Image>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        _rectTransform.anchorMin = new Vector2(0f, 1f);
        _rectTransform.anchorMax = new Vector2(0f, 1f);
        _rectTransform.pivot = new Vector2(0f, 1f);

        EnsureItemVisualLayers();
        EnsureSearchOverlay();
    }

    // 物品根节点负责接收射线和显示稀有度底色，图标单独放在子 Image 上避免被底色染色
    private void EnsureItemVisualLayers()
    {
        if (_defaultItemBackgroundSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            _defaultItemBackgroundSprite = Sprite.Create(
                whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        if (_itemBackgroundImage != null)
        {
            _itemBackgroundImage.sprite = _defaultItemBackgroundSprite;
            _itemBackgroundImage.type = Image.Type.Simple;
            _itemBackgroundImage.raycastTarget = true;
        }

        if (_itemImage == null)
        {
            Transform iconTransform = transform.Find("ItemIcon");
            if (iconTransform == null)
            {
                GameObject iconObject = new GameObject(
                    "ItemIcon",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                iconTransform = iconObject.transform;
                iconTransform.SetParent(transform, false);
            }

            _itemImage = iconTransform.GetComponent<Image>();
            if (_itemImage == null)
                _itemImage = iconTransform.gameObject.AddComponent<Image>();
        }

        RectTransform iconRect = _itemImage.rectTransform;
        iconRect.anchorMin = new Vector2(ItemIconInsetRatio, ItemIconInsetRatio);
        iconRect.anchorMax = new Vector2(1f - ItemIconInsetRatio, 1f - ItemIconInsetRatio);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        _itemImage.preserveAspect = true;
        _itemImage.raycastTarget = false;

        EnsureItemNameText();
        if (ItemNameText != null)
        {
            ItemNameText.transform.SetAsLastSibling();
        }

        if (AmountText != null)
        {
            AmountText.raycastTarget = false;
            AmountText.transform.SetAsLastSibling();
        }
    }

    private void EnsureItemNameText()
    {
        if (ItemNameText == null)
        {
            Transform labelTransform = transform.Find(ItemNameTextObjectName);
            if (labelTransform == null)
            {
                GameObject labelObject = new GameObject(
                    ItemNameTextObjectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                labelTransform = labelObject.transform;
                labelTransform.SetParent(transform, false);
            }

            ItemNameText = labelTransform.GetComponent<Text>();
            if (ItemNameText == null)
            {
                ItemNameText = labelTransform.gameObject.AddComponent<Text>();
            }
        }

        ItemNameText.font = ResolveItemLabelFont();
        ItemNameText.fontSize = ItemNameTextMaxFontSize;
        ItemNameText.resizeTextForBestFit = true;
        ItemNameText.resizeTextMinSize = ItemNameTextMinFontSize;
        ItemNameText.resizeTextMaxSize = ItemNameTextMaxFontSize;
        ItemNameText.alignment = TextAnchor.LowerRight;
        ItemNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        ItemNameText.verticalOverflow = VerticalWrapMode.Truncate;
        ItemNameText.color = new Color(1f, 1f, 1f, 0.92f);
        ItemNameText.raycastTarget = false;

        Shadow shadow = ItemNameText.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = ItemNameText.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;

        ConfigureItemNameTextLayout();
    }

    private void ConfigureItemNameTextLayout()
    {
        if (ItemNameText == null)
        {
            return;
        }

        RectTransform labelRect = ItemNameText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;

        float rightInset = ItemNameTextHorizontalPadding;
        if (AmountText != null && AmountText.gameObject.activeSelf)
        {
            rightInset += ItemNameTextAmountReserveWidth;
        }

        labelRect.offsetMin = new Vector2(ItemNameTextHorizontalPadding, ItemNameTextBottomPadding);
        labelRect.offsetMax = new Vector2(-rightInset, ItemNameTextBottomPadding + ItemNameTextHeight);
    }

    private Font ResolveItemLabelFont()
    {
        if (AmountText != null && AmountText.font != null)
        {
            return AmountText.font;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static string ResolveDisplayItemName(InventoryItemData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.ItemName))
        {
            return data.ItemName.Trim();
        }

        return !string.IsNullOrWhiteSpace(data.name) ? data.name.Trim() : string.Empty;
    }

    // 按物品稀有度刷新底色和图标；底色保持不透明，图标保持原始颜色
    private void ApplyItemVisual(InventoryItemData data)
    {
        EnsureComponents();

        ApplyItemBackgroundVisual(data);

        if (_itemImage == null)
        {
            UpdateItemNameText();
            return;
        }

        _itemImage.sprite = data != null ? data.ItemIcon : null;
        _itemImage.enabled = _itemImage.sprite != null;
        _itemImage.color = Color.white;
        UpdateItemNameText();
    }

    // 按物品当前显示格子的宽高比轻微拉伸图标，避免长条物品图标缩得过小
    private void UpdateItemIconStretch(int width, int height)
    {
        if (_itemImage == null || width <= 0 || height <= 0)
        {
            return;
        }

        float stretchX = 1f;
        float stretchY = 1f;
        float displayAspect = width / (float)height;

        if (displayAspect > 1f)
        {
            stretchX = Mathf.Min(ItemIconMaxAxisStretch, 1f + (displayAspect - 1f) * ItemIconAxisStretchPerAspect);
        }
        else if (displayAspect < 1f)
        {
            stretchY = Mathf.Min(ItemIconMaxAxisStretch, 1f + (1f / displayAspect - 1f) * ItemIconAxisStretchPerAspect);
        }

        _itemImage.rectTransform.localScale = new Vector3(stretchX, stretchY, 1f);
    }

    private static Color ResolveRarityBackgroundColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return new Color(0.12f, 0.42f, 0.92f, RarityBackgroundAlpha);
            case ItemRarity.Rare:
                return new Color(0.55f, 0.24f, 0.86f, RarityBackgroundAlpha);
            case ItemRarity.Epic:
                return new Color(0.96f, 0.68f, 0.12f, RarityBackgroundAlpha);
            case ItemRarity.Legendary:
                return new Color(0.86f, 0.14f, 0.12f, RarityBackgroundAlpha);
            case ItemRarity.Common:
            default:
                return new Color(0.18f, 0.64f, 0.28f, RarityBackgroundAlpha);
        }
    }

    // 深拷贝容器中的物品快照，避免不同运行时对象共享同一份列表引用
    private void ApplyItemBackgroundVisual(InventoryItemData data)
    {
        if (_itemBackgroundImage == null)
        {
            return;
        }

        Sprite backgroundSprite = data != null ? data.ItemBackgroundSprite : null;
        if (backgroundSprite != null)
        {
            _itemBackgroundImage.sprite = backgroundSprite;
            _itemBackgroundImage.type = Image.Type.Simple;
            _itemBackgroundImage.color = Color.white;
            return;
        }

        _itemBackgroundImage.sprite = _defaultItemBackgroundSprite;
        _itemBackgroundImage.type = Image.Type.Simple;
        _itemBackgroundImage.color = ResolveRarityBackgroundColor(data != null ? data.Rarity : ItemRarity.Common);
    }

    private static List<ContainerItemSaveData> CloneSaveDataList(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> clone = new List<ContainerItemSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerItemSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }

    // 深拷贝容器格子状态，避免 blocked cell 等运行时信息串改
    private static List<ContainerCellStateSaveData> CloneCellStateList(List<ContainerCellStateSaveData> source)
    {
        List<ContainerCellStateSaveData> clone = new List<ContainerCellStateSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerCellStateSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }

    // 统一通过主控制器判断某个网格是否为角色背包网格
    private static bool IsBackpackGrid(InventoryUIController targetGrid)
    {
        return InventoryScreenController.Instance != null && InventoryScreenController.Instance.BackpackGrid == targetGrid;
    }

    // 空容器可以作为普通物品放入背包，非空容器会被拦截以避免嵌套内容转移混乱
    private static bool IsContainerItem(ItemType itemType)
    {
        return itemType == ItemType.Bag || itemType == ItemType.Rig;
    }
}
