using Core.BehaviorTree.Blackboard;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent黑板键集中定义
    /// 将具体的业务逻辑承接到黑板中，供行为树 / Brain使用
    /// </summary>
    public static class AgentBlackboardKeys
    {
        /// <summary>
        /// 当前 Agent 的运行时 ID
        /// </summary>
        public static readonly BlackboardKey AgentId =
            new BlackboardKey("Agent_Id");

        /// <summary>
        /// 当前宏状态 ID
        /// 这里记录的是 Brain 宏状态，不是身体层动画 / 动作状态
        /// </summary>
        public static readonly BlackboardKey CurrentMacroStateId =
            new BlackboardKey("Agent_CurrentMacroStateId");

        /// <summary>
        /// 当前宏状态名称
        /// 主要用于 Inspector 和调试面板显示
        /// </summary>
        public static readonly BlackboardKey CurrentMacroStateName =
            new BlackboardKey("Agent_CurrentMacroStateName");

        /// <summary>
        /// 当前生命比例
        /// </summary>
        public static readonly BlackboardKey AgentHealthRatio =
            new BlackboardKey("Agent_HealthRatio");

        /// <summary>
        /// 当前 Agent 是否死亡
        /// </summary>
        public static readonly BlackboardKey AgentIsDead =
            new BlackboardKey("Agent_IsDead");

        /// <summary>
        /// 当前 Agent 是否看见可接战敌人
        /// </summary>
        public static readonly BlackboardKey HasVisibleEnemy =
            new BlackboardKey("Agent_HasVisibleEnemy");

        /// <summary>
        /// 当前 Agent 是否有可侦查的敌人来源目标
        /// </summary>
        public static readonly BlackboardKey HasEnemySourceTarget =
            new BlackboardKey("Agent_HasEnemySourceTarget");

        /// <summary>
        /// 当前 Agent 是否有可搜索资源目标
        /// </summary>
        public static readonly BlackboardKey HasResourceTarget =
            new BlackboardKey("Agent_HasResourceTarget");

        /// <summary>
        /// 当前 Agent 是否有可交互目标
        /// </summary>
        public static readonly BlackboardKey HasInteractableTarget =
            new BlackboardKey("Agent_HasInteractableTarget");

        /// <summary>
        /// 当前 Agent 是否应该进入撤离流程
        /// </summary>
        public static readonly BlackboardKey ShouldExtract =
            new BlackboardKey("Agent_ShouldExtract");

        /// <summary>
        /// 是否需要恢复
        /// 由生命值比例派生，供状态转移或行为节点读取
        /// </summary>
        public static readonly BlackboardKey NeedRecovery =
            new BlackboardKey("Agent_NeedRecovery");

        /// <summary>
        /// Agent 移动速度
        /// </summary>
        public static readonly BlackboardKey MoveSpeed =
            new BlackboardKey("Agent_MoveSpeed");

        /// <summary>
        /// Agent 普通移动停止距离
        /// </summary>
        public static readonly BlackboardKey MoveStoppingDistance =
            new BlackboardKey("Agent_MoveStoppingDistance");

        /// <summary>
        /// Agent 交互目标停止距离
        /// </summary>
        public static readonly BlackboardKey InteractionDistance =
            new BlackboardKey("Agent_InteractionDistance");

        /// <summary>
        /// Agent 攻击距离
        /// </summary>
        public static readonly BlackboardKey AttackRange =
            new BlackboardKey("Agent_AttackRange");

        /// <summary>
        /// Agent 攻击伤害
        /// </summary>
        public static readonly BlackboardKey AttackDamage =
            new BlackboardKey("Agent_AttackDamage");

        /// <summary>
        /// Agent 攻击间隔
        /// </summary>
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
