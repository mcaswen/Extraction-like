using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包模块总控制器。
/// 负责背包开关、容器打开、快捷转移和拾取路由。
/// </summary>
public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    public EquipmentSlotUI RigSlot;
    public EquipmentSlotUI BackpackSlot;
    public bool IsInventoryOpen { get; private set; }

    [Header("Panels")]
    public GameObject InventoryPanel;
    public InventoryUIController PocketGrid;
    public InventoryUIController TacticalRigGrid;
    public InventoryUIController BackpackGrid;
    public InventoryUIController LootChestGrid;

    public LootBoxEntity CurrentLootBox { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(false);
        }

        if (LootChestGrid != null)
        {
            LootChestGrid.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        RefreshCharacterContainerState(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }

    /// <summary>
    /// 打开一个场景容器。
    /// </summary>
    public void OpenLootBox(LootBoxEntity lootBox)
    {
        if (lootBox == null || LootChestGrid == null)
        {
            return;
        }

        CurrentLootBox = lootBox;
        CurrentLootBox.PrecalculateLootIfNeeded();
        LootChestGrid.gameObject.SetActive(true);
        LootChestGrid.RebuildGridUI(
            lootBox.ContainerColumns,
            lootBox.ContainerRows,
            lootBox.GetBlockedCells());
        LootChestGrid.LoadFromRuntimeState(
            CloneSaveDataList(lootBox.GetSavedItems()),
            CloneCellStateList(lootBox.GetSavedCellStates()));
        LootChestGrid.transform.SetAsLastSibling();

        if (!IsInventoryOpen)
        {
            ToggleInventory();
        }
        else
        {
            RefreshCharacterContainerState(true);
        }
    }

    /// <summary>
    /// 切换背包面板显示状态。
    /// </summary>
    public void ToggleInventory()
    {
        IsInventoryOpen = !IsInventoryOpen;

        if (IsInventoryOpen)
        {
            OpenInventoryInternal();
        }
        else
        {
            CloseInventoryInternal();
        }
    }

    /// <summary>
    /// 根据源网格和物品信息查找快速转移目标。
    /// </summary>
    public bool TryFindQuickTransferTarget(
        InventoryUIController sourceGrid,
        DraggableItemUI itemView,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (itemView == null || itemView.ItemData == null)
        {
            return false;
        }

        RefreshCharacterContainerState(IsInventoryOpen);

        if (sourceGrid == LootChestGrid)
        {
            if (TryResolveAvailableSpace(BackpackSlot, BackpackGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (TryResolveAvailableSpace(RigSlot, TacticalRigGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (TryResolveAvailableSpace(PocketGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            return false;
        }

        if (sourceGrid == PocketGrid || sourceGrid == TacticalRigGrid || sourceGrid == BackpackGrid)
        {
            if (CurrentLootBox != null)
            {
                return TryResolveAvailableSpace(LootChestGrid, itemView, out targetGrid, out position, out needsRotation);
            }

            if (sourceGrid != BackpackGrid && TryResolveAvailableSpace(BackpackSlot, BackpackGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (sourceGrid != TacticalRigGrid && TryResolveAvailableSpace(RigSlot, TacticalRigGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (sourceGrid != PocketGrid && TryResolveAvailableSpace(PocketGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试将一个物品直接拾取到角色身上的任意可用网格。
    /// </summary>
    public bool TryPickupItem(InventoryItemData itemData, int amount)
    {
        if (itemData == null)
        {
            return false;
        }

        RefreshCharacterContainerState(IsInventoryOpen);

        if (TryResolveAvailableSpace(BackpackSlot, BackpackGrid, itemData, out InventoryUIController backpackTarget, out Vector2Int backpackPosition, out bool backpackRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(itemData, backpackTarget, backpackPosition.x, backpackPosition.y, amount, backpackRotation);
            return true;
        }

        if (TryResolveAvailableSpace(RigSlot, TacticalRigGrid, itemData, out InventoryUIController rigTarget, out Vector2Int rigPosition, out bool rigRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(itemData, rigTarget, rigPosition.x, rigPosition.y, amount, rigRotation);
            return true;
        }

        if (TryResolveAvailableSpace(PocketGrid, itemData, out InventoryUIController pocketTarget, out Vector2Int pocketPosition, out bool pocketRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(itemData, pocketTarget, pocketPosition.x, pocketPosition.y, amount, pocketRotation);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 处理世界物品拾取与自动装备。
    /// </summary>
    public bool TryStoreWorldItem(WorldLootItem worldItem)
    {
        if (worldItem == null || worldItem.ItemData == null)
        {
            return false;
        }

        RefreshCharacterContainerState(IsInventoryOpen);

        if (TryResolveAvailableSpace(BackpackSlot, BackpackGrid, worldItem, out InventoryUIController backpackTarget, out Vector2Int backpackPosition, out bool backpackRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(
                worldItem.ItemData,
                backpackTarget,
                backpackPosition.x,
                backpackPosition.y,
                worldItem.CurrentAmount,
                backpackRotation,
                CloneSaveDataList(worldItem.InternalItems),
                CloneCellStateList(worldItem.InternalCellStates));
            return true;
        }

        if (TryResolveAvailableSpace(RigSlot, TacticalRigGrid, worldItem, out InventoryUIController rigTarget, out Vector2Int rigPosition, out bool rigRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(
                worldItem.ItemData,
                rigTarget,
                rigPosition.x,
                rigPosition.y,
                worldItem.CurrentAmount,
                rigRotation,
                CloneSaveDataList(worldItem.InternalItems),
                CloneCellStateList(worldItem.InternalCellStates));
            return true;
        }

        if (TryResolveAvailableSpace(PocketGrid, worldItem, out InventoryUIController pocketTarget, out Vector2Int pocketPosition, out bool pocketRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(
                worldItem.ItemData,
                pocketTarget,
                pocketPosition.x,
                pocketPosition.y,
                worldItem.CurrentAmount,
                pocketRotation,
                CloneSaveDataList(worldItem.InternalItems),
                CloneCellStateList(worldItem.InternalCellStates));
            return true;
        }

        return false;
    }

    /// <summary>
    /// 使用装备交互键尝试装备或替换世界中的容器。
    /// </summary>
    public bool TryEquipWorldContainer(WorldLootItem worldItem)
    {
        if (worldItem == null || worldItem.ItemData == null)
        {
            return false;
        }

        RefreshCharacterContainerState(IsInventoryOpen);

        if (worldItem.ItemData.Type == ItemType.Bag)
        {
            return TryHandleContainerPickup(worldItem, BackpackSlot);
        }

        if (worldItem.ItemData.Type == ItemType.Rig)
        {
            return TryHandleContainerPickup(worldItem, RigSlot);
        }

        return false;
    }

    /// <summary>
    /// 处理拖拽物品投放到已占用装备槽时的替换逻辑。
    /// </summary>
    public bool TryReplaceEquippedContainerFromDrag(EquipmentSlotUI slot, DraggableItemUI incomingItem)
    {
        if (slot == null || incomingItem == null || incomingItem.ItemData == null)
        {
            return false;
        }

        if (incomingItem.ItemData.Type != slot.AcceptedType)
        {
            return false;
        }

        slot.InitializeRuntimeState(false);
        slot.SyncEquippedItemRuntimeDataFromGrid();

        DraggableItemUI oldItem = slot.ReleaseEquippedItem();
        if (oldItem == null)
        {
            return slot.TryEquip(incomingItem);
        }

        if (!slot.TryEquip(incomingItem))
        {
            slot.TryEquip(oldItem);
            return false;
        }

        DropItemViewToWorld(
            oldItem,
            CloneSaveDataList(oldItem.InternalItems),
            CloneCellStateList(oldItem.InternalCellStates));
        return true;
    }

    private void OpenInventoryInternal()
    {
        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(true);
        }

        RefreshCharacterContainerState(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseInventoryInternal()
    {
        if (DraggableItemUI.CurrentlyDraggedItem != null)
        {
            DraggableItemUI.CurrentlyDraggedItem.BounceBack();
            DraggableItemUI.CurrentlyDraggedItem.ForceEndDrag();
        }

        RefreshCharacterContainerState(false);
        CloseLootBoxIfNeeded();

        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void CloseLootBoxIfNeeded()
    {
        if (CurrentLootBox == null || LootChestGrid == null)
        {
            return;
        }

        CurrentLootBox.SaveRuntimeState(
            LootChestGrid.ExtractSaveData(),
            LootChestGrid.ExtractCellStateData());
        LootChestGrid.ClearUI();
        LootChestGrid.gameObject.SetActive(false);
        CurrentLootBox = null;
    }

    private static bool TryResolveAvailableSpace(
        EquipmentSlotUI slot,
        InventoryUIController grid,
        DraggableItemUI itemView,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (slot == null || !slot.HasEquippedItem)
        {
            return false;
        }

        return TryResolveAvailableSpace(grid, itemView, out targetGrid, out position, out needsRotation);
    }

    private static bool TryResolveAvailableSpace(
        InventoryUIController grid,
        DraggableItemUI itemView,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (grid == null || itemView == null || itemView.ItemData == null || !itemView.CanBePlacedInGrid(grid))
        {
            return false;
        }

        if (!grid.GetGridController().FindFirstAvailableSpace(itemView.ItemData.Width, itemView.ItemData.Height, out position, out needsRotation))
        {
            return false;
        }

        targetGrid = grid;
        return true;
    }

    private static bool TryResolveAvailableSpace(
        EquipmentSlotUI slot,
        InventoryUIController grid,
        InventoryItemData item,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (slot == null || !slot.HasEquippedItem)
        {
            return false;
        }

        return TryResolveAvailableSpace(grid, item, out targetGrid, out position, out needsRotation);
    }

    private static bool TryResolveAvailableSpace(
        InventoryUIController grid,
        InventoryItemData item,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (grid == null || item == null || !CanStaticItemEnterGrid(item, grid))
        {
            return false;
        }

        if (!grid.GetGridController().FindFirstAvailableSpace(item.Width, item.Height, out position, out needsRotation))
        {
            return false;
        }

        targetGrid = grid;
        return true;
    }

    private static bool TryResolveAvailableSpace(
        EquipmentSlotUI slot,
        InventoryUIController grid,
        WorldLootItem worldItem,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (slot == null || !slot.HasEquippedItem)
        {
            return false;
        }

        return TryResolveAvailableSpace(grid, worldItem, out targetGrid, out position, out needsRotation);
    }

    private static bool TryResolveAvailableSpace(
        InventoryUIController grid,
        WorldLootItem worldItem,
        out InventoryUIController targetGrid,
        out Vector2Int position,
        out bool needsRotation)
    {
        targetGrid = null;
        position = Vector2Int.zero;
        needsRotation = false;

        if (grid == null || worldItem == null || worldItem.ItemData == null || !CanWorldItemEnterGrid(worldItem, grid))
        {
            return false;
        }

        if (!grid.GetGridController().FindFirstAvailableSpace(worldItem.ItemData.Width, worldItem.ItemData.Height, out position, out needsRotation))
        {
            return false;
        }

        targetGrid = grid;
        return true;
    }

    private bool TryHandleContainerPickup(WorldLootItem worldItem, EquipmentSlotUI slot)
    {
        if (slot == null)
        {
            return false;
        }

        if (!slot.HasEquippedItem)
        {
            return TryEquipWorldContainer(
                slot,
                worldItem,
                CloneSaveDataList(worldItem.InternalItems),
                CloneCellStateList(worldItem.InternalCellStates));
        }

        if (worldItem.ItemData.Type == ItemType.Rig)
        {
            return TrySwapRig(worldItem, slot);
        }

        if (worldItem.ItemData.Type == ItemType.Bag)
        {
            return TrySwapBackpack(worldItem, slot);
        }

        return false;
    }

    private bool TrySwapRig(WorldLootItem worldItem, EquipmentSlotUI slot)
    {
        slot.InitializeRuntimeState(false);
        slot.SyncEquippedItemRuntimeDataFromGrid();

        DraggableItemUI oldItem = slot.EquippedItem;
        if (oldItem == null)
        {
            return false;
        }

        DraggableItemUI newItem = CreateWorldContainerView(
            worldItem,
            CloneSaveDataList(worldItem.InternalItems),
            CloneCellStateList(worldItem.InternalCellStates));
        if (newItem == null)
        {
            return false;
        }

        DraggableItemUI releasedItem = slot.ReleaseEquippedItem();
        if (releasedItem != oldItem)
        {
            oldItem = releasedItem;
        }
        if (!slot.TryEquip(newItem))
        {
            Destroy(newItem.gameObject);
            slot.TryEquip(oldItem);
            return false;
        }

        DropItemViewToWorld(
            oldItem,
            CloneSaveDataList(oldItem.InternalItems),
            CloneCellStateList(oldItem.InternalCellStates));
        return true;
    }

    private bool TrySwapBackpack(WorldLootItem worldItem, EquipmentSlotUI slot)
    {
        slot.InitializeRuntimeState(false);
        slot.SyncEquippedItemRuntimeDataFromGrid();

        DraggableItemUI oldItem = slot.EquippedItem;
        if (oldItem == null)
        {
            return false;
        }

        bool shouldTransferOldContents = TryBuildReplacementBagLayout(
            oldItem,
            worldItem,
            out List<ContainerItemSaveData> transferredLayout);

        List<ContainerItemSaveData> newBagItems = shouldTransferOldContents
            ? transferredLayout
            : CloneSaveDataList(worldItem.InternalItems);
        List<ContainerCellStateSaveData> newBagCellStates = shouldTransferOldContents
            ? new List<ContainerCellStateSaveData>()
            : CloneCellStateList(worldItem.InternalCellStates);

        DraggableItemUI newItem = CreateWorldContainerView(worldItem, newBagItems, newBagCellStates);
        if (newItem == null)
        {
            return false;
        }

        DraggableItemUI releasedItem = slot.ReleaseEquippedItem();
        if (releasedItem != oldItem)
        {
            oldItem = releasedItem;
        }
        if (!slot.TryEquip(newItem))
        {
            Destroy(newItem.gameObject);
            slot.TryEquip(oldItem);
            return false;
        }

        DropItemViewToWorld(
            oldItem,
            shouldTransferOldContents ? new List<ContainerItemSaveData>() : CloneSaveDataList(oldItem.InternalItems),
            shouldTransferOldContents ? new List<ContainerCellStateSaveData>() : CloneCellStateList(oldItem.InternalCellStates));
        return true;
    }

    private static bool TryBuildReplacementBagLayout(
        DraggableItemUI oldBagItem,
        WorldLootItem newBagWorldItem,
        out List<ContainerItemSaveData> sortedLayout)
    {
        sortedLayout = new List<ContainerItemSaveData>();
        if (oldBagItem == null || oldBagItem.ItemData == null || newBagWorldItem == null || newBagWorldItem.ItemData == null)
        {
            return false;
        }

        int oldCapacity = GetContainerCapacity(oldBagItem.ItemData);
        int newCapacity = GetContainerCapacity(newBagWorldItem.ItemData);
        if (newCapacity <= oldCapacity)
        {
            return false;
        }

        List<ContainerItemSaveData> combinedItems = CloneSaveDataList(newBagWorldItem.InternalItems);
        combinedItems.AddRange(CloneSaveDataList(oldBagItem.InternalItems));

        return InventoryAutoSortService.TryBuildSortedLayout(
            newBagWorldItem.ItemData.ContainerColumns,
            newBagWorldItem.ItemData.ContainerRows,
            newBagWorldItem.ItemData.BlockedCells,
            combinedItems,
            out sortedLayout);
    }

    private bool TryEquipWorldContainer(
        EquipmentSlotUI slot,
        WorldLootItem worldItem,
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        DraggableItemUI itemView = CreateWorldContainerView(worldItem, internalItems, internalCellStates);
        if (itemView == null)
        {
            return false;
        }

        if (slot.TryEquip(itemView))
        {
            return true;
        }

        Destroy(itemView.gameObject);
        return false;
    }

    private static DraggableItemUI CreateWorldContainerView(
        WorldLootItem worldItem,
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        if (InventoryItemFactory.Instance == null || worldItem == null || worldItem.ItemData == null)
        {
            return null;
        }

        return InventoryItemFactory.Instance.CreateFloatingItem(
            worldItem.ItemData,
            worldItem.CurrentAmount,
            internalItems,
            internalCellStates);
    }

    private static void DropItemViewToWorld(
        DraggableItemUI itemView,
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        if (itemView == null || itemView.ItemData == null)
        {
            return;
        }

        if (itemView.ItemData.WorldPrefab != null)
        {
            Vector3 spawnPosition = GetWorldDropPosition();
            GameObject droppedObject = Instantiate(itemView.ItemData.WorldPrefab, spawnPosition, Quaternion.identity);
            droppedObject.GetComponent<WorldLootItem>()?.InitializeDrop(
                itemView.ItemData,
                itemView.CurrentAmount,
                CloneSaveDataList(internalItems),
                CloneCellStateList(internalCellStates));
        }

        Destroy(itemView.gameObject);
    }

    private static Vector3 GetWorldDropPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 1.5f + Vector3.up;
        }

        return Vector3.zero;
    }

    private static int GetContainerCapacity(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        int blockedCount = itemData.BlockedCells != null ? itemData.BlockedCells.Count : 0;
        return Mathf.Max(0, itemData.ContainerColumns * itemData.ContainerRows - blockedCount);
    }

    private static bool CanWorldItemEnterGrid(WorldLootItem worldItem, InventoryUIController targetGrid)
    {
        if (worldItem == null || worldItem.ItemData == null || targetGrid == null)
        {
            return false;
        }

        if (worldItem.ItemData.Type == ItemType.Bag && Instance != null && Instance.BackpackGrid == targetGrid)
        {
            return false;
        }

        if (worldItem.ItemData.Type == ItemType.Rig &&
            Instance != null &&
            Instance.BackpackGrid == targetGrid &&
            !IsContainerSnapshotEmpty(worldItem.InternalItems, worldItem.InternalCellStates))
        {
            return false;
        }

        return true;
    }

    private static bool CanStaticItemEnterGrid(InventoryItemData itemData, InventoryUIController targetGrid)
    {
        if (itemData == null || targetGrid == null)
        {
            return false;
        }

        if (itemData.Type == ItemType.Bag && Instance != null && Instance.BackpackGrid == targetGrid)
        {
            return false;
        }

        return true;
    }

    private static bool IsContainerSnapshotEmpty(
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        bool hasItems = internalItems != null && internalItems.Count > 0;
        bool hasCellStates = internalCellStates != null && internalCellStates.Count > 0;
        return !hasItems && !hasCellStates;
    }

    private void RefreshCharacterContainerState(bool showLinkedGrids)
    {
        BackpackSlot?.InitializeRuntimeState(showLinkedGrids);
        RigSlot?.InitializeRuntimeState(showLinkedGrids);
    }

    /// <summary>
    /// 根据屏幕坐标命中装备槽。
    /// 用显式矩形检测替代纯 UI 射线，避免拖拽物挡住槽位。
    /// </summary>
    public EquipmentSlotUI GetEquipmentSlotAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (IsScreenPointInsideSlot(BackpackSlot, screenPosition, eventCamera))
        {
            return BackpackSlot;
        }

        if (IsScreenPointInsideSlot(RigSlot, screenPosition, eventCamera))
        {
            return RigSlot;
        }

        return null;
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

    private static bool IsScreenPointInsideSlot(EquipmentSlotUI slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null)
        {
            return false;
        }

        RectTransform slotRect = slot.transform as RectTransform;
        return slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera);
    }

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
}
