using System;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes
{
    /// <summary>
    /// 单个行为树节点的抽象基类定义，包含节点名称、进入、执行、退出和中断的基本逻辑
    /// </summary>
    public abstract class BehaviorNode
    {
        private bool _hasEntered;

        protected BehaviorNode(string nodeName)
        {
            NodeName = string.IsNullOrWhiteSpace(nodeName) ? GetType().Name : nodeName;
        }

        public string NodeName { get; }

        /// <summary>
        /// 进入该节点
        /// </summary>
        /// <param name="context"></param>
        public void Enter(BehaviorTreeContext context)
        {
            if (_hasEntered) return;

            _hasEntered = true;
            context.LogNodeEvent(NodeName, "Enter");
            OnEnter(context);
        }

        /// <summary>
        /// 执行该节点的 Tick 逻辑
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public BehaviorNodeResult Execute(BehaviorTreeContext context)
        {
            if (!_hasEntered) Enter(context);

            BehaviorNodeResult result = Tick(context);
            context.LogNodeEvent(NodeName, $"Tick => {result}");
            return result;
        }

        /// <summary>
        /// 退出该节点，传入当前的执行状态
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        public void Exit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            if (!_hasEntered) return;

            OnExit(context, result);
            context.LogNodeEvent(NodeName, $"Exit => {result}");
            _hasEntered = false;
        }

        /// <summary>
        /// 中断该节点的执行，强制退出当前节点
        /// </summary>
        /// <param name="context"></param>
        public void Abort(BehaviorTreeContext context)
        {
            if (!_hasEntered) return;

            OnAbort(context);
            context.LogNodeEvent(NodeName, "Abort");
            _hasEntered = false;
        }

        // 供子类重写的虚方法，可用节点的具体逻辑填充
        protected virtual void OnEnter(BehaviorTreeContext context) { }

        protected abstract BehaviorNodeResult Tick(BehaviorTreeContext context);

        protected virtual void OnExit(BehaviorTreeContext context, BehaviorNodeResult result) { }

        protected virtual void OnAbort(BehaviorTreeContext context) { }

        // 辅助方法，方便子类快速返回成功、失败或运行中的结果
        protected BehaviorNodeResult Succeed()
        {
            return BehaviorNodeResult.Success();
        }

        protected BehaviorNodeResult Running()
        {
            return BehaviorNodeResult.Running();
        }

        protected BehaviorNodeResult Fail(BehaviorFailureCode code, string detail)
        {
            return BehaviorNodeResult.Failure(
                new BehaviorFailureReason(code, NodeName, detail));
        }

        protected BehaviorNodeResult Fail(BehaviorFailureReason failureReason)
        {
            return BehaviorNodeResult.Failure(failureReason);
        }
    }
}