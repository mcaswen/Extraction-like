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

        /// <summary>面向展示的名称；未设置时与 NodeName 相同</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? NodeName : _displayName;
            set => _displayName = value;
        }

        private string _displayName;

        /// <summary>节点类别，供可视化和遥测区分组合、装饰、条件和动作</summary>
        public virtual BehaviorNodeKind Kind => BehaviorNodeKind.Action;

        /// <summary>直接子节点数量；叶子节点为 0</summary>
        public virtual int ChildCount => 0;

        /// <summary>按序号取直接子节点</summary>
        public virtual BehaviorNode GetChild(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>所属行为树；构建 BehaviorTree 时写入，一个节点实例只能属于一棵树</summary>
        public global::Core.BehaviorTree.Runtime.BehaviorTree Tree { get; internal set; }

        /// <summary>在所属树内的深度优先序号；未加入树时为 -1</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>当前是否已进入且尚未退出或中断</summary>
        public bool IsEntered => _hasEntered;

        /// <summary>最近一次 Tick 的结果状态</summary>
        public BehaviorNodeStatus LastStatus { get; private set; } = BehaviorNodeStatus.Failure;

        /// <summary>最近一次 Tick 失败时的失败码</summary>
        public BehaviorFailureCode LastFailureCode { get; private set; }

        /// <summary>最近一次 Tick 所在的运行器推进序号；从未执行时为 0</summary>
        public int LastTickVersion { get; private set; }

        /// <summary>
        /// 进入该节点
        /// </summary>
        /// <param name="context"></param>
        public void Enter(BehaviorTreeContext context)
        {
            if (_hasEntered) return;

            _hasEntered = true;
            if (context.NodeEventSink != null) context.RecordNodeEvent(this, BehaviorNodeEventKind.Enter, LastStatus, BehaviorFailureCode.None);
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
            LastStatus = result.Status;
            LastFailureCode = result.FailureReason.Code;
            LastTickVersion = context.TickVersion;
            if (context.NodeEventSink != null) context.RecordNodeEvent(this, BehaviorNodeEventKind.Tick, result.Status, result.FailureReason.Code);
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
            if (context.NodeEventSink != null) context.RecordNodeEvent(this, BehaviorNodeEventKind.Exit, result.Status, result.FailureReason.Code);
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
            if (context.NodeEventSink != null) context.RecordNodeEvent(this, BehaviorNodeEventKind.Abort, LastStatus, BehaviorFailureCode.ExternalAbort);
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