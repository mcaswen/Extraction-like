namespace Gameplay.Player.Data
{
    /// <summary>
    /// 主角 Brain 的宏状态标识
    /// 这里描述的是“AI 当前处于什么决策阶段”的宏观状态，而非具体执行状态
    /// </summary>
    public enum PlayerMacroStateId
    {
        None = 0,
        Explore = 1,
        Combat = 2,
        SearchResource = 3,
        InteractLoot = 4,
        Extraction = 5
    }
}