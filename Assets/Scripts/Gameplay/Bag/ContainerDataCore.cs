using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 【纯数据结构】：用于 100% 持久化保存容器内的物品状态
// =========================================================
[System.Serializable]
public class ContainerItemSaveData
{
    public InventoryItemData ItemData; // 物品种类
    public int Amount;                 // 叠加数量
    public int X;                      // 在网格中的 X 坐标
    public int Y;                      // 在网格中的 Y 坐标
    public bool IsRotated;             // 是否旋转
}

// =========================================================
// 【多态接口】：PRD中点名要求的通用容器接口
// 不管是地上的宝箱、尸体、还是丢在地上的背包，只要实现了它，就能和UI通信！
// =========================================================
public interface IInteractableContainer
{
    string GetContainerName(); // 获取容器名字 (比如 "军用战利品箱")

    // 获取这个 3D 实体内部保存的所有物品数据
    List<ContainerItemSaveData> GetSavedItems();

    // UI 关闭时，把最新的物品状态保存回这个 3D 实体里！
    void SaveItems(List<ContainerItemSaveData> items);
}