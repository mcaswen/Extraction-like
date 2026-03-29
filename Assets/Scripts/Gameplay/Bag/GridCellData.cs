//底层数据应当是一个二维数组，每个格子三种状态
public enum GridState//格子状态
{
    Empty,
    Occupied_Item,
    Locked_Searching
}
/// <summary>
/// 格子数据
/// </summary>
public class GridCellData
{
    public int X;
    public int Y;
    public GridState State = GridState.Empty;//初始化为空

    // 【核心修改】：不再记录模板数据，而是记录具体的 UI 脚本实例！
    public DraggableItemUI OccupyingUI;
    // 记录该物品放在这里时，是否发生了自动旋转
    public bool IsItemRotated;
    //构造方法
    public GridCellData(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void Clear()
    {
        State = GridState.Empty;
        OccupyingUI = null; 
        IsItemRotated = false;
    }
}

//总结  这是单个格子的数据类  包含  被什么占用  当前旋转状态   当前格子被占用状态（别忘了被锁上的情况）