namespace Gameplay.Targets.Data
{
    /// <summary>
    /// Gameplay 目标层级
    /// Zone 是区域目标，Cluster 是群目标，Entity 是具体目标
    /// </summary>
    public enum GameplayTargetLevel
    {
        None = 0,
        Zone = 1,
        Cluster = 2,
        Entity = 3
    }

    /// <summary>
    /// Gameplay 目标类型
    /// 框架只描述目标语义，不定义目标选择优先级
    /// </summary>
    public enum GameplayTargetKind
    {
        None = 0,
        Mixed = 1,
        Resource = 2,
        Enemy = 3,
        Extraction = 4
    }
}
