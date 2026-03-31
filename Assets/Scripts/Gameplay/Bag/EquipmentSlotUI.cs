using UnityEngine;

/// <summary>
/// 装备槽控制器。
/// 负责装备类物品与其内部网格之间的装配关系。
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    public ItemType AcceptedType;
    public InventoryUIController LinkedGrid;

    public DraggableItemUI EquippedItem { get; private set; }
    public bool HasEquippedItem => EquippedItem != null;

    private bool _isLinkedGridInitialized;

    private void Start()
    {
        InitializeRuntimeState(GameUIController.Instance != null && GameUIController.Instance.IsInventoryOpen);
    }

    private void OnEnable()
    {
        InitializeRuntimeState(GameUIController.Instance != null && GameUIController.Instance.IsInventoryOpen);
    }

    /// <summary>
    /// 尝试把一个物品装备到当前槽位。
    /// </summary>
    public bool TryEquip(DraggableItemUI item)
    {
        InitializeRuntimeState(false);

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        if (item.ItemData.Type != AcceptedType || EquippedItem != null)
        {
            return false;
        }

        EquippedItem = item;
        _isLinkedGridInitialized = false;
        item.CurrentGrid = null;
        item.transform.SetParent(transform, false);
        item.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        InitializeLinkedGridFromEquippedItem();
        SetLinkedGridVisible(GameUIController.Instance != null && GameUIController.Instance.IsInventoryOpen);

        return true;
    }

    /// <summary>
    /// 处理拖拽物品投放到装备槽时的装备或替换逻辑。
    /// </summary>
    public bool TryHandleDrop(DraggableItemUI item)
    {
        if (item == null || item.ItemData == null || item.ItemData.Type != AcceptedType)
        {
            return false;
        }

        if (!HasEquippedItem)
        {
            return TryEquip(item);
        }

        if (EquippedItem == item)
        {
            item.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            return true;
        }

        return GameUIController.Instance != null && GameUIController.Instance.TryReplaceEquippedContainerFromDrag(this, item);
    }

    /// <summary>
    /// 将当前装备从槽位中卸下，并把内部网格状态写回物品数据。
    /// </summary>
    public void Unequip()
    {
        ReleaseEquippedItem();
    }

    /// <summary>
    /// 初始化槽位运行时状态，并尝试识别场景中已挂载的装备物品。
    /// </summary>
    public void InitializeRuntimeState(bool showLinkedGrid)
    {
        EnsureEquippedItemReference();
        InitializeLinkedGridFromEquippedItem();
        SetLinkedGridVisible(showLinkedGrid);
    }

    /// <summary>
    /// 切换关联内部网格的可见性。
    /// </summary>
    public void SetLinkedGridVisible(bool isVisible)
    {
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
    /// 将当前内部网格的运行时数据同步回已装备容器物品。
    /// </summary>
    public void SyncEquippedItemRuntimeDataFromGrid()
    {
        if (EquippedItem == null || LinkedGrid == null)
        {
            return;
        }

        EquippedItem.InternalItems = LinkedGrid.ExtractSaveData();
        EquippedItem.InternalCellStates = LinkedGrid.ExtractCellStateData();
    }

    /// <summary>
    /// 释放当前已装备物品，并把内部网格状态写回物品。
    /// </summary>
    public DraggableItemUI ReleaseEquippedItem()
    {
        if (EquippedItem == null)
        {
            return null;
        }

        DraggableItemUI releasedItem = EquippedItem;
        if (LinkedGrid != null)
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

            if (item.ItemData.Type != AcceptedType)
            {
                continue;
            }

            EquippedItem = item;
            EquippedItem.CurrentGrid = null;
            RectTransform itemRect = item.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                itemRect.anchoredPosition = Vector2.zero;
            }
            break;
        }
    }

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
