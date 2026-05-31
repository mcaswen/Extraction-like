using UnityEngine;

/// <summary>
/// 装备槽控制器
/// 负责装备类物品与其内部网格之间的装配关系
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    [Header("Accepted Item")]
    public ItemType AcceptedType;
    public EquipmentSlotKind AcceptedEquipmentKind = EquipmentSlotKind.None;

    [Header("Linked Container Grid")]
    public InventoryUIController LinkedGrid;

    [Header("Lock")]
    public bool IsDefaultLockedContainerSlot;

    private bool IsEquipmentKindSlot => AcceptedEquipmentKind != EquipmentSlotKind.None;

    public DraggableItemUI EquippedItem { get; private set; }
    public bool HasEquippedItem => EquippedItem != null;

    private bool _isLinkedGridInitialized;

    private void Start()
    {
        InitializeRuntimeState(InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen);
    }

    private void OnEnable()
    {
        InitializeRuntimeState(InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen);
    }


    private bool CanAcceptItem(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (IsEquipmentKindSlot)
        {
            return itemData.Type == ItemType.Equipment &&
                   itemData.EquipmentKind == AcceptedEquipmentKind;
        }

        return itemData.Type == AcceptedType;
    }

    /// <summary>
    /// 尝试把一个物品装备到当前槽位
    /// </summary>
    /// <param name="item">待装备的物品视图</param>
    /// <returns>是否装备成功</returns>
    public bool TryEquip(DraggableItemUI item)
    {
        InitializeRuntimeState(false);

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        if (!CanAcceptItem(item.ItemData) || EquippedItem != null)
        {
            return false;
        }

        EquippedItem = item;
        _isLinkedGridInitialized = false;

        item.CurrentGrid = null;
        item.transform.SetParent(transform, false);

        ApplyEquippedItemVisual(item);

        if (AcceptedEquipmentKind != EquipmentSlotKind.None)
        {
            SetLinkedGridVisible(false);
            return true;
        }

        InitializeLinkedGridFromEquippedItem();
        SetLinkedGridVisible(InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen);

        return true;
    }

    private void ApplyEquippedItemVisual(DraggableItemUI item)
    {
        if (item == null)
        {
            return;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            return;
        }

        itemRect.localScale = Vector3.one;
        itemRect.localRotation = Quaternion.identity;
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);
        itemRect.anchoredPosition = Vector2.zero;

        if (AcceptedEquipmentKind == EquipmentSlotKind.None)
        {
            return;
        }

        RectTransform slotRect = transform as RectTransform;
        Vector2 targetSize = slotRect != null ? slotRect.rect.size : Vector2.zero;

        if (targetSize.x <= 0f || targetSize.y <= 0f)
        {
            targetSize = slotRect != null ? slotRect.sizeDelta : Vector2.zero;
        }

        if (targetSize.x <= 0f || targetSize.y <= 0f)
        {
            targetSize = new Vector2(50f, 50f);
        }

        itemRect.sizeDelta = targetSize;
    }

    private void ApplyEquipmentSlotVisualSize(DraggableItemUI item)
    {
        if (item == null)
        {
            return;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        RectTransform slotRect = transform as RectTransform;
        if (itemRect == null || slotRect == null)
        {
            return;
        }

        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.localScale = Vector3.one;
        itemRect.localRotation = Quaternion.identity;

        itemRect.sizeDelta = slotRect.rect.size;
    }

    /// <summary>
    /// 处理拖拽物品投放到装备槽时的装备或替换逻辑
    /// </summary>
    /// <param name="item">被拖到槽位上的物品</param>
    /// <returns>是否处理成功</returns>
    public bool TryHandleDrop(DraggableItemUI item)
    {
        if (item == null || item.ItemData == null || !CanAcceptItem(item.ItemData))
        {
            return false;
        }

        if (IsDefaultLockedContainerSlot && HasEquippedItem)
        {
            return false;
        }

        if (!HasEquippedItem)
        {
            return TryEquip(item);
        }

        if (EquippedItem == item)
        {
            ApplyEquippedItemVisual(item);
            return true;
        }

        return InventoryScreenController.Instance != null &&
               InventoryScreenController.Instance.TryReplaceEquippedContainerFromDrag(this, item);
    }

    /// <summary>
    /// 将当前装备从槽位中卸下，并把内部网格状态写回物品数据
    /// </summary>
    public void Unequip()
    {
        ReleaseEquippedItem();
    }

    /// <summary>
    /// 初始化槽位运行时状态，并尝试识别场景中已挂载的装备物品
    /// </summary>
    /// <param name="showLinkedGrid">是否显示关联内部网格</param>
    public void InitializeRuntimeState(bool showLinkedGrid)
    {
        EnsureEquippedItemReference();

        if (IsEquipmentKindSlot)
        {
            ApplyEquippedItemVisual(EquippedItem);
            SetLinkedGridVisible(false);
            return;
        }

        InitializeLinkedGridFromEquippedItem();
        SetLinkedGridVisible(showLinkedGrid);
    }

    /// <summary>
    /// 切换关联内部网格的可见性
    /// </summary>
    /// <param name="isVisible">是否显示关联内部网格</param>
    public void SetLinkedGridVisible(bool isVisible)
    {
        if (IsEquipmentKindSlot)
        {
            if (LinkedGrid != null)
            {
                LinkedGrid.gameObject.SetActive(false);
            }

            return;
        }
        if (LinkedGrid == null)
        {
            return;
        }

        if (EquippedItem == null)
        {
            LinkedGrid.gameObject.SetActive(false);
            return;
        }

        LinkedGrid.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 将当前内部网格的运行时数据同步回已装备容器物品
    /// </summary>
    public void SyncEquippedItemRuntimeDataFromGrid()
    {
        if (IsEquipmentKindSlot)
        {
            return;

        }
        if (EquippedItem == null || LinkedGrid == null)
        {
            return;
        }

        EquippedItem.InternalItems = LinkedGrid.ExtractSaveData();
        EquippedItem.InternalCellStates = LinkedGrid.ExtractCellStateData();
    }

    /// <summary>
    /// 释放当前已装备物品，并把内部网格状态写回物品
    /// </summary>
    /// <returns>被释放的物品，没有则返回空</returns>
    public DraggableItemUI ReleaseEquippedItem()
    {
        if (EquippedItem == null)
        {
            return null;
        }

        DraggableItemUI releasedItem = EquippedItem;
        if (!IsEquipmentKindSlot && LinkedGrid != null)
        {
            releasedItem.InternalItems = LinkedGrid.ExtractSaveData();
            releasedItem.InternalCellStates = LinkedGrid.ExtractCellStateData();
            LinkedGrid.ClearUI();
            LinkedGrid.gameObject.SetActive(false);
        }

        _isLinkedGridInitialized = false;
        EquippedItem = null;
        ReparentReleasedItem(releasedItem);
        return releasedItem;
    }

    // 在运行时首次激活时，尝试从子物体中恢复已经挂在槽位上的容器引用
    private void EnsureEquippedItemReference()
    {
        if (EquippedItem != null)
        {
            return;
        }

        foreach (Transform child in transform)
        {
            DraggableItemUI item = child.GetComponent<DraggableItemUI>();
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            if (!CanAcceptItem(item.ItemData))
            {
                continue;
            }

            EquippedItem = item;
            EquippedItem.CurrentGrid = null;
            ApplyEquippedItemVisual(item);
            break;
        }
    }

    // 根据当前装备物品重建关联的内部网格，使装备槽与容器内容保持同步
    private void InitializeLinkedGridFromEquippedItem()
    {
        if (_isLinkedGridInitialized || LinkedGrid == null || EquippedItem == null || EquippedItem.ItemData == null)
        {
            return;
        }

        LinkedGrid.RebuildGridUI(
            EquippedItem.ItemData.ContainerColumns,
            EquippedItem.ItemData.ContainerRows,
            EquippedItem.ItemData.BlockedCells);
        LinkedGrid.LoadFromRuntimeState(EquippedItem.InternalItems, EquippedItem.InternalCellStates);
        _isLinkedGridInitialized = true;
    }

    // 把卸下来的容器临时挂到全局拖拽层，便于后续继续拖拽或做替换处理
    private static void ReparentReleasedItem(DraggableItemUI releasedItem)
    {
        if (releasedItem == null)
        {
            return;
        }

        Transform targetParent = InventoryItemFactory.Instance != null && InventoryItemFactory.Instance.GlobalDragLayer != null
            ? InventoryItemFactory.Instance.GlobalDragLayer
            : null;

        releasedItem.transform.SetParent(targetParent, true);
    }
}
