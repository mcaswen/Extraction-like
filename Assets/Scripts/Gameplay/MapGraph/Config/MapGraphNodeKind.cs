namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// 抽象地图节点类型
    /// 只描述图上的战术语义，不直接参与具体资源或敌人的运行时逻辑
    /// </summary>
    public enum MapGraphNodeKind
    {
        None = 0,
        Start = 1,
        Resource = 2,
        EnemySource = 3,
        ActiveEnemy = 4,
        Extraction = 5,
        Custom = 100
    }
}
