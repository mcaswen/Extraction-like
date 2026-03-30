using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{   


    [Header("UI 核心引用")]
    public RectTransform ItemContainer;
    public Transform GridBackground; // 【新增】：拖入你的 GridBackground 节点

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


    void Start()
    {
        // 游戏开始时，自动隐藏死区的背景格子，制造视觉上的“分割”错觉
        if (GridBackground != null)
        {
            InventoryGridController gridCtrl = GetGridController();
            foreach (var pos in gridCtrl.BlockedCells)
            {
                // 计算当前坐标在 GridLayoutGroup 里的子物体索引 (从左到右，从上到下)
                int childIndex = pos.y * gridCtrl.Columns + pos.x;
                if (childIndex >= 0 && childIndex < GridBackground.childCount)
                {
                    Image cellImage = GridBackground.GetChild(childIndex).GetComponent<Image>();
                    if (cellImage != null)
                    {
                        cellImage.enabled = false; // 关掉图片显示，但保留物体以维持 Layout 排版！
                    }
                }
            }
        }
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

        // 【核心修复】：将 SetAsLastSibling 改为 SetAsFirstSibling
        // 这会让绿框在层级里排到最顶端，即渲染在最底层
        Highlighter.transform.SetAsFirstSibling();

        Highlighter.anchoredPosition = GetLocalPosition(x, y);
        Highlighter.sizeDelta = GetItemActualSize(width, height);

        if (_highlighterImage == null) _highlighterImage = Highlighter.GetComponent<Image>();
        if (_highlighterImage != null)
        {
            // 建议把 Alpha (第四个参数) 调低一点，比如 0.3f 或 0.4f
            // 这样既能看到绿色，又不会觉得刺眼，还能透出底部的网格线
            _highlighterImage.color = isValid ? new Color(0, 1f, 0, 0.35f) : new Color(1f, 0, 0, 0.35f);
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
                    // 【核心修复】：千万不要清除死区！
                    if (GetGridController()._grid[x, y].State != GridState.Blocked)
                    {
                        GetGridController()._grid[x, y].Clear();
                    }
                }
            }
        }
    }

    // =========================================================
    // 【PRD 核心：一键整理终极算法 (One-Click Auto Sort)】
    // =========================================================
    public void AutoSort()
    {
        // 1. 获取当前背包内所有物品的纯数据
        List<ContainerItemSaveData> allItems = ExtractSaveData();
        if (allItems.Count == 0) return; // 空背包直接返回

        // ==========================================
        // Step 1: 全局强制合并 (Global Auto-Merge)
        // ==========================================
        Dictionary<InventoryItemData, int> mergedStackables = new Dictionary<InventoryItemData, int>();
        List<ContainerItemSaveData> nonStackables = new List<ContainerItemSaveData>();

        foreach (var item in allItems)
        {
            if (item.ItemData.IsStackable)
            {
                // 如果是子弹、医疗包等可堆叠物，把它们的数量全部提取出来相加！
                if (mergedStackables.ContainsKey(item.ItemData))
                    mergedStackables[item.ItemData] += item.Amount;
                else
                    mergedStackables[item.ItemData] = item.Amount;
            }
            else
            {
                // 枪械、背包等不可堆叠物，直接单独存放
                nonStackables.Add(item);
            }
        }

        // 重新切分合并后的物品（比如一共 140 发子弹，切分成 60 + 60 + 20）
        List<ContainerItemSaveData> itemsToPlace = new List<ContainerItemSaveData>();
        itemsToPlace.AddRange(nonStackables);

        foreach (var kvp in mergedStackables)
        {
            int remainingAmount = kvp.Value;
            int maxStack = kvp.Key.MaxStack;
            while (remainingAmount > 0)
            {
                int amountToCreate = Mathf.Min(remainingAmount, maxStack);
                itemsToPlace.Add(new ContainerItemSaveData { ItemData = kvp.Key, Amount = amountToCreate });
                remainingAmount -= amountToCreate;
            }
        }

        // ==========================================
        // Step 2 & 3: 面积装箱排序 与 类型次级排序
        // ==========================================
        itemsToPlace.Sort((a, b) =>
        {
            // 首要权重：计算面积 (宽 * 高)
            int areaA = a.ItemData.Width * a.ItemData.Height;
            int areaB = b.ItemData.Width * b.ItemData.Height;

            if (areaA != areaB)
            {
                return areaB.CompareTo(areaA); // 面积大的排在前面（优先霸占左上角）
            }

            // 次级权重：如果面积一样大，按物品类别 (ItemType) 排序，保证同类挨在一起
            return a.ItemData.Type.CompareTo(b.ItemData.Type);
        });

        // ==========================================
        // 执行整理：清空全场，按最优解重新发牌！
        // ==========================================
        ClearUI(); // 瞬间抹除当前所有 UI 和网格记录 (Highlighter 会被安全保留)

        foreach (var item in itemsToPlace)
        {
            // 利用底层大脑的寻找空位功能 (包含自动旋转预测)
            if (GetGridController().FindFirstAvailableSpace(item.ItemData.Width, item.ItemData.Height, out Vector2Int pos, out bool needsRot))
            {
                // 让工厂在这个算好的最佳空位上，重新生成 UI 实体
                InventoryItemFactory.Instance.SpawnItemInGrid(item.ItemData, this, pos.x, pos.y, item.Amount, needsRot);
            }
            else
            {
                // 极限情况：由于空间碎片化被消除，整理后空间绝对只会变大不会变小，所以理论上绝对不可能放不下。
                Debug.LogError($"一键整理异常：物品 {item.ItemData.ItemName} 无法放入！");
            }
        }

        Debug.Log($"[{gameObject.name}] 一键整理完成！");
    }
}