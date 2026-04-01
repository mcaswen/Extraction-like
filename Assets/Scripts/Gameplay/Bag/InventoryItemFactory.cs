using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 物品视图工厂。
/// 负责创建 DraggableItemUI，并把静态配置与运行时数据装配到 view 上。
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
    /// 在指定网格中生成一个物品视图。
    /// </summary>
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

        ConfigureItemView(itemView, itemData, amount, targetGrid, internalItems, internalCellStates);
        itemView.InitializeItem(itemData, new Vector2Int(startX, startY), isRotated);
        targetGrid.GetGridController().PlaceItem(itemView, startX, startY, isRotated);
        return itemView;
    }

    /// <summary>
    /// 创建一个未附着在网格上的悬浮物品视图。
    /// </summary>
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

        ConfigureItemView(itemView, itemData, amount, null, internalItems, internalCellStates);

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

    private static void ConfigureItemView(
        DraggableItemUI itemView,
        InventoryItemData itemData,
        int amount,
        InventoryUIController targetGrid,
        List<ContainerItemSaveData> internalItems,
        List<ContainerCellStateSaveData> internalCellStates)
    {
        itemView.name = itemData.ItemName;
        itemView.IsDebugItem = false;
        itemView.CurrentGrid = targetGrid;
        itemView.CurrentAmount = amount;
        itemView.ItemData = itemData;
        itemView.InternalItems = CloneSaveDataList(internalItems);
        itemView.InternalCellStates = CloneCellStateList(internalCellStates);
    }

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
