/// <summary>
/// 背包运行时物品视图的最小抽象。
/// Model 层只依赖这个接口，避免直接耦合具体 UI 组件。
/// </summary>
public interface IInventoryItemView
{
    InventoryItemData ItemData { get; }
}

/// <summary>
/// 网格单元的运行时状态。
/// </summary>
public enum GridState
{
    Empty,
    OccupiedItem,
    LockedSearching,
    Blocked
}

/// <summary>
/// 单个格子的运行时数据。
/// </summary>
public class GridCellData
{
    public GridCellData(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public GridState State = GridState.Empty;

    public IInventoryItemView OccupyingItemView;

    public bool IsItemRotated;

    public void Clear()
    {
        State = GridState.Empty;
        OccupyingItemView = null;
        IsItemRotated = false;
    }
}
