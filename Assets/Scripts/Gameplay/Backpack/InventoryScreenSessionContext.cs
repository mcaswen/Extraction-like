using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包界面的一次运行时会话配置
/// 支持复用现有角色背包布局，或临时接管为自定义玩家格子 + 外部容器格子
/// </summary>
public sealed class InventoryScreenSessionContext
{
    public string DisplayName { get; set; } = string.Empty;
    public bool UseCustomPlayerInventory { get; set; }
    public int PlayerColumns { get; set; } = 1;
    public int PlayerRows { get; set; } = 1;
    public List<Vector2Int> PlayerBlockedCells { get; set; } = new List<Vector2Int>();
    public List<ContainerItemSaveData> PlayerItems { get; set; } = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> PlayerCellStates { get; set; } = new List<ContainerCellStateSaveData>();

    public string ExternalContainerName { get; set; } = string.Empty;
    public InventoryExternalContainerKind ExternalContainerKind { get; set; } = InventoryExternalContainerKind.Loot;
    public int ExternalColumns { get; set; } = 1;
    public int ExternalRows { get; set; } = 1;
    public List<Vector2Int> ExternalBlockedCells { get; set; } = new List<Vector2Int>();
    public List<ContainerItemSaveData> ExternalItems { get; set; } = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> ExternalCellStates { get; set; } = new List<ContainerCellStateSaveData>();

    public Action BeforeOpen { get; set; }
    public Action<InventoryScreenSessionResult> OnClose { get; set; }
}

public enum InventoryExternalContainerKind
{
    Loot,
    Storage
}

/// <summary>
/// 背包界面关闭时导出的会话结果
/// </summary>
public sealed class InventoryScreenSessionResult
{
    public List<ContainerItemSaveData> PlayerItems { get; set; } = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> PlayerCellStates { get; set; } = new List<ContainerCellStateSaveData>();
    public List<ContainerItemSaveData> ExternalItems { get; set; } = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> ExternalCellStates { get; set; } = new List<ContainerCellStateSaveData>();
}
