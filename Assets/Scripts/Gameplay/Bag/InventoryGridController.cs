using UnityEngine;
using System.Collections.Generic; // 必须引入这个以使用 HashSe
/// <summary>
/// 背包控制器
/// </summary>
public class InventoryGridController : MonoBehaviour
{
    [Header("背包配置")]
    public int Columns = 10;//列
    public int Rows = 5;//行

    // 【新增】：配置哪些坐标是“死区”（不让放东西的空隙）[Header("空间分割 (死区坐标)")]
    public List<Vector2Int> BlockedCells = new List<Vector2Int>();


    // 核心底层数据：二维数组
    public GridCellData[,] _grid;

    // 【核心修复 1】：将初始化独立为一个公开的方法，并加上防重复判定
    public void InitializeGridIfNeeded()
    {
        if (_grid == null)
        {
            _grid = new GridCellData[Columns, Rows];
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    _grid[x, y] = new GridCellData(x, y);
                }
            }

            // 【核心逻辑】：强行封死死区！
            foreach (var pos in BlockedCells)
            {
                if (pos.x >= 0 && pos.x < Columns && pos.y >= 0 && pos.y < Rows)
                {
                    _grid[pos.x, pos.y].State = GridState.Blocked;
                }
            }
        }
    }

    void Awake()
    {
        InitializeGridIfNeeded();
    }
    //初始化二维数组
    private void InitializeGrid()
    {
        _grid = new GridCellData[Columns, Rows];
        for (int x = 0; x < Columns; x++)
        {
            for (int y = 0; y < Rows; y++)
            {
                _grid[x, y] = new GridCellData(x, y);//遍历实例化
            }
        }
    }


    // 基础空间检测（只看指定的长宽是否越界或被占用）   都是空的就能直接放下
    /// <summary>
    /// 纯粹的裁判机制：不论你是怎么转的，我只查指定的长宽有没有越界、有没有撞车
    /// </summary>
    // 【核心修复 2】：在查空间前，强行唤醒一次！
    public bool IsSpaceAvailable(int startX, int startY, int width, int height)
    {
        InitializeGridIfNeeded(); // 就算面板隐藏，我强行把数组 new 出来！

        if (startX < 0 || startY < 0 || startX + width > Columns || startY + height > Rows)
            return false;

        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                if (_grid[x, y].State != GridState.Empty)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 将物品真实写入底层数据   将物品移入
    /// </summary>
    public void PlaceItem(DraggableItemUI itemUI, int startX, int startY, bool isRotated)
    {
        InitializeGridIfNeeded();

        int actualW = isRotated ? itemUI.ItemData.Height : itemUI.ItemData.Width;
        int actualH = isRotated ? itemUI.ItemData.Width : itemUI.ItemData.Height;

        for (int x = startX; x < startX + actualW; x++)
        {
            for (int y = startY; y < startY + actualH; y++)
            {
                _grid[x, y].State = GridState.Occupied_Item;
                _grid[x, y].OccupyingUI = itemUI;
                _grid[x, y].IsItemRotated = isRotated;
            }
        }
    }


    public void RemoveItem(DraggableItemUI itemUI, int startX, int startY, bool isRotated)
    {
        InitializeGridIfNeeded();

        int actualW = isRotated ? itemUI.ItemData.Height : itemUI.ItemData.Width;
        int actualH = isRotated ? itemUI.ItemData.Width : itemUI.ItemData.Height;

        for (int x = startX; x < startX + actualW; x++)
            for (int y = startY; y < startY + actualH; y++)
                if (x >= 0 && x < Columns && y >= 0 && y < Rows)
                    _grid[x, y].Clear();
    }
    // --- 【置换法则核心 1】：扫描指定区域内，究竟被哪些物品挡住了？ ---
    public HashSet<DraggableItemUI> GetItemsInArea(int startX, int startY, int width, int height)
    {
        HashSet<DraggableItemUI> foundItems = new HashSet<DraggableItemUI>();
        int endX = Mathf.Min(startX + width, Columns);
        int endY = Mathf.Min(startY + height, Rows);

        for (int x = Mathf.Max(0, startX); x < endX; x++)
        {
            for (int y = Mathf.Max(0, startY); y < endY; y++)
            {
                if (_grid[x, y].State == GridState.Occupied_Item && _grid[x, y].OccupyingUI != null)
                {
                    foundItems.Add(_grid[x, y].OccupyingUI); // 使用 HashSet 保证同一把枪即使占了 8 格，也只被记录一次
                }
            }
        }
        return foundItems;
    }

    // --- 【置换法则核心 2】：全局寻找第一个能塞下的空位 (也是一键整理的底层逻辑) ---
    public bool FindFirstAvailableSpace(int width, int height, out Vector2Int foundPos, out bool needsRotation)
    {
        // 1. 先尝试不旋转寻找
        for (int y = 0; y <= Rows - height; y++)
        {
            for (int x = 0; x <= Columns - width; x++)
            {
                if (IsSpaceAvailable(x, y, width, height))
                {
                    foundPos = new Vector2Int(x, y); needsRotation = false; return true;
                }
            }
        }
        // 2. 再尝试旋转 90 度寻找
        if (width != height)
        {
            for (int y = 0; y <= Rows - width; y++)
            {
                for (int x = 0; x <= Columns - height; x++)
                {
                    if (IsSpaceAvailable(x, y, height, width))
                    {
                        foundPos = new Vector2Int(x, y); needsRotation = true; return true;
                    }
                }
            }
        }
        foundPos = Vector2Int.zero; needsRotation = false; return false;
    }

    /// <summary>
    /// 【新增算法】：以指定坐标为中心，向四周一圈一圈扩散寻找最近的空位！
    /// </summary>
    public bool FindSpaceAround(int startX, int startY, int width, int height, out Vector2Int foundPos, out bool needsRot)
    {
        int maxRadius = Mathf.Max(Columns, Rows); // 最大搜索半径

        // 从距离为 1 的那一圈开始往外搜
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int x = startX - r; x <= startX + r; x++)
            {
                for (int y = startY - r; y <= startY + r; y++)
                {
                    // 仅检测这一圈的边缘（边框），防止重复搜索内部
                    if (x == startX - r || x == startX + r || y == startY - r || y == startY + r)
                    {
                        // 尝试 1：默认方向能不能放下？
                        if (IsSpaceAvailable(x, y, width, height))
                        {
                            foundPos = new Vector2Int(x, y);
                            needsRot = false;
                            return true;
                        }
                        // 尝试 2：旋转 90 度能不能放下？
                        if (width != height && IsSpaceAvailable(x, y, height, width))
                        {
                            foundPos = new Vector2Int(x, y);
                            needsRot = true;
                            return true;
                        }
                    }
                }
            }
        }

        // 如果四周全都被围死了，降级为【全局寻找】（去犄角旮旯找个空位）
        return FindFirstAvailableSpace(width, height, out foundPos, out needsRot);
    }
}