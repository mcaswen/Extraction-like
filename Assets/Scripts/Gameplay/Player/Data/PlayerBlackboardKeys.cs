using Core.BehaviorTree.Blackboard;

namespace Gameplay.Player.Data
{
    /// <summary>
    /// 主角黑板键集中定义
    /// 将具体的业务逻辑承接到黑板中，供行为树 / Brain使用
    /// </summary>
    public static class PlayerBlackboardKeys
    {
        /// <summary>
        /// 当前宏状态 ID
        /// 这里记录的是 Brain 宏状态，不是身体层动画 / 动作状态
        /// </summary>
        public static readonly BlackboardKey CurrentMacroStateId =
            new BlackboardKey("Player_CurrentMacroStateId");

        public static readonly BlackboardKey CurrentMacroStateName =
            new BlackboardKey("Player_CurrentMacroStateName");

        public static readonly BlackboardKey PlayerHealthRatio =
            new BlackboardKey("Player_HealthRatio");

        public static readonly BlackboardKey PlayerIsDead =
            new BlackboardKey("Player_IsDead");

        public static readonly BlackboardKey HasVisibleEnemy =
            new BlackboardKey("Player_HasVisibleEnemy");

        public static readonly BlackboardKey HasResourceTarget =
            new BlackboardKey("Player_HasResourceTarget");

        public static readonly BlackboardKey HasInteractableTarget =
            new BlackboardKey("Player_HasInteractableTarget");

        public static readonly BlackboardKey ShouldExtract =
            new BlackboardKey("Player_ShouldExtract");

        // TODO: 当前设为派生事实，后续考虑直接改为单状态
        public static readonly BlackboardKey NeedRecovery =
            new BlackboardKey("Player_NeedRecovery");

        /// <summary>
        /// 是否存在待处理的玩家干预请求
        /// </summary>
        public static readonly BlackboardKey HasPendingDirective =
            new BlackboardKey("Player_HasPendingDirective");

        /// <summary>
        /// 当前缓存的干预请求体
        /// 这里只做暂存
        /// </summary>
        public static readonly BlackboardKey PendingDirectiveRequest =
            new BlackboardKey("Player_PendingDirectiveRequest");
    }
}
