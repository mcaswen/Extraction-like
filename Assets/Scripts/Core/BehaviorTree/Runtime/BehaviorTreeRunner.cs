using System;
using Core.BehaviorTree.Nodes;
using BehaviorTree = Core.BehaviorTree.Runtime.BehaviorTree;

namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树运行器类定义，负责执行行为树的运行时 Tick，并管理其执行状态
    /// </summary>
    public sealed class BehaviorTreeRunner
    {
        private readonly BehaviorTree _behaviorTree;
        private readonly BehaviorTreeContext _context;

        private bool _isStarted;
        private bool _isAborted;
        private BehaviorNodeResult _lastResult = BehaviorNodeResult.Failure(
            new BehaviorFailureReason(
                BehaviorFailureCode.ExternalAbort,
                "BehaviorTreeRunner",
                "Tree has not run yet."));

        public BehaviorTreeRunner(BehaviorTree behaviorTree, BehaviorTreeContext context)
        {
            _behaviorTree = behaviorTree ?? throw new ArgumentNullException(nameof(behaviorTree));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public BehaviorNodeResult LastResult => _lastResult;
        public BehaviorFailureReason LastFailureReason => _lastResult.FailureReason;
        public bool IsRunning => _isStarted && !_isAborted && _lastResult.IsRunning;

        /// <summary>
        /// 行为树的单次 Tick 更新
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="timeSeconds"></param>
        /// <returns></returns>
        public BehaviorNodeResult Tick(float deltaTime, double timeSeconds)
        {
            // 更新上下文的时间戳和版本号
            _context.TickVersion++;
            _context.DeltaTime = deltaTime;
            _context.TimeSeconds = timeSeconds;

            // 如果行为树尚未开始执行，则进入根节点
            if (!_isStarted)
            {
                _behaviorTree.RootNode.Enter(_context);
                _isStarted = true;
                _isAborted = false;
            }

            // 执行根节点的 Tick，并更新最后的状态
            _lastResult = _behaviorTree.RootNode.Execute(_context);

            // 如果节点不再 Running，退出节点并重置状态
            if (!_lastResult.IsRunning)
            {
                _behaviorTree.RootNode.Exit(_context, _lastResult);
                _isStarted = false;
            }

            return _lastResult;
        }

        /// <summary>
        /// 中断行为树的执行，强制退出当前节点并返回 ExternalAbort 和原因
        /// </summary>
        public void Abort()
        {
            Abort(new BehaviorFailureReason(
                BehaviorFailureCode.ExternalAbort,
                "BehaviorTreeRunner",
                "Tree was aborted externally."));
        }
        
        /// <summary>
        /// 带原因的重载版本，执行真正的中断逻辑并重置状态
        /// </summary>
        /// <param name="failureReason"></param>
        public void Abort(BehaviorFailureReason failureReason)
        {
            if (!_isStarted)
            {
                _lastResult = BehaviorNodeResult.Failure(failureReason);
                _isAborted = true;
                return;
            }

            _behaviorTree.RootNode.Abort(_context);
            _lastResult = BehaviorNodeResult.Failure(failureReason);
            _isStarted = false;
            _isAborted = true;
        }

        /// <summary>
        /// 重置行为树的执行状态，准备下一次执行
        /// </summary>
        public void Reset()
        {
            if (_isStarted)
            {
                Abort();
            }

            _isAborted = false;
            _lastResult = BehaviorNodeResult.Failure(
                new BehaviorFailureReason(
                    BehaviorFailureCode.ExternalAbort,
                    "BehaviorTreeRunner",
                    "Tree was reset."));
        }
    }
}