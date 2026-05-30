using Core.BehaviorTree.Blackboard;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent黑板键集中定义
    /// 将具体的业务逻辑承接到黑板中，供行为树 / Brain使用
    /// </summary>
    public static class AgentBlackboardKeys
    {
        public static readonly BlackboardKey AgentId =
            new BlackboardKey("Agent_Id");

        /// <summary>
        /// 当前宏状态 ID
        /// 这里记录的是 Brain 宏状态，不是身体层动画 / 动作状态
        /// </summary>
        public static readonly BlackboardKey CurrentMacroStateId =
            new BlackboardKey("Agent_CurrentMacroStateId");

        public static readonly BlackboardKey CurrentMacroStateName =
            new BlackboardKey("Agent_CurrentMacroStateName");

        public static readonly BlackboardKey AgentHealthRatio =
            new BlackboardKey("Agent_HealthRatio");

        public static readonly BlackboardKey AgentIsDead =
            new BlackboardKey("Agent_IsDead");

        public static readonly BlackboardKey HasVisibleEnemy =
            new BlackboardKey("Agent_HasVisibleEnemy");

        public static readonly BlackboardKey HasEnemySourceTarget =
            new BlackboardKey("Agent_HasEnemySourceTarget");

        public static readonly BlackboardKey HasResourceTarget =
            new BlackboardKey("Agent_HasResourceTarget");

        public static readonly BlackboardKey HasInteractableTarget =
            new BlackboardKey("Agent_HasInteractableTarget");

        public static readonly BlackboardKey ShouldExtract =
            new BlackboardKey("Agent_ShouldExtract");

        // NeedRecovery 由生命值比例派生，供状态转移或行为节点读取
        public static readonly BlackboardKey NeedRecovery =
            new BlackboardKey("Agent_NeedRecovery");

        public static readonly BlackboardKey MoveSpeed =
            new BlackboardKey("Agent_MoveSpeed");

        public static readonly BlackboardKey MoveStoppingDistance =
            new BlackboardKey("Agent_MoveStoppingDistance");

        public static readonly BlackboardKey InteractionDistance =
            new BlackboardKey("Agent_InteractionDistance");

        public static readonly BlackboardKey AttackRange =
            new BlackboardKey("Agent_AttackRange");

        public static readonly BlackboardKey AttackDamage =
            new BlackboardKey("Agent_AttackDamage");

        public static readonly BlackboardKey AttackInterval =
            new BlackboardKey("Agent_AttackInterval");

        /// <summary>
        /// 是否存在待处理的Agent干预请求
        /// </summary>
        public static readonly BlackboardKey HasPendingDirective =
            new BlackboardKey("Agent_HasPendingDirective");

        /// <summary>
        /// 当前缓存的干预请求体
        /// 这里只做暂存
        /// </summary>
        public static readonly BlackboardKey PendingDirectiveRequest =
            new BlackboardKey("Agent_PendingDirectiveRequest");
    }
}
