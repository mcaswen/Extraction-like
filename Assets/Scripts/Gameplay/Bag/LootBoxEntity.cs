using System.Collections.Generic;
using UnityEngine;

// 继承 MonoBehaviour，并【实现 IInteractableContainer 接口】
public class LootBoxEntity : MonoBehaviour, IInteractableContainer
{
    [Header("宝箱配置")]
    public string BoxName = "军用物资箱";

    // 用于模拟：首次打开时，箱子里默认有什么？
    public InventoryItemData FirstTimeLootItem;
    public int FirstTimeLootAmount = 30;

    // 【100% 状态持久化】：保存在这个 3D 物体内存中的纯数据！
    [SerializeField]
    private List<ContainerItemSaveData> _savedItems = new List<ContainerItemSaveData>();

    private bool _isFirstTimeOpen = true;

    // 接口实现 1：获取名字
    public string GetContainerName() => BoxName;

    // 接口实现 2：被 UI 索要数据时
    public List<ContainerItemSaveData> GetSavedItems()
    {
        // 【PRD 预计算生成机制】：首次打开时，动态生成战利品数据！
        if (_isFirstTimeOpen && FirstTimeLootItem != null)
        {
            ContainerItemSaveData initialLoot = new ContainerItemSaveData
            {
                ItemData = FirstTimeLootItem,
                Amount = FirstTimeLootAmount,
                X = 0,
                Y = 0,
                IsRotated = false
            };
            _savedItems.Add(initialLoot);
            _isFirstTimeOpen = false;
        }

        return _savedItems;
    }

    // 接口实现 3：被 UI 关闭时，把新数据覆盖保存到这里
    public void SaveItems(List<ContainerItemSaveData> items)
    {
        _savedItems = items;
        Debug.Log($"[{BoxName}] 数据已持久化保存！当前内部物品数量：{_savedItems.Count}");
    }
}