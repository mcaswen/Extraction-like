namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 决策候选目标类型
    /// 用于区分同一套评分公式下的资源、敌人、敌人来源和撤离行为
    /// </summary>
    public enum AgentDecisionTargetKind
    {
        None = 0,
        Resource = 1,
        EnemySource = 2,
        ActiveEnemy = 3,
        Extraction = 4
    }
}
