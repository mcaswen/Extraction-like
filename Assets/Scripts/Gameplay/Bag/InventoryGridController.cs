using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包网格控制器。
/// 负责把场景中的配置同步到运行时 grid model，并向外暴露稳定的网格规则接口。
/// </summary>
public class InventoryGridController : MonoBehaviour
{
    [Header("Grid Config")]
    public int Columns = 10;
    public int Rows = 5;

    [Header("Blocked Cells")]
    public List<Vector2Int> BlockedCells = new List<Vector2Int>();

    private readonly InventoryGridModel _model = new InventoryGridModel();

    public GridCellData[,] _grid => _model.Cells;

    private void Awake()
    {
        InitializeGridIfNeeded();
    }

    /// <summary>
    /// 根据当前 Inspector 配置初始化或重建网格模型。
    /// </summary>
    public void InitializeGridIfNeeded(bool force = false)
    {
        _model.Configure(Columns, Rows, BlockedCells, force);
    }

    /// <summary>
    /// 用新的尺寸和阻塞格配置网格。
    /// </summary>
    public void ConfigureGrid(int columns, int rows, List<Vector2Int> blockedCells)
    {
        Columns = columns;
        Rows = rows;
        BlockedCells = blockedCells != null ? new List<Vector2Int>(blockedCells) : new List<Vector2Int>();
        InitializeGridIfNeeded(true);
    }

    /// <summary>
    /// 清空所有可放置格的动态占用信息，保留阻塞格。
    /// </summary>
    public void ClearDynamicCells()
    {
        InitializeGridIfNeeded();
        _model.ClearDynamicCells();
    }

    /// <summary>
    /// 导出当前网格中需要持久化的特殊格状态。
    /// </summary>
    public List<ContainerCellStateSaveData> ExtractRuntimeCellStates()
    {
        InitializeGridIfNeeded();
        return _model.ExtractRuntimeCellStates();
    }

    /// <summary>
    /// 应用运行时特殊格状态快照。
    /// </summary>
    public void ApplyRuntimeCellStates(List<ContainerCellStateSaveData> cellStates)
    {
        InitializeGridIfNeeded();
        _model.ApplyRuntimeCellStates(cellStates);
    }

    /// <summary>
    /// 检查指定区域是否可以放下物品。
    /// </summary>
    public bool IsSpaceAvailable(int startX, int startY, int width, int height)
    {
        InitializeGridIfNeeded();
        return _model.IsSpaceAvailable(startX, startY, width, height);
    }

    /// <summary>
    /// 将物品写入网格占用状态。
    /// </summary>
    public void PlaceItem(DraggableItemUI itemUI, int startX, int startY, bool isRotated)
    {
        InitializeGridIfNeeded();
        GetItemFootprint(itemUI, isRotated, out int width, out int height);
        _model.PlaceItem(itemUI, startX, startY, width, height, isRotated);
    }

    /// <summary>
    /// 从网格中移除物品占用。
    /// </summary>
    public void RemoveItem(DraggableItemUI itemUI, int startX, int startY, bool isRotated)
    {
        InitializeGridIfNeeded();
        GetItemFootprint(itemUI, isRotated, out int width, out int height);
        _model.RemoveItem(startX, startY, width, height);
    }

    /// <summary>
    /// 获取目标区域内所有被挡住的物品视图。
    /// </summary>
    public HashSet<DraggableItemUI> GetItemsInArea(int startX, int startY, int width, int height)
    {
        InitializeGridIfNeeded();

        HashSet<DraggableItemUI> foundItems = new HashSet<DraggableItemUI>();
        foreach (IInventoryItemView itemView in _model.GetItemsInArea(startX, startY, width, height))
        {
            if (itemView is DraggableItemUI itemUI)
            {
                foundItems.Add(itemUI);
            }
        }

        return foundItems;
    }

    /// <summary>
    /// 查找第一个可用空位，必要时自动尝试旋转。
    /// </summary>
    public bool FindFirstAvailableSpace(int width, int height, out Vector2Int foundPos, out bool needsRotation)
    {
        InitializeGridIfNeeded();
        return _model.FindFirstAvailableSpace(width, height, out foundPos, out needsRotation);
    }

    /// <summary>
    /// 从指定坐标开始向外搜索最近空位。
    /// </summary>
    public bool FindSpaceAround(int startX, int startY, int width, int height, out Vector2Int foundPos, out bool needsRotation)
    {
        InitializeGridIfNeeded();
        return _model.FindSpaceAround(startX, startY, width, height, out foundPos, out needsRotation);
    }

    private static void GetItemFootprint(DraggableItemUI itemUI, bool isRotated, out int width, out int height)
    {
        width = isRotated ? itemUI.ItemData.Height : itemUI.ItemData.Width;
        height = isRotated ? itemUI.ItemData.Width : itemUI.ItemData.Height;
    }
}

/// <summary>
/// 背包网格的纯运行时数据模型。
/// 负责格子状态、占用检测和查找可用空间，不负责任何 UI 表现。
/// </summary>
public sealed class InventoryGridModel
{
    private int _columns;
    private int _rows;
    private List<Vector2Int> _blockedCells = new List<Vector2Int>();
    private GridCellData[,] _cells;

    public int Columns => _columns;
    public int Rows => _rows;
    public IReadOnlyList<Vector2Int> BlockedCells => _blockedCells;
    public GridCellData[,] Cells => _cells;

    public void Configure(int columns, int rows, List<Vector2Int> blockedCells, bool forceRebuild)
    {
        _columns = Mathf.Max(1, columns);
        _rows = Mathf.Max(1, rows);
        _blockedCells = blockedCells != null ? new List<Vector2Int>(blockedCells) : new List<Vector2Int>();

        bool needsRebuild =
            forceRebuild ||
            _cells == null ||
            _cells.GetLength(0) != _columns ||
            _cells.GetLength(1) != _rows;

        if (!needsRebuild)
        {
            return;
        }

        _cells = new GridCellData[_columns, _rows];
        for (int x = 0; x < _columns; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                _cells[x, y] = new GridCellData(x, y);
            }
        }

        ApplyBlockedCells();
    }

    public void ClearDynamicCells()
    {
        if (_cells == null)
        {
            return;
        }

        for (int x = 0; x < _columns; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_cells[x, y].State != GridState.Blocked)
                {
                    _cells[x, y].Clear();
                }
            }
        }
    }

    public List<ContainerCellStateSaveData> ExtractRuntimeCellStates()
    {
        List<ContainerCellStateSaveData> runtimeStates = new List<ContainerCellStateSaveData>();
        if (_cells == null)
        {
            return runtimeStates;
        }

        for (int x = 0; x < _columns; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                GridState state = _cells[x, y].State;
                if (state == GridState.Empty || state == GridState.OccupiedItem || state == GridState.Blocked)
                {
                    continue;
                }

                runtimeStates.Add(new ContainerCellStateSaveData
                {
                    X = x,
                    Y = y,
                    State = state
                });
            }
        }

        return runtimeStates;
    }

    public void ApplyRuntimeCellStates(List<ContainerCellStateSaveData> cellStates)
    {
        if (_cells == null || cellStates == null)
        {
            return;
        }

        foreach (ContainerCellStateSaveData cellState in cellStates)
        {
            if (cellState == null || !IsWithinBounds(cellState.X, cellState.Y))
            {
                continue;
            }

            GridCellData cell = _cells[cellState.X, cellState.Y];
            if (cell.State == GridState.Blocked || cell.State == GridState.OccupiedItem)
            {
                continue;
            }

            cell.State = cellState.State;
            cell.OccupyingItemView = null;
            cell.IsItemRotated = false;
        }
    }

    public bool IsSpaceAvailable(int startX, int startY, int width, int height)
    {
        if (_cells == null)
        {
            return false;
        }

        if (startX < 0 || startY < 0)
        {
            return false;
        }

        if (startX + width > _columns || startY + height > _rows)
        {
            return false;
        }

        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                if (_cells[x, y].State != GridState.Empty)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public void PlaceItem(IInventoryItemView itemView, int startX, int startY, int width, int height, bool isRotated)
    {
        if (_cells == null)
        {
            return;
        }

        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                _cells[x, y].State = GridState.OccupiedItem;
                _cells[x, y].OccupyingItemView = itemView;
                _cells[x, y].IsItemRotated = isRotated;
            }
        }
    }

    public void RemoveItem(int startX, int startY, int width, int height)
    {
        if (_cells == null)
        {
            return;
        }

        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                if (!IsWithinBounds(x, y))
                {
                    continue;
                }

                if (_cells[x, y].State != GridState.Blocked)
                {
                    _cells[x, y].Clear();
                }
            }
        }
    }

    public HashSet<IInventoryItemView> GetItemsInArea(int startX, int startY, int width, int height)
    {
        HashSet<IInventoryItemView> foundItems = new HashSet<IInventoryItemView>();
        if (_cells == null)
        {
            return foundItems;
        }

        int minX = Mathf.Max(0, startX);
        int minY = Mathf.Max(0, startY);
        int maxX = Mathf.Min(_columns, startX + width);
        int maxY = Mathf.Min(_rows, startY + height);

        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                if (_cells[x, y].State == GridState.OccupiedItem && _cells[x, y].OccupyingItemView != null)
                {
                    foundItems.Add(_cells[x, y].OccupyingItemView);
                }
            }
        }

        return foundItems;
    }

    public bool FindFirstAvailableSpace(int width, int height, out Vector2Int foundPosition, out bool needsRotation)
    {
        if (TryFindAvailableSpace(width, height, out foundPosition))
        {
            needsRotation = false;
            return true;
        }

        if (width != height && TryFindAvailableSpace(height, width, out foundPosition))
        {
            needsRotation = true;
            return true;
        }

        foundPosition = Vector2Int.zero;
        needsRotation = false;
        return false;
    }

    public bool FindSpaceAround(int startX, int startY, int width, int height, out Vector2Int foundPosition, out bool needsRotation)
    {
        int maxRadius = Mathf.Max(_columns, _rows);

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            for (int x = startX - radius; x <= startX + radius; x++)
            {
                for (int y = startY - radius; y <= startY + radius; y++)
                {
                    bool isBorder =
                        x == startX - radius ||
                        x == startX + radius ||
                        y == startY - radius ||
                        y == startY + radius;

                    if (!isBorder)
                    {
                        continue;
                    }

                    if (IsSpaceAvailable(x, y, width, height))
                    {
                        foundPosition = new Vector2Int(x, y);
                        needsRotation = false;
                        return true;
                    }

                    if (width != height && IsSpaceAvailable(x, y, height, width))
                    {
                        foundPosition = new Vector2Int(x, y);
                        needsRotation = true;
                        return true;
                    }
                }
            }
        }

        return FindFirstAvailableSpace(width, height, out foundPosition, out needsRotation);
    }

    private void ApplyBlockedCells()
    {
        if (_cells == null)
        {
            return;
        }

        foreach (Vector2Int blockedCell in _blockedCells)
        {
            if (!IsWithinBounds(blockedCell.x, blockedCell.y))
            {
                continue;
            }

            _cells[blockedCell.x, blockedCell.y].State = GridState.Blocked;
            _cells[blockedCell.x, blockedCell.y].OccupyingItemView = null;
            _cells[blockedCell.x, blockedCell.y].IsItemRotated = false;
        }
    }

    private bool TryFindAvailableSpace(int width, int height, out Vector2Int foundPosition)
    {
        for (int y = 0; y <= _rows - height; y++)
        {
            for (int x = 0; x <= _columns - width; x++)
            {
                if (IsSpaceAvailable(x, y, width, height))
                {
                    foundPosition = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        foundPosition = Vector2Int.zero;
        return false;
    }

    private bool IsWithinBounds(int x, int y)
    {
        return x >= 0 && x < _columns && y >= 0 && y < _rows;
    }
}
