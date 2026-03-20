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
        public BehaviorNodeStatus Execute(BehaviorTreeContext context)
        {
            if (!_hasEntered) Enter(context);

            BehaviorNodeStatus status = Tick(context);
            context.LogNodeEvent(NodeName, $"Tick => {status}");
            return status;
        }

        /// <summary>
        /// 退出该节点，传入当前的执行状态
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        public void Exit(BehaviorTreeContext context, BehaviorNodeStatus status)
        {
            if (!_hasEntered) return;

            OnExit(context, status);
            context.LogNodeEvent(NodeName, $"Exit => {status}");
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

        protected abstract BehaviorNodeStatus Tick(BehaviorTreeContext context);

        protected virtual void OnExit(BehaviorTreeContext context, BehaviorNodeStatus status) { }

        protected virtual void OnAbort(BehaviorTreeContext context) { }
    }
}