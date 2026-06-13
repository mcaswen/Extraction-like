using System.Collections.Generic;
using System;
using System.Text;
using Gameplay.Agent.Runtime;
using UnityEngine;

/// <summary>
/// 背包模块总控制器
/// 负责背包开关、容器打开、快捷转移和拾取路由
/// </summary>
public class InventoryScreenController : MonoBehaviour
{
    public static InventoryScreenController Instance { get; private set; }

    private const float LootPanelHorizontalPadding = 24f;
    private const float LootPanelVerticalPadding = 20f;
    private const float LootHeaderHeight = 56f;
    private const float LootHeaderGap = 10f;

    [Header("Agent Focus")]
    [SerializeField] private bool _syncWithFocusedAgent = true;

    [Header("Debug")]
    [SerializeField] private bool _logInventoryDebug;

    public bool IsInventoryOpen { get; private set; }

    [Header("Legacy Container Slots")]
    public EquipmentSlotUI RigSlot;
    public EquipmentSlotUI BackpackSlot;

    [Header("Equipment Slots")]
    public EquipmentSlotUI HeadSlot;
    public EquipmentSlotUI BodySlot;
    public EquipmentSlotUI FaceSlot;
    public EquipmentSlotUI HeadphoneSlot;
    public EquipmentSlotUI TotemSlotA;
    public EquipmentSlotUI TotemSlotB;

    [Header("Default Backpack")]
    public InventoryItemData DefaultBackpackItem;

    [Header("Carry Load")]
    [Min(0.1f)]
    public float MaxCarryWeight = 30f;


    [Header("Panels")]
    public GameObject InventoryPanel;
    public GameObject LootChestPanel;
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
    private readonly Dictionary<string, CharacterInventorySnapshot> _inventorySnapshotsByAgentId =
        new Dictionary<string, CharacterInventorySnapshot>();
    private AgentRuntimeRegistry _agentRegistry;
    private bool _isAgentFocusSubscribed;
    private string _activeInventoryAgentId;
    private bool _isInventoryTimePauseApplied;
    private float _timeScaleBeforeInventoryPause = 1f;
    private float _fixedDeltaTimeBeforeInventoryPause = 0.02f;
    private bool _missingBackpackLinkedGridLogged;

    public InventoryScreenSessionContext ActiveSessionContext => _activeSessionContext;
    public bool HasActiveExternalContainer => _activeSessionContext != null;
    public bool UsesCustomPlayerInventory => _activeSessionContext != null && _activeSessionContext.UseCustomPlayerInventory;
    public InventoryUIController ActiveExternalGrid => HasActiveExternalContainer ? LootChestGrid : null;
    public InventoryUIController ActivePlayerGrid => BackpackGrid;
    public string ActiveInventoryAgentId => _activeInventoryAgentId ?? string.Empty;

    public float GetCurrentCarryWeight()
    {
        RefreshCarryLoadRuntimeState();
        return CalculateCurrentCarryWeightWithoutRefresh();
    }

    public float GetMaxCarryWeight()
    {
        return Mathf.Max(0.1f, MaxCarryWeight);
    }

    public float GetCarryWeightRatio()
    {
        return Mathf.Clamp01(GetCurrentCarryWeight() / GetMaxCarryWeight());
    }

    public float GetCurrentBackpackOccupiedCells()
    {
        RefreshCarryLoadRuntimeState();
        return GetGridOccupiedCellCount(BackpackGrid);
    }

    public float GetMaxBackpackUsableCells()
    {
        return Mathf.Max(1f, GetGridUsableCellCount(BackpackGrid));
    }

    public float GetBackpackCapacityRatio()
    {
        return Mathf.Clamp01(GetCurrentBackpackOccupiedCells() / GetMaxBackpackUsableCells());
    }

    private void Awake()
    {
        Instance = this;
        InitializeRuntimeScreen();
        SubscribeAgentFocus();
    }

    private void OnDestroy()
    {
        ReleaseInventoryTimePause();

        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeAgentFocus();
    }

    private void Start()
    {
        EnsureDefaultBackpackEquipped();
        RefreshCharacterContainerState(false);
        BindToFocusedAgentIfNeeded();
    }

    private void Update()
    {
        BindToFocusedAgentIfNeeded();

        if (Input.GetKeyDown(KeyCode.I))
        {
            DumpBackpackCapacitySyncDebug();
        }
    }

    private void SubscribeAgentFocus()
    {
        if (!_syncWithFocusedAgent || _isAgentFocusSubscribed)
        {
            return;
        }

        _agentRegistry = AgentRuntimeRegistry.GetOrCreate();
        if (_agentRegistry == null)
        {
            return;
        }

        _agentRegistry.FocusedAgentChanged += HandleFocusedAgentChanged;
        _isAgentFocusSubscribed = true;
    }

    private void UnsubscribeAgentFocus()
    {
        if (_agentRegistry != null && _isAgentFocusSubscribed)
        {
            _agentRegistry.FocusedAgentChanged -= HandleFocusedAgentChanged;
        }

        _isAgentFocusSubscribed = false;
        _agentRegistry = null;
    }

    private void BindToFocusedAgentIfNeeded()
    {
        if (!_syncWithFocusedAgent)
        {
            return;
        }

        SubscribeAgentFocus();
        if (_agentRegistry == null || !_agentRegistry.TryGetFocusedHandle(out AgentRuntimeHandle focusedHandle))
        {
            return;
        }

        SwitchActiveInventoryAgent(focusedHandle.AgentId.Value);
    }

    private void HandleFocusedAgentChanged(
        AgentRuntimeHandle previousHandle,
        AgentRuntimeHandle currentHandle)
    {
        if (!currentHandle.IsValid)
        {
            PersistActiveAgentInventory();
            _activeInventoryAgentId = string.Empty;
            return;
        }

        SwitchActiveInventoryAgent(currentHandle.AgentId.Value);
    }

    private void SwitchActiveInventoryAgent(string agentId)
    {
        string normalizedAgentId = string.IsNullOrWhiteSpace(agentId) ? string.Empty : agentId.Trim();
        if (string.IsNullOrEmpty(normalizedAgentId) || string.Equals(_activeInventoryAgentId, normalizedAgentId, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrEmpty(_activeInventoryAgentId))
        {
            _activeInventoryAgentId = normalizedAgentId;
            SaveInventorySnapshot(normalizedAgentId);
            return;
        }

        PersistActiveAgentInventory();
        _activeInventoryAgentId = normalizedAgentId;
        RestoreInventorySnapshot(normalizedAgentId);
    }

    private void PersistActiveAgentInventory()
    {
        if (string.IsNullOrEmpty(_activeInventoryAgentId))
        {
            return;
        }

        EndCurrentDragIfNeeded();

        if (_activeSessionContext != null)
        {
            CloseActiveSessionIfNeeded();
        }

        SyncCharacterContainerRuntimeState();
        SaveInventorySnapshot(_activeInventoryAgentId);
    }

    private void SaveInventorySnapshot(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        _inventorySnapshotsByAgentId[agentId] = CreateCharacterInventorySnapshot();
    }

    private void RestoreInventorySnapshot(string agentId)
    {
        InventoryItemInfoPanelController.Instance?.Hide();

        if (_inventorySnapshotsByAgentId.TryGetValue(agentId, out CharacterInventorySnapshot snapshot))
        {
            LoadCharacterInventorySnapshot(snapshot);
        }
        else
        {
            ClearCharacterInventoryUi();
            EnsureDefaultBackpackEquipped();
        }

        if (IsInventoryOpen)
        {
            RefreshVisibleStateForCurrentContext();
        }
        else
        {
            RefreshCharacterContainerState(false);
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
            SetLootUiVisible(false);
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
            if (_logInventoryDebug)
            {
                Debug.LogWarning(
                    $"[InventoryScreen] OpenLootBox skipped. lootBox={(lootBox != null ? lootBox.name : "null")}, " +
                    $"lootGrid={(LootChestGrid != null ? LootChestGrid.name : "null")}.",
                    this);
            }

            return;
        }

        if (_logInventoryDebug)
        {
            Debug.Log($"[InventoryScreen] OpenLootBox -> {lootBox.name}.", this);
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
            if (_logInventoryDebug)
            {
                Debug.LogWarning(
                    $"[InventoryScreen] OpenInventorySession skipped. session={(sessionContext != null ? sessionContext.DisplayName : "null")}, " +
                    $"lootGrid={(LootChestGrid != null ? LootChestGrid.name : "null")}.",
                    this);
            }

            return;
        }

        bool wasInventoryOpen = IsInventoryOpen;

        if (_logInventoryDebug)
        {
            Debug.Log(
                $"[InventoryScreen] OpenInventorySession start. display={sessionContext.DisplayName}, " +
                $"wasOpen={wasInventoryOpen}, activeAgent={ActiveInventoryAgentId}.",
                this);
        }

        if (_activeSessionContext != null)
        {
            CloseActiveSessionIfNeeded();
        }

        sessionContext.BeforeOpen?.Invoke();
        _activeSessionContext = sessionContext;
        InventoryItemInfoPanelController.Instance?.Hide();
        PrepareSessionForDisplay(sessionContext);

        if (!wasInventoryOpen)
        {
            OpenInventory();
        }
        else
        {
            RefreshVisibleStateForCurrentContext();
        }

        if (_logInventoryDebug)
        {
            Debug.Log($"[InventoryScreen] OpenInventorySession complete. isOpen={IsInventoryOpen}.", this);
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
            if (_logInventoryDebug)
            {
                Debug.Log("[InventoryScreen] OpenInventory ignored: already open.", this);
            }

            return;
        }

        if (_logInventoryDebug)
        {
            Debug.Log(
                $"[InventoryScreen] OpenInventory start. panel={(InventoryPanel != null ? InventoryPanel.name : "null")}, " +
                $"backpackGrid={(BackpackGrid != null ? BackpackGrid.name : "null")}, " +
                $"backpackSlot={(BackpackSlot != null ? BackpackSlot.name : "null")}, " +
                $"activeAgent={ActiveInventoryAgentId}.",
                this);
        }

        IsInventoryOpen = true;
        ApplyInventoryTimePause();
        OpenInventoryInternal();

        if (_logInventoryDebug)
        {
            Debug.Log($"[InventoryScreen] OpenInventory complete. panelActive={(InventoryPanel != null && InventoryPanel.activeSelf)}.", this);
        }
    }

    /// <summary>
    /// 显式关闭背包界面
    /// </summary>
    public void CloseInventory()
    {
        if (!IsInventoryOpen)
        {
            if (_logInventoryDebug)
            {
                Debug.Log("[InventoryScreen] CloseInventory ignored: already closed.", this);
            }

            return;
        }

        if (_logInventoryDebug)
        {
            Debug.Log($"[InventoryScreen] CloseInventory start. activeAgent={ActiveInventoryAgentId}.", this);
        }

        IsInventoryOpen = false;
        CloseInventoryInternal();
        ReleaseInventoryTimePause();

        if (_logInventoryDebug)
        {
            Debug.Log("[InventoryScreen] CloseInventory complete.", this);
        }
    }

    /// <summary>
    /// 判断当前会话是否仍然是指定引用
    /// </summary>
    public bool IsSessionContextActive(InventoryScreenSessionContext sessionContext)
    {
        return sessionContext != null && ReferenceEquals(_activeSessionContext, sessionContext);
    }

    private void ApplyInventoryTimePause()
    {
        if (_isInventoryTimePauseApplied)
        {
            return;
        }

        _timeScaleBeforeInventoryPause = Time.timeScale;
        _fixedDeltaTimeBeforeInventoryPause = Time.fixedDeltaTime;
        _isInventoryTimePauseApplied = true;
        Time.timeScale = 0f;
    }

    private void ReleaseInventoryTimePause()
    {
        if (!_isInventoryTimePauseApplied)
        {
            return;
        }

        _isInventoryTimePauseApplied = false;

        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            return;
        }

        Time.timeScale = _timeScaleBeforeInventoryPause;
        Time.fixedDeltaTime = _fixedDeltaTimeBeforeInventoryPause;
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

        EnsureDefaultBackpackEquipped();
        RefreshCharacterContainerState(IsInventoryOpen);

        if (sourceGrid == LootChestGrid)
        {
            return TryResolveAvailableSpace(
                BackpackGrid,
                itemView,
                out targetGrid,
                out position,
                out needsRotation);
        }

        if (sourceGrid == BackpackGrid)
        {
            if (HasActiveExternalContainer)
            {
                return TryResolveAvailableSpace(
                    LootChestGrid,
                    itemView,
                    out targetGrid,
                    out position,
                    out needsRotation);
            }

            return false;
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

        EnsureDefaultBackpackEquipped();
        RefreshCharacterContainerState(IsInventoryOpen);

        if (TryResolveAvailableSpace(
            BackpackGrid,
            itemData,
            out InventoryUIController backpackTarget,
            out Vector2Int backpackPosition,
            out bool backpackRotation))
        {
            InventoryItemFactory.Instance.SpawnItemInGrid(
                itemData,
                backpackTarget,
                backpackPosition.x,
                backpackPosition.y,
                amount,
                backpackRotation);
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

        EnsureDefaultBackpackEquipped();
        RefreshCharacterContainerState(IsInventoryOpen);

        if (TryResolveAvailableSpace(
            BackpackGrid,
            worldItem,
            out InventoryUIController backpackTarget,
            out Vector2Int backpackPosition,
            out bool backpackRotation))
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

        return false;
    }

    /// <summary>
    /// 使用装备交互键尝试装备或替换世界中的容器
    /// </summary>
    /// <param name="worldItem">场景中的容器掉落物</param>
    /// <returns>是否成功装备</returns>
    public bool TryEquipWorldContainer(WorldLootItem worldItem)
    {
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

        if (slot.AcceptedEquipmentKind != EquipmentSlotKind.None)
        {
            if (incomingItem.ItemData.Type != ItemType.Equipment ||
                incomingItem.ItemData.EquipmentKind != slot.AcceptedEquipmentKind)
            {
                return false;
            }
        }
        else if (incomingItem.ItemData.Type != slot.AcceptedType)
        {
            return false;
        }

        if (slot.AcceptedEquipmentKind == EquipmentSlotKind.None && slot.HasEquippedItem)
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

        if (slot.AcceptedEquipmentKind != EquipmentSlotKind.None)
        {
            if (!TryMoveReleasedEquipmentToBackpack(oldItem))
            {
                slot.ReleaseEquippedItem();
                slot.TryEquip(oldItem);
                return false;
            }

            return true;
        }

        slot.ReleaseEquippedItem();
        slot.TryEquip(oldItem);
        return false;
    }

    private bool TryMoveReleasedEquipmentToBackpack(DraggableItemUI oldItem)
    {
        if (oldItem == null || oldItem.ItemData == null || BackpackGrid == null)
        {
            return false;
        }

        if (!TryResolveAvailableSpace(
            BackpackGrid,
            oldItem,
            out InventoryUIController targetGrid,
            out Vector2Int position,
            out bool needsRotation))
        {
            return false;
        }

        oldItem.transform.SetParent(targetGrid.ItemContainer, false);
        oldItem.CurrentGrid = targetGrid;
        oldItem.PlaceSuccessfully(position, needsRotation);
        return true;
    }

    // 打开背包面板时，同步角色容器状态并释放鼠标
    private void OpenInventoryInternal()
    {
        if (_logInventoryDebug)
        {
            Debug.Log(
                $"[InventoryScreen] OpenInventoryInternal. hasPanel={InventoryPanel != null}, " +
                $"hasBackpackGrid={BackpackGrid != null}, hasBackpackSlot={BackpackSlot != null}, " +
                $"hasDefaultBackpack={DefaultBackpackItem != null}.",
                this);
        }

        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(true);
        }
        else if (_logInventoryDebug)
        {
            Debug.LogWarning("[InventoryScreen] OpenInventoryInternal could not activate UI: InventoryPanel is null.", this);
        }

        RefreshVisibleStateForCurrentContext();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_logInventoryDebug)
        {
            Debug.Log(
                $"[InventoryScreen] OpenInventoryInternal complete. panelActive={(InventoryPanel != null && InventoryPanel.activeSelf)}, " +
                $"backpackGridActive={(BackpackGrid != null && BackpackGrid.gameObject.activeSelf)}, " +
                $"cursorVisible={Cursor.visible}, cursorLock={Cursor.lockState}.",
                this);
        }
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
        SyncCharacterContainerRuntimeState();
        RefreshCharacterContainerState(false);

        if (InventoryPanel != null)
        {
            InventoryPanel.SetActive(false);
        }

        InventoryItemInfoPanelController.Instance?.Hide();

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
            SetLootUiVisible(false);

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
            RestoreStandardPlayerInventoryUiState();
            EnsureDefaultBackpackEquipped();

            if (BackpackGrid != null)
            {
                BackpackGrid.RebuildGridUI(5, 6, new List<Vector2Int>());
                if (sessionContext.UseCustomPlayerInventory)
                {
                    BackpackGrid.LoadFromRuntimeState(
                        CloneSaveDataList(sessionContext.PlayerItems),
                        CloneCellStateList(sessionContext.PlayerCellStates));
                }
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
            LayoutLootPanelAroundGrid();
        }
    }

    private void EnsureBackpackGridVisible()
    {
        if (BackpackGrid == null)
        {
            return;
        }

        BackpackGrid.gameObject.SetActive(true);

        InventoryGridController gridController = BackpackGrid.GetGridController();
        if (gridController == null)
        {
            return;
        }

        if (gridController.Columns != 5 || gridController.Rows != 6)
        {
            BackpackGrid.RebuildGridUI(5, 6, new List<Vector2Int>());
            return;
        }

        if (BackpackGrid.NeedsBackgroundCellRefresh())
        {
            BackpackGrid.RefreshBackgroundCellsFromCurrentConfig();
        }
    }

    // 根据当前会话模式刷新界面显隐：普通模式展示角色装备联动格，自定义模式只保留玩家格子和外部容器
    private void RefreshVisibleStateForCurrentContext()
    {
        RestoreStandardPlayerInventoryUiState();
        EnsureDefaultBackpackEquipped();
        RefreshCharacterContainerState(true);
        EnsureBackpackGridVisible();

        if (PocketGrid != null)
        {
            PocketGrid.gameObject.SetActive(false);
        }

        if (TacticalRigGrid != null)
        {
            TacticalRigGrid.gameObject.SetActive(false);
        }

        if (LootChestGrid != null)
        {
            SetLootUiVisible(HasActiveExternalContainer);
            if (HasActiveExternalContainer)
            {
                LayoutLootPanelAroundGrid();
            }
        }
    }

    private void SetLootUiVisible(bool visible)
    {
        GameObject lootPanelRoot = ResolveLootPanelRoot();
        if (lootPanelRoot != null)
        {
            lootPanelRoot.SetActive(visible);
        }

        if (LootChestGrid != null && (lootPanelRoot == null || lootPanelRoot == LootChestGrid.gameObject))
        {
            LootChestGrid.gameObject.SetActive(visible);
        }
    }

    private void LayoutLootPanelAroundGrid()
    {
        if (LootChestGrid == null)
        {
            return;
        }

        GameObject lootPanelRoot = ResolveLootPanelRoot();
        if (lootPanelRoot == null || lootPanelRoot == LootChestGrid.gameObject)
        {
            return;
        }

        RectTransform gridRect = LootChestGrid.GetComponent<RectTransform>();
        RectTransform panelRect = lootPanelRoot.GetComponent<RectTransform>();
        if (gridRect == null || panelRect == null)
        {
            return;
        }

        Vector2 gridSize = gridRect.sizeDelta;
        if (gridSize.x <= 0f || gridSize.y <= 0f)
        {
            InventoryGridController gridController = LootChestGrid.GetGridController();
            if (gridController == null)
            {
                return;
            }

            gridSize = LootChestGrid.GetItemActualSize(
                Mathf.Max(1, gridController.Columns),
                Mathf.Max(1, gridController.Rows));
        }

        Vector2 panelSize = new Vector2(
            gridSize.x + LootPanelHorizontalPadding * 2f,
            LootPanelVerticalPadding * 2f + LootHeaderHeight + LootHeaderGap + gridSize.y);
        panelRect.sizeDelta = panelSize;

        float centeredGridPanelY =
            LootPanelVerticalPadding + LootHeaderHeight + LootHeaderGap + gridSize.y * 0.5f
            - panelSize.y * (1f - panelRect.pivot.y);
        panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, centeredGridPanelY);

        RectTransform headerRow = FindChildRectTransform(lootPanelRoot.transform, "LootHeaderRow");
        if (headerRow != null)
        {
            headerRow.anchorMin = new Vector2(0f, 1f);
            headerRow.anchorMax = new Vector2(1f, 1f);
            headerRow.pivot = new Vector2(0.5f, 1f);
            headerRow.anchoredPosition = new Vector2(0f, -LootPanelVerticalPadding);
            headerRow.sizeDelta = new Vector2(-LootPanelHorizontalPadding * 2f, LootHeaderHeight);
        }

        RectTransform gridContainer = FindChildRectTransform(lootPanelRoot.transform, "LootGridContainer");
        RectTransform gridParent = gridContainer != null ? gridContainer : gridRect.parent as RectTransform;
        if (gridParent != null && gridParent != panelRect)
        {
            gridParent.anchorMin = new Vector2(0.5f, 1f);
            gridParent.anchorMax = new Vector2(0.5f, 1f);
            gridParent.pivot = new Vector2(0.5f, 1f);
            gridParent.anchoredPosition = new Vector2(
                0f,
                -(LootPanelVerticalPadding + LootHeaderHeight + LootHeaderGap));
            gridParent.sizeDelta = gridSize;
        }

        gridRect.anchorMin = new Vector2(0.5f, 1f);
        gridRect.anchorMax = new Vector2(0.5f, 1f);
        gridRect.pivot = new Vector2(0.5f, 1f);
        gridRect.anchoredPosition = Vector2.zero;
    }

    private GameObject ResolveLootPanelRoot()
    {
        if (LootChestPanel != null)
        {
            return LootChestPanel;
        }

        if (LootChestGrid == null)
        {
            return null;
        }

        Transform current = LootChestGrid.transform.parent;
        while (current != null)
        {
            if (current.name == "LootSearchPanel")
            {
                LootChestPanel = current.gameObject;
                return LootChestPanel;
            }

            current = current.parent;
        }

        LootChestPanel = LootChestGrid.gameObject;
        return LootChestPanel;
    }

    private static RectTransform FindChildRectTransform(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
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

        //if (BackpackGrid != null)
        //{
        //    BackpackGrid.gameObject.SetActive(false);
        //}

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

    // 统一处理世界容器的拾取入口
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

        return false;
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

    // 世界容器入格时，需要补充校验非空容器不能直接塞入背包内容格
    private static bool CanWorldItemEnterGrid(WorldLootItem worldItem, InventoryUIController targetGrid)
    {
        if (worldItem == null || worldItem.ItemData == null || targetGrid == null)
        {
            return false;
        }

        if (IsContainerItem(worldItem.ItemData.Type) &&
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

        return true;
    }

    private static bool IsContainerItem(ItemType itemType)
    {
        return itemType == ItemType.Bag || itemType == ItemType.Rig;
    }

    private static float GetSlotCarryWeight(EquipmentSlotUI slot)
    {
        if (slot == null || !slot.HasEquippedItem)
        {
            return 0f;
        }

        DraggableItemUI equippedItem = slot.EquippedItem;
        float carryWeight = GetItemViewOwnCarryWeight(equippedItem);
        carryWeight += GetSlotContentsCarryWeight(slot, equippedItem);
        return carryWeight;
    }

    private static float GetItemViewCarryWeight(DraggableItemUI itemView)
    {
        if (itemView == null || itemView.ItemData == null)
        {
            return 0f;
        }

        float carryWeight = GetItemViewOwnCarryWeight(itemView);
        carryWeight += GetSavedItemsCarryWeight(itemView.InternalItems);
        return carryWeight;
    }

    private static float GetItemViewOwnCarryWeight(DraggableItemUI itemView)
    {
        if (itemView == null || itemView.ItemData == null)
        {
            return 0f;
        }

        int amount = Mathf.Max(1, itemView.CurrentAmount);
        return Mathf.Max(0f, itemView.ItemData.CarryWeight) * amount;
    }

    private static float GetSlotContentsCarryWeight(EquipmentSlotUI slot, DraggableItemUI equippedItem)
    {
        if (slot == null || equippedItem == null)
        {
            return 0f;
        }

        InventoryUIController linkedGrid = slot.LinkedGrid;
        if (linkedGrid == null)
        {
            return GetSavedItemsCarryWeight(equippedItem.InternalItems);
        }

        bool gridHasRuntimeItems = HasRuntimeItemViews(linkedGrid);
        if (linkedGrid.gameObject.activeInHierarchy || gridHasRuntimeItems)
        {
            return GetSavedItemsCarryWeight(linkedGrid.ExtractSaveData());
        }

        return GetSavedItemsCarryWeight(equippedItem.InternalItems);
    }

    private void SyncCharacterContainerRuntimeState()
    {
        BackpackSlot?.SyncEquippedItemRuntimeDataFromGrid();
        RigSlot?.SyncEquippedItemRuntimeDataFromGrid();
        HeadSlot?.SyncEquippedItemRuntimeDataFromGrid();
        BodySlot?.SyncEquippedItemRuntimeDataFromGrid();
        FaceSlot?.SyncEquippedItemRuntimeDataFromGrid();
        HeadphoneSlot?.SyncEquippedItemRuntimeDataFromGrid();
        TotemSlotA?.SyncEquippedItemRuntimeDataFromGrid();
        TotemSlotB?.SyncEquippedItemRuntimeDataFromGrid();
    }

    private void RefreshCarryLoadRuntimeState()
    {
        EnsureDefaultBackpackEquipped();

        if (BackpackSlot != null &&
            BackpackGrid != null &&
            BackpackSlot.LinkedGrid == null &&
            !_missingBackpackLinkedGridLogged)
        {
            _missingBackpackLinkedGridLogged = true;
            Debug.LogError(
                "[InventoryScreen] BackpackSlot.LinkedGrid is missing. Assign BackpackGrid on the BackpackSlot component, otherwise normal items in the backpack grid will not affect HUD carry load.",
                this);
        }

        SyncCharacterContainerRuntimeState();
    }

    private float CalculateCurrentCarryWeightWithoutRefresh()
    {
        float carryWeight = 0f;
        carryWeight += GetSlotOrLooseGridCarryWeight(BackpackSlot, BackpackGrid);
        carryWeight += GetSlotOrLooseGridCarryWeight(RigSlot, TacticalRigGrid);
        carryWeight += GetSlotCarryWeight(HeadSlot);
        carryWeight += GetSlotCarryWeight(BodySlot);
        carryWeight += GetSlotCarryWeight(FaceSlot);
        carryWeight += GetSlotCarryWeight(HeadphoneSlot);
        carryWeight += GetSlotCarryWeight(TotemSlotA);
        carryWeight += GetSlotCarryWeight(TotemSlotB);
        return Mathf.Max(0f, carryWeight);
    }

    private static float GetSlotOrLooseGridCarryWeight(EquipmentSlotUI slot, InventoryUIController fallbackGrid)
    {
        if (slot != null && slot.HasEquippedItem)
        {
            return GetSlotCarryWeight(slot);
        }

        if (fallbackGrid == null)
        {
            return 0f;
        }

        return GetSavedItemsCarryWeight(fallbackGrid.ExtractSaveData());
    }

    private void DumpBackpackCapacitySyncDebug()
    {
        float beforeWeight = CalculateCurrentCarryWeightWithoutRefresh();
        float maxWeight = GetMaxCarryWeight();
        float occupiedCellsBefore = GetGridOccupiedCellCount(BackpackGrid);
        float usableCells = GetMaxBackpackUsableCells();
        StringBuilder builder = new StringBuilder(2048);
        builder.AppendLine(
            $"[InventoryCapacityDebug] time={Time.unscaledTime:0.###} " +
            $"activeAgent={ActiveInventoryAgentId} inventoryOpen={IsInventoryOpen} " +
            $"activeSession={(_activeSessionContext != null ? _activeSessionContext.DisplayName : "none")} " +
            $"usesCustomPlayerInventory={UsesCustomPlayerInventory}");
        builder.AppendLine($"beforeSyncWeight: current={beforeWeight:0.###} max={maxWeight:0.###} ratio={(beforeWeight / Mathf.Max(0.1f, maxWeight)):0.###}");
        builder.AppendLine($"beforeSyncCapacity: occupiedCells={occupiedCellsBefore:0.###} usableCells={usableCells:0.###} ratio={(occupiedCellsBefore / Mathf.Max(1f, usableCells)):0.###}");
        AppendSlotCarryDebug(builder, "BackpackSlot", BackpackSlot, BackpackGrid);
        AppendSlotCarryDebug(builder, "RigSlot", RigSlot, TacticalRigGrid);

        RefreshCarryLoadRuntimeState();

        float afterWeight = CalculateCurrentCarryWeightWithoutRefresh();
        float occupiedCellsAfter = GetGridOccupiedCellCount(BackpackGrid);
        builder.AppendLine($"afterSyncWeight: current={afterWeight:0.###} max={maxWeight:0.###} ratio={(afterWeight / Mathf.Max(0.1f, maxWeight)):0.###}");
        builder.AppendLine($"afterSyncCapacity: occupiedCells={occupiedCellsAfter:0.###} usableCells={usableCells:0.###} ratio={(occupiedCellsAfter / Mathf.Max(1f, usableCells)):0.###}");
        AppendSlotCarryDebug(builder, "BackpackSlot", BackpackSlot, BackpackGrid);
        AppendSlotCarryDebug(builder, "RigSlot", RigSlot, TacticalRigGrid);
        Debug.Log(builder.ToString(), this);
    }

    private static void AppendSlotCarryDebug(
        StringBuilder builder,
        string label,
        EquipmentSlotUI slot,
        InventoryUIController expectedGrid)
    {
        if (slot == null)
        {
            builder.AppendLine($"{label}: slot=null");
            return;
        }

        DraggableItemUI equippedItem = slot.EquippedItem;
        InventoryItemRuntimeState equippedState = slot.EquippedItemState;
        InventoryItemData itemData = equippedState != null && equippedState.ItemData != null
            ? equippedState.ItemData
            : equippedItem != null ? equippedItem.ItemData : null;
        InventoryUIController linkedGrid = slot.LinkedGrid;
        List<ContainerItemSaveData> internalItems = equippedState != null
            ? equippedState.InternalItems
            : equippedItem != null ? equippedItem.InternalItems : null;

        builder.AppendLine(
            $"{label}: active={slot.gameObject.activeSelf}/{slot.gameObject.activeInHierarchy} " +
            $"hasEquipped={slot.HasEquippedItem} item={(itemData != null ? itemData.ItemName : "none")} " +
            $"linkedGrid={(linkedGrid != null ? linkedGrid.name : "null")} " +
            $"matchesExpectedGrid={(linkedGrid != null && expectedGrid != null && ReferenceEquals(linkedGrid, expectedGrid))} " +
            $"slotWeight={GetSlotCarryWeight(slot):0.###} " +
            $"slotOrGridWeight={GetSlotOrLooseGridCarryWeight(slot, expectedGrid):0.###}");

        AppendGridCarryDebug(builder, $"{label}.linkedGrid", linkedGrid);
        if (expectedGrid != null && !ReferenceEquals(linkedGrid, expectedGrid))
        {
            AppendGridCarryDebug(builder, $"{label}.expectedGrid", expectedGrid);
        }

        builder.AppendLine(
            $"{label}.equippedInternalSnapshot: items={CountSavedItemsRecursive(internalItems)} " +
            $"weight={GetSavedItemsCarryWeight(internalItems):0.###}");
    }

    private static void AppendGridCarryDebug(StringBuilder builder, string label, InventoryUIController grid)
    {
        if (grid == null)
        {
            builder.AppendLine($"{label}: grid=null");
            return;
        }

        InventoryGridController gridController = grid.GetGridController();
        List<ContainerItemSaveData> saveData = grid.ExtractSaveData();
        builder.AppendLine(
            $"{label}: active={grid.gameObject.activeSelf}/{grid.gameObject.activeInHierarchy} " +
            $"itemsInView={CountRuntimeItemViews(grid)} savedItems={CountSavedItemsRecursive(saveData)} " +
            $"savedWeight={GetSavedItemsCarryWeight(saveData):0.###} " +
            $"occupiedCells={GetSavedItemsOccupiedCellCount(saveData):0.###} " +
            $"size={(gridController != null ? $"{gridController.Columns}x{gridController.Rows}" : "no-controller")} " +
            $"usableCells={GetGridUsableCellCount(grid):0.###} " +
            $"blocked={(gridController != null && gridController.BlockedCells != null ? gridController.BlockedCells.Count : 0)}");
    }

    private static int CountRuntimeItemViews(InventoryUIController grid)
    {
        if (grid == null || grid.ItemContainer == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Transform child in grid.ItemContainer)
        {
            if (child == grid.Highlighter)
            {
                continue;
            }

            if (child.GetComponent<DraggableItemUI>() != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSavedItemsRecursive(List<ContainerItemSaveData> items)
    {
        if (items == null)
        {
            return 0;
        }

        int count = 0;
        foreach (ContainerItemSaveData item in items)
        {
            if (item == null)
            {
                continue;
            }

            count++;
            count += CountSavedItemsRecursive(item.InternalItems);
        }

        return count;
    }

    private static float GetGridOccupiedCellCount(InventoryUIController grid)
    {
        if (grid == null)
        {
            return 0f;
        }

        return GetSavedItemsOccupiedCellCount(grid.ExtractSaveData());
    }

    private static float GetSavedItemsOccupiedCellCount(List<ContainerItemSaveData> items)
    {
        if (items == null)
        {
            return 0f;
        }

        float occupiedCells = 0f;
        foreach (ContainerItemSaveData item in items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            int width = item.IsRotated ? item.ItemData.Height : item.ItemData.Width;
            int height = item.IsRotated ? item.ItemData.Width : item.ItemData.Height;
            occupiedCells += Mathf.Max(1, width) * Mathf.Max(1, height);
        }

        return occupiedCells;
    }

    private static float GetGridUsableCellCount(InventoryUIController grid)
    {
        if (grid == null)
        {
            return 1f;
        }

        InventoryGridController gridController = grid.GetGridController();
        if (gridController == null)
        {
            return 1f;
        }

        int columns = Mathf.Max(1, gridController.Columns);
        int rows = Mathf.Max(1, gridController.Rows);
        int totalCells = columns * rows;
        int blockedCells = CountUniqueBlockedCellsInBounds(gridController.BlockedCells, columns, rows);
        return Mathf.Max(1, totalCells - blockedCells);
    }

    private static int CountUniqueBlockedCellsInBounds(IReadOnlyList<Vector2Int> blockedCells, int columns, int rows)
    {
        if (blockedCells == null || blockedCells.Count == 0)
        {
            return 0;
        }

        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
        for (int i = 0; i < blockedCells.Count; i++)
        {
            Vector2Int cell = blockedCells[i];
            if (cell.x < 0 || cell.y < 0 || cell.x >= columns || cell.y >= rows)
            {
                continue;
            }

            uniqueCells.Add(cell);
        }

        return uniqueCells.Count;
    }

    private CharacterInventorySnapshot CreateCharacterInventorySnapshot()
    {
        SyncCharacterContainerRuntimeState();
        CharacterInventorySnapshot snapshot = new CharacterInventorySnapshot
        {
            BackpackItem = CreateSlotSnapshot(BackpackSlot),
            RigItem = CreateSlotSnapshot(RigSlot),
            HeadItem = CreateSlotSnapshot(HeadSlot),
            BodyItem = CreateSlotSnapshot(BodySlot),
            FaceItem = CreateSlotSnapshot(FaceSlot),
            HeadphoneItem = CreateSlotSnapshot(HeadphoneSlot),
            TotemAItem = CreateSlotSnapshot(TotemSlotA),
            TotemBItem = CreateSlotSnapshot(TotemSlotB)
        };

        if (snapshot.BackpackItem == null && BackpackGrid != null)
        {
            snapshot.BackpackLooseItems = CloneSaveDataList(BackpackGrid.ExtractSaveData());
            snapshot.BackpackLooseCellStates = CloneCellStateList(BackpackGrid.ExtractCellStateData());
        }

        return snapshot;
    }

    private void LoadCharacterInventorySnapshot(CharacterInventorySnapshot snapshot)
    {
        ClearCharacterInventoryUi();

        if (snapshot == null)
        {
            EnsureDefaultBackpackEquipped();
            return;
        }

        LoadSlotSnapshot(BackpackSlot, snapshot.BackpackItem);
        LoadSlotSnapshot(RigSlot, snapshot.RigItem);
        LoadSlotSnapshot(HeadSlot, snapshot.HeadItem);
        LoadSlotSnapshot(BodySlot, snapshot.BodyItem);
        LoadSlotSnapshot(FaceSlot, snapshot.FaceItem);
        LoadSlotSnapshot(HeadphoneSlot, snapshot.HeadphoneItem);
        LoadSlotSnapshot(TotemSlotA, snapshot.TotemAItem);
        LoadSlotSnapshot(TotemSlotB, snapshot.TotemBItem);

        if (snapshot.BackpackItem == null && HasLooseBackpackSnapshot(snapshot))
        {
            RestoreLooseBackpackGridSnapshot(snapshot);
        }
        else
        {
            EnsureDefaultBackpackEquipped();
        }
    }

    private void ClearCharacterInventoryUi()
    {
        ClearEquipmentSlot(BackpackSlot);
        ClearEquipmentSlot(RigSlot);
        ClearEquipmentSlot(HeadSlot);
        ClearEquipmentSlot(BodySlot);
        ClearEquipmentSlot(FaceSlot);
        ClearEquipmentSlot(HeadphoneSlot);
        ClearEquipmentSlot(TotemSlotA);
        ClearEquipmentSlot(TotemSlotB);

        PocketGrid?.ClearUI();
        TacticalRigGrid?.ClearUI();
        BackpackGrid?.ClearUI();
    }

    private static ContainerItemSaveData CreateSlotSnapshot(EquipmentSlotUI slot)
    {
        if (slot == null || !slot.HasEquippedItem || slot.EquippedItemState == null)
        {
            return null;
        }

        slot.SyncEquippedItemRuntimeDataFromGrid();
        return slot.EquippedItemState.CreateSaveDataSnapshot(Vector2Int.zero, false);
    }

    private static void LoadSlotSnapshot(EquipmentSlotUI slot, ContainerItemSaveData itemSnapshot)
    {
        if (slot == null || itemSnapshot == null || itemSnapshot.ItemData == null || InventoryItemFactory.Instance == null)
        {
            return;
        }

        InventoryItemRuntimeState runtimeState = InventoryItemRuntimeState.Create(
            itemSnapshot.ItemData,
            itemSnapshot.Amount,
            itemSnapshot.InternalItems,
            itemSnapshot.InternalCellStates);
        runtimeState.ApplyContainerSaveData(itemSnapshot);

        DraggableItemUI itemView = InventoryItemFactory.Instance.CreateFloatingItem(runtimeState);
        if (itemView == null)
        {
            return;
        }

        if (!slot.TryEquip(itemView))
        {
            UnityEngine.Object.Destroy(itemView.gameObject);
        }
    }

    private void RestoreLooseBackpackGridSnapshot(CharacterInventorySnapshot snapshot)
    {
        if (snapshot == null || BackpackGrid == null)
        {
            return;
        }

        BackpackGrid.LoadFromRuntimeState(
            CloneSaveDataList(snapshot.BackpackLooseItems),
            CloneCellStateList(snapshot.BackpackLooseCellStates));
    }

    private static void ClearEquipmentSlot(EquipmentSlotUI slot)
    {
        if (slot == null)
        {
            return;
        }

        DraggableItemUI releasedItem = slot.ReleaseEquippedItem();
        if (releasedItem != null)
        {
            UnityEngine.Object.Destroy(releasedItem.gameObject);
        }
    }

    private static void EndCurrentDragIfNeeded()
    {
        DraggableItemUI draggedItem = DraggableItemUI.CurrentlyDraggedItem;
        if (draggedItem == null)
        {
            return;
        }

        draggedItem.BounceBack();
        draggedItem.ForceEndDrag();
    }

    private static bool HasRuntimeItemViews(InventoryUIController grid)
    {
        if (grid == null || grid.ItemContainer == null)
        {
            return false;
        }

        foreach (Transform child in grid.ItemContainer)
        {
            if (child == grid.Highlighter)
            {
                continue;
            }

            if (child.GetComponent<DraggableItemUI>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetSavedItemsCarryWeight(List<ContainerItemSaveData> items)
    {
        if (items == null)
        {
            return 0f;
        }

        float carryWeight = 0f;
        foreach (ContainerItemSaveData item in items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            int amount = Mathf.Max(1, item.Amount);
            carryWeight += Mathf.Max(0f, item.ItemData.CarryWeight) * amount;
            carryWeight += GetSavedItemsCarryWeight(item.InternalItems);
        }

        return carryWeight;
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

    private void EnsureDefaultBackpackEquipped()
    {
        if (BackpackSlot == null || DefaultBackpackItem == null)
        {
            return;
        }

        BackpackSlot.InitializeRuntimeState(false);

        if (BackpackSlot.HasEquippedItem)
        {
            return;
        }

        if (BackpackGrid != null && HasRuntimeItemViews(BackpackGrid))
        {
            return;
        }

        if (InventoryItemFactory.Instance == null)
        {
            return;
        }

        DraggableItemUI defaultBackpack = InventoryItemFactory.Instance.CreateFloatingItem(
            DefaultBackpackItem,
            1,
            null,
            null);

        if (defaultBackpack == null)
        {
            return;
        }

        if (!BackpackSlot.TryEquip(defaultBackpack))
        {
            Destroy(defaultBackpack.gameObject);
        }
    }

    private sealed class CharacterInventorySnapshot
    {
        public ContainerItemSaveData BackpackItem;
        public List<ContainerItemSaveData> BackpackLooseItems;
        public List<ContainerCellStateSaveData> BackpackLooseCellStates;
        public ContainerItemSaveData RigItem;
        public ContainerItemSaveData HeadItem;
        public ContainerItemSaveData BodyItem;
        public ContainerItemSaveData FaceItem;
        public ContainerItemSaveData HeadphoneItem;
        public ContainerItemSaveData TotemAItem;
        public ContainerItemSaveData TotemBItem;
    }

    private static bool HasLooseBackpackSnapshot(CharacterInventorySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        bool hasItems = snapshot.BackpackLooseItems != null && snapshot.BackpackLooseItems.Count > 0;
        bool hasCellStates = snapshot.BackpackLooseCellStates != null && snapshot.BackpackLooseCellStates.Count > 0;
        return hasItems || hasCellStates;
    }

    // 根据界面开关状态决定是否展示背包和胸挂的联动内部网格
    private void RefreshCharacterContainerState(bool showLinkedGrids)
    {
        EnsureDefaultBackpackEquipped();

        BackpackSlot?.InitializeRuntimeState(false);

        if (BackpackGrid != null)
        {
            BackpackGrid.gameObject.SetActive(showLinkedGrids);
        }

        if (RigSlot != null)
        {
            RigSlot.SetLinkedGridVisible(false);
            RigSlot.gameObject.SetActive(false);
        }

        if (PocketGrid != null)
        {
            PocketGrid.gameObject.SetActive(false);
        }

        if (TacticalRigGrid != null)
        {
            TacticalRigGrid.gameObject.SetActive(false);
        }
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
        EquipmentSlotUI[] slots =
{
    HeadSlot,
    BodySlot,
    FaceSlot,
    HeadphoneSlot,
    TotemSlotA,
    TotemSlotB
};

        for (int i = 0; i < slots.Length; i++)
        {
            if (IsScreenPointInsideSlot(slots[i], screenPosition, eventCamera))
            {
                return slots[i];
            }
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
        if (slot == null || !slot.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform slotRect = slot.transform as RectTransform;
        return slotRect != null &&
               RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPosition, eventCamera);
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
