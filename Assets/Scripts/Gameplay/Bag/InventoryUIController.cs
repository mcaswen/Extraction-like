using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{   

    [Header("UI 核心引用")]
    public RectTransform ItemContainer;
    public float CellSize = 50f;
    public float Spacing = 2f; // 【核心修复1】：加入缝隙计算，必须与 GridBackground 的 Spacing 一致

    [Header("预测高亮框 (需求新增)")]
    public RectTransform Highlighter; // 拖入一个新建的半透明 Image
    private Image _highlighterImage;

    private InventoryGridController _gridController;

    void Awake()
    {
        if (Highlighter != null)
        {
            _highlighterImage = Highlighter.GetComponent<Image>();
            Highlighter.gameObject.SetActive(false);
            Highlighter.anchorMin = new Vector2(0, 1);
            Highlighter.anchorMax = new Vector2(0, 1);
            Highlighter.pivot = new Vector2(0, 1);
        }


        _gridController = GetComponent<InventoryGridController>();
    }


    // 将二维数组坐标转换为 UI 局部坐标 (加入 Spacing)
    public Vector2 GetLocalPosition(int x, int y)
    {
        float posX = x * (CellSize + Spacing);
        float posY = -y * (CellSize + Spacing);
        return new Vector2(posX, posY);
    }

    // 将鼠标局部坐标转换回二维数组索引 (加入 Spacing)
    public Vector2Int GetGridIndex(Vector2 localPosition)
    {
        int x = Mathf.FloorToInt(localPosition.x / (CellSize + Spacing));
        int y = Mathf.FloorToInt(-localPosition.y / (CellSize + Spacing));
        return new Vector2Int(x, y);
    }

    // 计算物品占用 UI 的实际像素大小 (包含内部的缝隙)
    public Vector2 GetItemActualSize(int width, int height)
    {
        float w = width * CellSize + (width - 1) * Spacing;
        float h = height * CellSize + (height - 1) * Spacing;
        return new Vector2(w, h);
    }

    // 控制高亮框显示
    public void ShowHighlight(int x, int y, int width, int height, bool isValid)
    {
        if (Highlighter == null) return;

        Highlighter.gameObject.SetActive(true);

        // 【重要】：每当显示高亮框时，强制它在 ItemContainer 里的层级排到最后
        // 在 UI 渲染规则里，层级越靠后，显示越靠前（不会被物品遮挡）
        Highlighter.SetAsLastSibling();

        Highlighter.anchoredPosition = GetLocalPosition(x, y);
        Highlighter.sizeDelta = GetItemActualSize(width, height);

        if (_highlighterImage == null) _highlighterImage = Highlighter.GetComponent<Image>();
        if (_highlighterImage != null)
        {
            _highlighterImage.color = isValid ? new Color(0, 1f, 0, 0.4f) : new Color(1f, 0, 0, 0.4f);
        }
    }

    public void HideHighlight()
    {
        if (Highlighter != null) Highlighter.gameObject.SetActive(false);
    }

    public InventoryGridController GetGridController()
    {
        if (_gridController == null)
        {
            _gridController = GetComponent<InventoryGridController>();
        }
        return _gridController;
    }

    // =========================================================
    // 【MVC 架构】：将底层数据转化为 UI 表现 (Load)
    // =========================================================
    public void LoadFromData(List<ContainerItemSaveData> saveDataList)
    {
        ClearUI(); // 先清空当前 UI 面板里的所有旧东西

        // 遍历传入的数据，让工厂把它们全部作为 UI 实体刷出来！
        foreach (var data in saveDataList)
        {
            if (data.ItemData != null)
            {
                InventoryItemFactory.Instance.SpawnItemInGrid(
                    data.ItemData, this, data.X, data.Y, data.Amount, data.IsRotated);
            }
        }
    }

    // =========================================================
    // 【MVC 架构】：将当前的 UI 表现打包成纯数据 (Save)
    // =========================================================
    public List<ContainerItemSaveData> ExtractSaveData()
    {
        List<ContainerItemSaveData> saveDataList = new List<ContainerItemSaveData>();

        // 遍历整个 ItemContainer 下所有的 UI 物品
        foreach (Transform child in ItemContainer)
        {
            DraggableItemUI itemUI = child.GetComponent<DraggableItemUI>();
            if (itemUI != null && itemUI.ItemData != null)
            {
                // 把它们的状态提取出来，存入纯数据类
                ContainerItemSaveData saveData = new ContainerItemSaveData
                {
                    ItemData = itemUI.ItemData,
                    Amount = itemUI.CurrentAmount,
                    X = itemUI._originalGridIndex.x, // 这里需要把 _originalGridIndex 改为 public 才能访问！(见下方说明)
                    Y = itemUI._originalGridIndex.y,
                    IsRotated = itemUI._originalIsRotated // 这里需要把 _originalIsRotated 改为 public 才能访问！
                };
                saveDataList.Add(saveData);
            }
        }
        return saveDataList;
    }

    // =========================================================
    // 清空 UI 和底层大脑（用于换宝箱时刷新面板）
    // =========================================================
    public void ClearUI()
    {
        // 1. 销毁所有 UI 实体，但要跳过我们的 Highlighter
        foreach (Transform child in ItemContainer)
        {
            // 如果这个子物体就是我们要保护的高亮框，或者是它的脚本，就跳过它不删
            if (child == Highlighter) continue;

            // 另一种双重保险：只删除挂载了 DraggableItemUI 脚本的物体
            if (child.GetComponent<DraggableItemUI>() != null)
            {
                Destroy(child.gameObject);
            }
        }

        // 2. 清空底层二维数组大脑
        if (GetGridController() != null && GetGridController()._grid != null)
        {
            int cols = GetGridController().Columns;
            int rows = GetGridController().Rows;
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    GetGridController()._grid[x, y].Clear();
                }
            }
        }
    }
}