/// <summary>
/// 网格单元的运行时状态
/// </summary>
public enum GridState
{
    Empty,
    OccupiedItem,
    LockedSearching,
    Blocked
}

/// <summary>
/// 单个格子的运行时数据
/// </summary>
public class GridCellData
{
    /// <summary>
    /// 创建一个新的运行时格子数据
    /// </summary>
    /// <param name="x">格子横坐标</param>
    /// <param name="y">格子纵坐标</param>
    public GridCellData(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public GridState State = GridState.Empty;

    public InventoryItemRuntimeState OccupyingItemState;

    public bool IsItemRotated;

    /// <summary>
    /// 清空当前格子的动态占用状态，保留其坐标信息
    /// </summary>
    public void Clear()
    {
        State = GridState.Empty;
        OccupyingItemState = null;
        IsItemRotated = false;
    }
}
