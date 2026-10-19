namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 作用域回调：由 ScopeDecorator 在子树进入、退出和中断时调用，用于管理子树共享的执行记忆
    /// </summary>
    public interface IBehaviorScope
    {
        void OnScopeEnter(BehaviorTreeContext context);

        void OnScopeExit(BehaviorTreeContext context, BehaviorNodeResult result);

        void OnScopeAbort(BehaviorTreeContext context);
    }
}
