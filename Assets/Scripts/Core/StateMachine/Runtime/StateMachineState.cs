using System;
using System.Collections.Generic;
using Core.BehaviorTree.Runtime;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 分层状态机所有状态的基类定义，支持嵌套子状态和绑定行为树
    /// 负责：
        /// 维护父子状态关系
        /// 挂自己的转移列表
        /// 绑定一棵 BehaviorTree，进入状态时创建 Runner，更新时 Tick，退出时 Abort
        /// 定义 OnEnter / OnUpdate / OnExit 等生命周期方法
        /// 作为层级结构的一部分参与状态切换
    /// </summary>
    public abstract class StateMachineState
    {
        private readonly List<StateMachineState> _children = new List<StateMachineState>();
        private readonly List<StateTransition> _transitions = new List<StateTransition>();

        private readonly BehaviorTreeType _boundBehaviorTree;
        private BehaviorTreeRunner _behaviorTreeRunner;
        private StateMachineState _initialChildState;

        protected StateMachineState(string stateName, BehaviorTreeType boundBehaviorTree = null)
        {
            StateName = string.IsNullOrWhiteSpace(stateName) ? GetType().Name : stateName;
            _boundBehaviorTree = boundBehaviorTree;
        }

        public string StateName { get; }
        public StateMachineState ParentState { get; private set; }
        public StateMachineState InitialChildState => _initialChildState;

        public IReadOnlyList<StateMachineState> Children => _children;
        public IReadOnlyList<StateTransition> Transitions => _transitions;

        /// <summary>
        /// 添加一个子状态，并指定是否为初始子状态
        /// </summary>
        /// <param name="childState"></param>
        /// <param name="isInitialChild"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddChild(StateMachineState childState, bool isInitialChild = false)
        {
            if (childState == null)
            {
                throw new ArgumentNullException(nameof(childState));
            }

            if (childState.ParentState != null)
            {
                throw new InvalidOperationException(
                    $"State [{childState.StateName}] already has a parent.");
            }

            childState.ParentState = this;
            _children.Add(childState);

            if (_initialChildState == null || isInitialChild)
            {
                _initialChildState = childState;
            }
        }

        /// <summary>
        /// 添加一条条件转换，指定从当前状态到目标状态的转变条件和优先级
        /// </summary>
        /// <param name="transition"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddTransition(StateTransition transition)
        {
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition));
            }

            _transitions.Add(transition);
        }

        /// <summary>
        /// 进入时，启动绑定的行为树（如果有）
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reason"></param>
        internal void Enter(StateMachineContext context, StateChangeReason reason)
        {
            context.LogStateEvent(StateName, $"Enter ({reason})");

            if (_boundBehaviorTree != null)
            {
                _behaviorTreeRunner = new BehaviorTreeRunner(
                    _boundBehaviorTree,
                    context.BehaviorTreeContext);
            }

            OnEnter(context, reason);
        }

        /// <summary>
        /// 每帧更新时，持续 Tick 绑定的行为树
        /// </summary>
        /// <param name="context"></param>
        internal void Update(StateMachineContext context)
        {
            if (_behaviorTreeRunner != null)
            {
                _behaviorTreeRunner.Tick(context.DeltaTime, context.TimeSeconds);
            }

            OnUpdate(context);
        }

        /// <summary>
        /// 退出时，强制中断绑定的行为树（如果有），并传入状态变化的原因
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reason"></param>
        internal void Exit(StateMachineContext context, StateChangeReason reason)
        {
            if (_behaviorTreeRunner != null && _behaviorTreeRunner.IsRunning)
            {
                _behaviorTreeRunner.Abort(
                    new BehaviorFailureReason(
                        BehaviorFailureCode.ExternalAbort,
                        StateName,
                        $"Behavior tree aborted because state exited with reason [{reason}]."));
            }

            _behaviorTreeRunner = null;

            OnExit(context, reason);
            context.LogStateEvent(StateName, $"Exit ({reason})");
        }

        /// <summary>
        /// 从根状态开始构建一条到当前状态的路径，存入提供的缓冲区列表中
        /// </summary>
        /// <param name="buffer"></param>
        internal void BuildPathFromRoot(List<StateMachineState> buffer)
        {
            if (ParentState != null)
            {
                ParentState.BuildPathFromRoot(buffer);
            }

            buffer.Add(this);
        }

        /// <summary>
        /// 从当前状态开始，沿着初始子状态一直往下走，直到返回最底层的一个状态，作为默认叶子状态返回
        /// </summary>
        /// <returns></returns>
        internal StateMachineState ResolveDefaultLeaf()
        {
            StateMachineState currentState = this;

            while (currentState._initialChildState != null)
            {
                currentState = currentState._initialChildState;
            }

            return currentState;
        }

        protected virtual void OnEnter(StateMachineContext context, StateChangeReason reason) { }

        protected virtual void OnUpdate(StateMachineContext context) { }

        protected virtual void OnExit(StateMachineContext context, StateChangeReason reason) { }
    }
}