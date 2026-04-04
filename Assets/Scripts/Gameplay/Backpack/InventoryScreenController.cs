using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 背包模块总控制器
/// 负责背包开关、容器打开、快捷转移和拾取路由
/// </summary>
public class InventoryScreenController : MonoBehaviour
{
    public static InventoryScreenController Instance { get; private set; }

    public EquipmentSlotUI RigSlot;
    public EquipmentSlotUI BackpackSlot;
    public bool IsInventoryOpen { get; private set; }

    [Header("Panels")]
    public GameObject InventoryPanel;
    public InventoryUIController PocketGrid;
    public InventoryUIController TacticalRigGrid;
    public InventoryUIController BackpackGrid;
    public InventoryUIController LootChestGrid;

    private InventoryScreenSessionContext _activeSessionContext;
    private bool _customPlayerInventoryUiApplied;
    private bool _rigSlotWasActive;
    private bool _backpackSlotWasActive;
    private bool _tacticalRigGridWasActive;
    private bool _backpackGridWasActive;

    public InventoryScreenSessionContext ActiveSessionContext => _activeSessionContext;
    public bool HasActiveExternalContainer => _activeSessionContext != null;
    public bool UsesCustomPlayerInventory => _activeSessionContext != null && _activeSessionContext.UseCustomPlayerInventory;
    public InventoryUIController ActiveExternalGrid => HasActiveExternalContainer ? LootChestGrid : null;
    public InventoryUIController ActivePlayerGrid => UsesCustomPlayerInventory ? PocketGrid : null;

    private void Awake()
    {
        Instance = this;
        InitializeRuntimeScreen();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
    /// 供运行时补建界面在完成引用装配后调用，统一同步初始显隐状态
    /// </summary>
    public void InitializeRuntimeScreen()
    {
        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(false);
        }

        if (LootChestGrid != null)
        {
            LootChestGrid.gameObject.SetActive(false);
        }

        RestoreStandardPlayerInventoryUiState();
        RefreshCharacterContainerState(false);
    }

    /// <summary>
    /// 打开一个场景容器
    /// </summary>
    /// <param name="lootBox">要打开的场景容器实体</param>
    public void OpenLootBox(LootBoxEntity lootBox)
    {
        if (lootBox == null || LootChestGrid == null)
        {
            return;
        }

        InventoryScreenSessionContext sessionContext = lootBox.CreateInventorySessionContext();
        OpenInventorySession(sessionContext);
    }

    /// <summary>
    /// 打开一轮背包会话，可选择复用角色原生背包或临时接管为自定义玩家格子
    /// </summary>
    public void OpenInventorySession(InventoryScreenSessionContext sessionContext)
    {
        if (sessionContext == null || LootChestGrid == null)
        {
            return;
        }

        bool wasInventoryOpen = IsInventoryOpen;

        if (_activeSessionContext != null)
        {
            CloseActiveSessionIfNeeded();
        }

        sessionContext.BeforeOpen?.Invoke();
        _activeSessionContext = sessionContext;
        PrepareSessionForDisplay(sessionContext);

        if (!wasInventoryOpen)
        {
            OpenInventory();
        }
        else
        {
            RefreshVisibleStateForCurrentContext();
        }
    }

    /// <summary>
    /// 切换背包面板显示状态
    /// </summary>
    public void ToggleInventory()
    {
        if (IsInventoryOpen)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    /// <summary>
    /// 显式打开背包界面
    /// </summary>
    public void OpenInventory()
    {
        if (IsInventoryOpen)
        {
            return;
        }

        IsInventoryOpen = true;
        OpenInventoryInternal();
    }

    /// <summary>
    /// 显式关闭背包界面
    /// </summary>
    public void CloseInventory()
    {
        if (!IsInventoryOpen)
        {
            return;
        }

        IsInventoryOpen = false;
        CloseInventoryInternal();
    }

    /// <summary>
    /// 判断当前会话是否仍然是指定引用
    /// </summary>
    public bool IsSessionContextActive(InventoryScreenSessionContext sessionContext)
    {
        return sessionContext != null && ReferenceEquals(_activeSessionContext, sessionContext);
    }

    /// <summary>
    /// 根据源网格和物品信息查找快速转移目标
    /// </summary>
    /// <param name="sourceGrid">当前物品所在网格</param>
    /// <param name="itemView">需要转移的物品视图</param>
    /// <param name="targetGrid">解析出的目标网格</param>
    /// <param name="position">解析出的目标坐标</param>
    /// <param name="needsRotation">目标位置是否需要旋转</param>
    /// <returns>是否找到合法目标</returns>
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

        if (!UsesCustomPlayerInventory)
        {
            RefreshCharacterContainerState(IsInventoryOpen);
        }

        if (sourceGrid == LootChestGrid)
        {
            if (UsesCustomPlayerInventory)
            {
                return TryResolveAvailableSpace(PocketGrid, itemView, out targetGrid, out position, out needsRotation);
            }

            if (TryResolveAvailableSpace(BackpackSlot, BackpackGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (TryResolveAvailableSpace(RigSlot, TacticalRigGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (TryResolveAvailableSpace(PocketGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            return false;
        }

        if (sourceGrid == PocketGrid || sourceGrid == TacticalRigGrid || sourceGrid == BackpackGrid)
        {
            if (HasActiveExternalContainer)
            {
                return TryResolveAvailableSpace(LootChestGrid, itemView, out targetGrid, out position, out needsRotation);
            }

            if (UsesCustomPlayerInventory)
            {
                return false;
            }

            if (sourceGrid != BackpackGrid && TryResolveAvailableSpace(BackpackSlot, BackpackGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (sourceGrid != TacticalRigGrid && TryResolveAvailableSpace(RigSlot, TacticalRigGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
            if (sourceGrid != PocketGrid && TryResolveAvailableSpace(PocketGrid, itemView, out targetGrid, out position, out needsRotation)) return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试将一个物品直接拾取到角色身上的任意可用网格
    /// </summary>
    /// <param name="itemData">要拾取的静态物品配置</param>
    /// <param name="amount">拾取数量</param>
    /// <returns>是否成功放入角色容器</returns>
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
    /// 处理世界物品拾取与自动装备
    /// </summary>
    /// <param name="worldItem">场景中的掉落物对象</param>
    /// <returns>是否成功收入角色容器</returns>
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
    /// 使用装备交互键尝试装备或替换世界中的容器
    /// </summary>
    /// <param name="worldItem">场景中的容器掉落物</param>
    /// <returns>是否成功装备</returns>
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
    /// 处理拖拽物品投放到已占用装备槽时的替换逻辑
    /// </summary>
    /// <param name="slot">目标装备槽</param>
    /// <param name="incomingItem">即将放入槽位的物品</param>
    /// <returns>是否替换成功</returns>
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

    // 打开背包面板时，同步角色容器状态并释放鼠标
    private void OpenInventoryInternal()
    {
        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(true);
        }

        RefreshVisibleStateForCurrentContext();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 关闭背包面板时，回收拖拽态并保存当前容器运行时数据
    private void CloseInventoryInternal()
    {
        if (DraggableItemUI.CurrentlyDraggedItem != null)
        {
            DraggableItemUI.CurrentlyDraggedItem.BounceBack();
            DraggableItemUI.CurrentlyDraggedItem.ForceEndDrag();
        }

        CloseActiveSessionIfNeeded();
        RefreshCharacterContainerState(false);

        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 关闭当前会话，并把左右容器的运行时结果统一交给会话回调处理
    private void CloseActiveSessionIfNeeded()
    {
        InventoryScreenSessionContext sessionContext = _activeSessionContext;

        if (LootChestGrid != null)
        {
            InventoryScreenSessionResult sessionResult = BuildSessionResult(sessionContext);

            LootChestGrid.ClearUI();
            LootChestGrid.gameObject.SetActive(false);

            if (sessionContext != null && sessionContext.UseCustomPlayerInventory && PocketGrid != null)
            {
                PocketGrid.ClearUI();
            }

            _activeSessionContext = null;
            RestoreStandardPlayerInventoryUiState();
            sessionContext?.OnClose?.Invoke(sessionResult);
        }
        else
        {
            _activeSessionContext = null;
            RestoreStandardPlayerInventoryUiState();
        }
    }

    // 根据当前会话构建关闭时要导出的左右容器快照
    private InventoryScreenSessionResult BuildSessionResult(InventoryScreenSessionContext sessionContext)
    {
        InventoryScreenSessionResult result = new InventoryScreenSessionResult();

        if (sessionContext != null && sessionContext.UseCustomPlayerInventory && PocketGrid != null)
        {
            result.PlayerItems = CloneSaveDataList(PocketGrid.ExtractSaveData());
            result.PlayerCellStates = CloneCellStateList(PocketGrid.ExtractCellStateData());
        }

        if (sessionContext != null && LootChestGrid != null)
        {
            result.ExternalItems = CloneSaveDataList(LootChestGrid.ExtractSaveData());
            result.ExternalCellStates = CloneCellStateList(LootChestGrid.ExtractCellStateData());
        }

        return result;
    }

    // 把当前会话的数据投影到界面中的玩家格子和外部容器格子
    private void PrepareSessionForDisplay(InventoryScreenSessionContext sessionContext)
    {
        if (sessionContext == null)
        {
            return;
        }

        if (sessionContext.UseCustomPlayerInventory)
        {
            ApplyCustomPlayerInventoryUiState();

            if (PocketGrid != null)
            {
                PocketGrid.RebuildGridUI(
                    Mathf.Max(1, sessionContext.PlayerColumns),
                    Mathf.Max(1, sessionContext.PlayerRows),
                    new List<Vector2Int>(sessionContext.PlayerBlockedCells ?? new List<Vector2Int>()));
                PocketGrid.LoadFromRuntimeState(
                    CloneSaveDataList(sessionContext.PlayerItems),
                    CloneCellStateList(sessionContext.PlayerCellStates));
            }
        }
        else
        {
            RestoreStandardPlayerInventoryUiState();
        }

        if (LootChestGrid != null)
        {
            LootChestGrid.RebuildGridUI(
                Mathf.Max(1, sessionContext.ExternalColumns),
                Mathf.Max(1, sessionContext.ExternalRows),
                new List<Vector2Int>(sessionContext.ExternalBlockedCells ?? new List<Vector2Int>()));
            LootChestGrid.LoadFromRuntimeState(
                CloneSaveDataList(sessionContext.ExternalItems),
                CloneCellStateList(sessionContext.ExternalCellStates));
            LootChestGrid.transform.SetAsLastSibling();
        }
    }

    // 根据当前会话模式刷新界面显隐：普通模式展示角色装备联动格，自定义模式只保留玩家格子和外部容器
    private void RefreshVisibleStateForCurrentContext()
    {
        if (UsesCustomPlayerInventory)
        {
            ApplyCustomPlayerInventoryUiState();

            if (PocketGrid != null)
            {
                PocketGrid.gameObject.SetActive(true);
            }
        }
        else
        {
            RestoreStandardPlayerInventoryUiState();
            RefreshCharacterContainerState(true);
        }

        if (LootChestGrid != null)
        {
            LootChestGrid.gameObject.SetActive(HasActiveExternalContainer);
        }
    }

    // 自定义会话期间隐藏主玩法专属的装备槽和联动格，保留一个扁平玩家格子即可
    private void ApplyCustomPlayerInventoryUiState()
    {
        if (_customPlayerInventoryUiApplied)
        {
            return;
        }

        _rigSlotWasActive = RigSlot != null && RigSlot.gameObject.activeSelf;
        _backpackSlotWasActive = BackpackSlot != null && BackpackSlot.gameObject.activeSelf;
        _tacticalRigGridWasActive = TacticalRigGrid != null && TacticalRigGrid.gameObject.activeSelf;
        _backpackGridWasActive = BackpackGrid != null && BackpackGrid.gameObject.activeSelf;

        if (RigSlot != null)
        {
            RigSlot.gameObject.SetActive(false);
        }

        if (BackpackSlot != null)
        {
            BackpackSlot.gameObject.SetActive(false);
        }

        if (TacticalRigGrid != null)
        {
            TacticalRigGrid.gameObject.SetActive(false);
        }

        if (BackpackGrid != null)
        {
            BackpackGrid.gameObject.SetActive(false);
        }

        _customPlayerInventoryUiApplied = true;
    }

    // 退出自定义会话后恢复主玩法原本的装备槽与联动格显隐
    private void RestoreStandardPlayerInventoryUiState()
    {
        if (!_customPlayerInventoryUiApplied)
        {
            return;
        }

        if (RigSlot != null)
        {
            RigSlot.gameObject.SetActive(_rigSlotWasActive);
        }

        if (BackpackSlot != null)
        {
            BackpackSlot.gameObject.SetActive(_backpackSlotWasActive);
        }

        if (TacticalRigGrid != null)
        {
            TacticalRigGrid.gameObject.SetActive(_tacticalRigGridWasActive);
        }

        if (BackpackGrid != null)
        {
            BackpackGrid.gameObject.SetActive(_backpackGridWasActive);
        }

        _customPlayerInventoryUiApplied = false;
    }

    // 只有对应装备槽已装备时，才允许把物品转入它关联的内部网格
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

    // 解析运行时物品视图的合法落点，同时复用物品自己的放置限制判断
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

    // 静态配置版的落点解析，供直接拾取逻辑使用
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

    // 根据静态物品尺寸在目标网格中寻找首个可用空位
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

    // 世界掉落物版本的落点解析，额外考虑容器内部快照的限制
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

    // 世界掉落物进入目标网格前，需要先通过背包规则校验
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

    // 统一处理世界容器的拾取入口，按类型分发到对应装备或替换逻辑
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

    // 胸挂替换只做装备交换，不迁移内部内容
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

    // 背包替换会优先尝试把旧包内容重排进新包，失败后再做普通交换
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

    // 当新包容量更大时，尝试把新包原内容和旧包内容合并重排成一套布局
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

    // 为世界容器创建临时 UI 物品，并尝试直接装备到指定槽位
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

    // 把世界掉落容器转换成可装备的浮动物品视图
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

    // 将卸下来的容器重新生成到场景里，并保留它当前的内部状态
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

    // 掉落物默认生成在玩家前方，避免直接压在角色脚下
    private static Vector3 GetWorldDropPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 1.5f + Vector3.up;
        }

        return Vector3.zero;
    }

    // 以总格数减去 blocked cell 的方式估算容器容量
    private static int GetContainerCapacity(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        int blockedCount = itemData.BlockedCells != null ? itemData.BlockedCells.Count : 0;
        return Mathf.Max(0, itemData.ContainerColumns * itemData.ContainerRows - blockedCount);
    }

    // 世界容器入格时，需要补充校验背包不能装背包以及非空胸挂不能进背包
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

    // 静态配置版规则校验，主要用于直接拾取和自动放置
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

    // 仅以保存快照中的物品列表和格子状态判断容器是否为空
    private static bool IsContainerSnapshotEmpty(
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        bool hasItems = internalItems != null && internalItems.Count > 0;
        bool hasCellStates = internalCellStates != null && internalCellStates.Count > 0;
        return !hasItems && !hasCellStates;
    }

    // 根据界面开关状态决定是否展示背包和胸挂的联动内部网格
    private void RefreshCharacterContainerState(bool showLinkedGrids)
    {
        BackpackSlot?.InitializeRuntimeState(showLinkedGrids);
        RigSlot?.InitializeRuntimeState(showLinkedGrids);
    }

    /// <summary>
    /// 根据屏幕坐标命中装备槽
    /// 用显式矩形检测替代纯 UI 射线，避免拖拽物挡住槽位
    /// </summary>
    /// <param name="screenPosition">当前屏幕坐标</param>
    /// <param name="eventCamera">用于 UI 计算的相机</param>
    /// <returns>命中的装备槽，没有则返回空</returns>
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

    // 深拷贝容器物品快照，确保 UI 编辑和世界实例不会共享状态引用
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

    // 用矩形包含关系检测装备槽命中，避免被拖拽物体的 Raycast 阻挡
    private static bool IsScreenPointInsideSlot(EquipmentSlotUI slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null)
        {
            return false;
        }

        RectTransform slotRect = slot.transform as RectTransform;
        return slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera);
    }

    // 深拷贝容器格子状态，避免多个运行时对象互相污染 blocked cell 数据
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
