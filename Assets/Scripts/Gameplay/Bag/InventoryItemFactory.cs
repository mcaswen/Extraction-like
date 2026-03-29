using UnityEngine;

public class InventoryItemFactory : MonoBehaviour
{
    public static InventoryItemFactory Instance { get; private set; }

    [Header("全局预制体配置")]
    public GameObject DraggableItemPrefab; // 拖入你之前做好的挂载了 DraggableItemUI 的预制体
    public Transform GlobalDragLayer;      // 【核心】：新建一个Canvas下的空物体，用于拖拽时悬浮，防止被其他UI遮挡

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 【物品工厂 API】：在指定的容器中生成一个物品
    /// </summary>
    public DraggableItemUI SpawnItemInGrid(InventoryItemData itemData, InventoryUIController targetGrid, int startX, int startY, int amount, bool isRotated = false)
    {
        int w = isRotated ? itemData.Height : itemData.Width;
        int h = isRotated ? itemData.Width : itemData.Height;

            // 1. 数据层安全校验：这个容器的这个位置能放下吗？
            if (targetGrid.GetGridController().IsSpaceAvailable(startX, startY, w, h))
            {
                // 2. 实例化 UI 预制体，放置到目标容器的 ItemContainer 下
                GameObject obj = Instantiate(DraggableItemPrefab, targetGrid.ItemContainer, false);
                DraggableItemUI itemUI = obj.GetComponent<DraggableItemUI>();

                itemUI.name = itemData.ItemName;
                itemUI.IsDebugItem = false; // 关闭手动 Debug 模式
                itemUI.CurrentGrid = targetGrid; // 认主：记录自己属于哪个容器
                itemUI.CurrentAmount = amount;

                // 3. 初始化并写入底层数据
                itemUI.InitializeItem(itemData, new Vector2Int(startX, startY), isRotated);
                targetGrid.GetGridController().PlaceItem(itemUI, startX, startY, isRotated);

                return itemUI;
            }
        
        else { Debug.Log("没有TargetGrid物体"); }

            Debug.LogWarning($"工厂生成失败：容器 {targetGrid.name} 的 [{startX},{startY}] 位置空间不足！");
        return null;
    }
}