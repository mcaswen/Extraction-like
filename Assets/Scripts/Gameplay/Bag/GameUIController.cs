using System.Collections.Generic;
using UnityEngine;

public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    public bool IsInventoryOpen { get; set; }

    // 缓存当前正在交互的 3D 宝箱实体
    public LootBoxEntity CurrentLootBox { get; private set; }

    [Header("UI 面板引用")]
    public GameObject InventoryPanel;       // 整个大背包的父节点
    public InventoryUIController BackpackGrid;  // 大背包的网格控制器
    public InventoryUIController LootChestGrid; // 宝箱的网格控制器


    void Awake()
    {
        Instance = this;
        InventoryPanel.SetActive(false);

        // 【修复 1】：游戏刚开始时，强制隐藏宝箱面板！平时按 Tab 只看自己的大背包
        if (LootChestGrid != null)
        {
            LootChestGrid.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }


    // =========================================================
    // 【新增 API】：供玩家按 F 键交互时调用，专门用来打开宝箱
    // =========================================================
    public void OpenLootBox(LootBoxEntity lootBox)
    {
        CurrentLootBox = lootBox;
        LootChestGrid.gameObject.SetActive(true);

        // 【新增】：确保宝箱里的物品生成后，每一个物品都明确知道自己属于这个宝箱
        LootChestGrid.LoadFromData(lootBox.GetSavedItems());

        // 这一步非常重要：如果是刚激活的面板，确保它处于可以接收射线的层级
        LootChestGrid.transform.SetAsLastSibling();

        if (!IsInventoryOpen)
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        IsInventoryOpen = !IsInventoryOpen;

        // 如果是【关闭背包】的操作
        if (!IsInventoryOpen)
        {
            // 1. 强中断保护：把悬在空中的物品弹回老家
            if (DraggableItemUI.CurrentlyDraggedItem != null)
            {
                DraggableItemUI.CurrentlyDraggedItem.BounceBack();
                DraggableItemUI.CurrentlyDraggedItem.ForceEndDrag();
            }

            // 2. 【修复 2 终极保存】：如果当前开着宝箱，必须把数据写回去！
            if (CurrentLootBox != null)
            {
                // 从 UI 提取出最新的纯数据
                List<ContainerItemSaveData> newData = LootChestGrid.ExtractSaveData();

                // 狠狠地塞进 3D 宝箱实体的内存里！(保存成功！)
                CurrentLootBox.SaveItems(newData);

                // 清空 UI，防止下一个宝箱打开时看到上一个宝箱的残影
                LootChestGrid.ClearUI();

                // 隐藏宝箱面板，解除绑定
                LootChestGrid.gameObject.SetActive(false);
                CurrentLootBox = null;
            }
        }

        InventoryPanel.SetActive(IsInventoryOpen);
        Cursor.lockState = IsInventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = IsInventoryOpen;
    }

    // =========================================================
    // 【PRD 核心：快捷转移路由 (Quick Transfer Routing)】
    // =========================================================
    public InventoryUIController GetQuickTransferTarget(InventoryUIController sourceGrid)
    {
        if (sourceGrid == LootChestGrid) return BackpackGrid;
        if (sourceGrid == BackpackGrid && CurrentLootBox != null) return LootChestGrid; // 只有开着宝箱时，背包的东西才能快捷转移给宝箱
        return null;
    }
}