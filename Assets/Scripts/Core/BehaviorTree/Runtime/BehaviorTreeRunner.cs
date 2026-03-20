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
        private BehaviorNodeStatus _lastStatus = BehaviorNodeStatus.Failure;

        public BehaviorTreeRunner(BehaviorTree behaviorTree, BehaviorTreeContext context)
        {
            _behaviorTree = behaviorTree ?? throw new ArgumentNullException(nameof(behaviorTree));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public BehaviorNodeStatus LastStatus => _lastStatus; // 当前节点的执行状态
        public bool IsRunning => _isStarted && !_isAborted && _lastStatus == BehaviorNodeStatus.Running;

        /// <summary>
        /// 行为树的单次 Tick 更新
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="timeSeconds"></param>
        /// <returns></returns>
        public BehaviorNodeStatus Tick(float deltaTime, double timeSeconds)
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
            _lastStatus = _behaviorTree.RootNode.Execute(_context);

            // 如果节点不再 Running，退出节点并重置状态
            if (_lastStatus != BehaviorNodeStatus.Running)
            {
                _behaviorTree.RootNode.Exit(_context, _lastStatus);
                _isStarted = false;
            }

            return _lastStatus;
        }

        /// <summary>
        /// 中断行为树的执行，强制退出当前节点并将状态设置为 Failure
        /// </summary>
        public void Abort()
        {
            if (!_isStarted)
            {
                return;
            }

            _behaviorTree.RootNode.Abort(_context);
            _isStarted = false;
            _isAborted = true;
            _lastStatus = BehaviorNodeStatus.Failure;
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
            _lastStatus = BehaviorNodeStatus.Failure;
        }
    }
}