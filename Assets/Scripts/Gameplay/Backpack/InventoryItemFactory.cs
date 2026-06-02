using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 物品视图工厂
/// 负责创建 DraggableItemUI，并把静态配置与运行时数据装配到 view 上
/// </summary>
public class InventoryItemFactory : MonoBehaviour
{
    public static InventoryItemFactory Instance { get; private set; }

    [Header("Factory References")]
    public GameObject DraggableItemPrefab;
    public Transform GlobalDragLayer;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 在指定网格中生成一个物品视图
    /// </summary>
    /// <param name="itemData">静态物品配置</param>
    /// <param name="targetGrid">要生成到的目标网格</param>
    /// <param name="startX">起始横坐标</param>
    /// <param name="startY">起始纵坐标</param>
    /// <param name="amount">生成数量</param>
    /// <param name="isRotated">是否以旋转后的占格生成</param>
    /// <param name="internalItems">容器类物品的内部物品快照</param>
    /// <param name="internalCellStates">容器类物品的内部格子状态快照</param>
    /// <returns>生成出的物品视图，失败则返回空</returns>
    public DraggableItemUI SpawnItemInGrid(
        InventoryItemData itemData,
        InventoryUIController targetGrid,
        int startX,
        int startY,
        int amount,
        bool isRotated = false,
        List<ContainerItemSaveData> internalItems = null,
        List<ContainerCellStateSaveData> internalCellStates = null)
    {
        if (itemData == null || targetGrid == null)
        {
            return null;
        }

        InventoryItemRuntimeState runtimeState = InventoryItemRuntimeState.Create(
            itemData,
            amount,
            internalItems,
            internalCellStates);
        return SpawnItemInGrid(runtimeState, targetGrid, startX, startY, isRotated);
    }

    /// <summary>
    /// 在指定网格中用已有运行时状态生成一个物品视图
    /// </summary>
    /// <param name="runtimeState">物品运行时状态</param>
    /// <param name="targetGrid">要生成到的目标网格</param>
    /// <param name="startX">起始横坐标</param>
    /// <param name="startY">起始纵坐标</param>
    /// <param name="isRotated">是否以旋转后的占格生成</param>
    /// <returns>生成出的物品视图，失败则返回空</returns>
    public DraggableItemUI SpawnItemInGrid(
        InventoryItemRuntimeState runtimeState,
        InventoryUIController targetGrid,
        int startX,
        int startY,
        bool isRotated = false)
    {
        if (runtimeState == null || runtimeState.ItemData == null || targetGrid == null)
        {
            return null;
        }

        InventoryItemData itemData = runtimeState.ItemData;
        int width = isRotated ? itemData.Height : itemData.Width;
        int height = isRotated ? itemData.Width : itemData.Height;
        if (!targetGrid.GetGridController().IsSpaceAvailable(startX, startY, width, height))
        {
            return null;
        }

        GameObject itemObject = CreateItemObject(targetGrid.ItemContainer);
        if (itemObject == null)
        {
            return null;
        }

        DraggableItemUI itemView = itemObject.GetComponent<DraggableItemUI>();
        if (itemView == null)
        {
            Destroy(itemObject);
            return null;
        }

        ConfigureItemView(itemView, runtimeState, targetGrid);
        itemView.InitializeItem(itemData, new Vector2Int(startX, startY), isRotated);
        targetGrid.GetGridController().PlaceItem(itemView, startX, startY, isRotated);
        return itemView;
    }

    /// <summary>
    /// 创建一个未附着在网格上的悬浮物品视图
    /// </summary>
    /// <param name="itemData">静态物品配置</param>
    /// <param name="amount">生成数量</param>
    /// <param name="internalItems">容器类物品的内部物品快照</param>
    /// <param name="internalCellStates">容器类物品的内部格子状态快照</param>
    /// <returns>生成出的悬浮物品视图，失败则返回空</returns>
    public DraggableItemUI CreateFloatingItem(
        InventoryItemData itemData,
        int amount,
        List<ContainerItemSaveData> internalItems = null,
        List<ContainerCellStateSaveData> internalCellStates = null)
    {
        if (itemData == null || GlobalDragLayer == null)
        {
            return null;
        }

        GameObject itemObject = CreateItemObject(GlobalDragLayer);
        if (itemObject == null)
        {
            return null;
        }

        DraggableItemUI itemView = itemObject.GetComponent<DraggableItemUI>();
        if (itemView == null)
        {
            Destroy(itemObject);
            return null;
        }

        ConfigureItemView(
            itemView,
            InventoryItemRuntimeState.Create(itemData, amount, internalItems, internalCellStates),
            null);

        Image image = itemView.GetComponent<Image>();
        if (image != null && itemData.ItemIcon != null)
        {
            image.sprite = itemData.ItemIcon;
        }

        itemView.UpdateAmountText();
        itemView.GetComponent<RectTransform>().sizeDelta = new Vector2(
            itemData.Width * 50 + (itemData.Width - 1) * 2,
            itemData.Height * 50 + (itemData.Height - 1) * 2);

        return itemView;
    }

    // 把静态配置和运行时快照统一灌入物品视图，避免生成入口分散赋值
    private static void ConfigureItemView(
        DraggableItemUI itemView,
        InventoryItemRuntimeState runtimeState,
        InventoryUIController targetGrid)
    {
        InventoryItemData itemData = runtimeState != null ? runtimeState.ItemData : null;
        if (itemData == null)
        {
            return;
        }

        itemView.name = itemData.ItemName;
        itemView.IsDebugItem = false;
        itemView.CurrentGrid = targetGrid;
        itemView.BindRuntimeState(runtimeState);
    }

    // 创建物品 GameObject；如果没有配置 prefab，则构建最小可用的运行时视图
    private GameObject CreateItemObject(Transform parent)
    {
        if (DraggableItemPrefab != null)
        {
            return Instantiate(DraggableItemPrefab, parent, false);
        }

        GameObject itemObject = new GameObject("RuntimeItem", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(DraggableItemUI));
        itemObject.transform.SetParent(parent, false);

        GameObject amountObject = new GameObject("AmountText", typeof(RectTransform), typeof(Text));
        amountObject.transform.SetParent(itemObject.transform, false);

        RectTransform amountRect = amountObject.GetComponent<RectTransform>();
        amountRect.anchorMin = Vector2.zero;
        amountRect.anchorMax = Vector2.one;
        amountRect.offsetMin = Vector2.zero;
        amountRect.offsetMax = Vector2.zero;

        Text amountText = amountObject.GetComponent<Text>();
        amountText.alignment = TextAnchor.LowerRight;
        amountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        amountText.fontSize = 14;
        amountText.color = Color.white;
        amountText.raycastTarget = false;

        DraggableItemUI itemView = itemObject.GetComponent<DraggableItemUI>();
        itemView.AmountText = amountText;
        return itemObject;
    }

}
