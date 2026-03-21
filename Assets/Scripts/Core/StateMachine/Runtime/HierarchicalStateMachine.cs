using System;
using System.Collections.Generic;

namespace Core.StateMachine.Runtime
{
    /// <summary>
    /// 整个分层状态机的核心运行类，负责管理状态的切换、更新和行为树的执行
    /// 负责：
        /// 持有根状态和状态机上下文
        /// 维护当前状态路径和目标状态路径的缓冲区列表
        /// 每帧更新当前已经激活的状态链
        /// 从当前叶子节点向上找可触发转移
        /// 再执行退出/进入链
    /// </summary>
    public sealed class HierarchicalStateMachine
    {
        private readonly StateMachineState _rootState;
        private readonly StateMachineContext _context;

        private readonly List<StateMachineState> _currentPathBuffer = new List<StateMachineState>(16);
        private readonly List<StateMachineState> _targetPathBuffer = new List<StateMachineState>(16);

        private bool _isStarted;

        public HierarchicalStateMachine(
            StateMachineState rootState,
            StateMachineContext context)
        {
            _rootState = rootState ?? throw new ArgumentNullException(nameof(rootState));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public StateMachineState CurrentLeafState { get; private set; }

        /// <summary>
        /// 启动状态机，从根状态开始进入默认路径上的状态，直到最底层的一个状态作为当前叶子状态
        /// </summary>
        /// <param name="timeSeconds"></param>
        public void Start(double timeSeconds = 0d)
        {
            if (_isStarted)
            {
                return;
            }

            SetTimeContext(0f, timeSeconds);

            StateMachineState targetLeafState = _rootState.ResolveDefaultLeaf();
            EnterPathToTarget(targetLeafState, StateChangeReason.Start);

            CurrentLeafState = targetLeafState;
            _isStarted = true;
        }

        /// <summary>
        /// 每帧更新时，检查从当前叶子节点向上是否有转移条件被触发，如果有则执行转移
        /// 再更新当前状态链上的所有状态，并 Tick 绑定的行为树
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="timeSeconds"></param>
        public void Update(float deltaTime, double timeSeconds)
        {
            if (!_isStarted)
            {
                Start(timeSeconds);
            }

            SetTimeContext(deltaTime, timeSeconds);

            if (TryGetTriggeredTransition(out StateTransition transition))
            {
                PerformTransition(
                    transition.TargetState.ResolveDefaultLeaf(),
                    StateChangeReason.Transition,
                    transition.Name);
            }

            UpdateActiveStatePath();
        }

        /// <summary>
        /// 强制转换到目标状态，无视当前状态和转移条件，直接执行退出/进入链完成状态切换
        /// </summary>
        /// <param name="targetState"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void ForceTransition(StateMachineState targetState)
        {
            if (targetState == null)
            {
                throw new ArgumentNullException(nameof(targetState));
            }

            if (!_isStarted)
            {
                Start();
            }

            PerformTransition(
                targetState.ResolveDefaultLeaf(),
                StateChangeReason.ForceTransition,
                "ForceTransition");
        }

        // 设置状态机上下文的时间信息
        private void SetTimeContext(float deltaTime, double timeSeconds)
        {
            _context.DeltaTime = deltaTime;
            _context.TimeSeconds = timeSeconds;

            _context.BehaviorTreeContext.DeltaTime = deltaTime;
            _context.BehaviorTreeContext.TimeSeconds = timeSeconds;
        }

        /// <summary>
        /// 找到触发的目标转移
        /// 从当前叶子节点开始，沿着父状态向上检查每个状态的转移列表，找出第一个满足条件的转移，并返回该转移
        /// </summary>
        /// <param name="triggeredTransition"></param>
        /// <returns></returns>
        private bool TryGetTriggeredTransition(out StateTransition triggeredTransition)
        {
            StateMachineState currentState = CurrentLeafState;

            while (currentState != null)
            {
                StateTransition bestTransition = null;
                int bestPriority = int.MinValue;

                // 遍历当前状态的所有转移
                for (int index = 0; index < currentState.Transitions.Count; index++)
                {
                    StateTransition transition = currentState.Transitions[index];
                    if (!transition.CanTransition(_context))
                    {
                        continue;
                    }

                    // 如果转移条件满足，比较优先级，找出优先级最高的转移
                    if (transition.Priority > bestPriority)
                    {
                        bestPriority = transition.Priority;
                        bestTransition = transition;
                    }
                }

                // 如果找到了满足条件的转移，则返回该转移；否则继续向上检查父状态
                if (bestTransition != null)
                {
                    triggeredTransition = bestTransition;
                    return true;
                }

                currentState = currentState.ParentState;
            }

            triggeredTransition = null;
            return false;
        }

        /// <summary>
        /// 执行从当前状态到目标状态的转移
        /// 1. 首先找到当前状态和目标状态路径上的分叉点
        /// 2. 然后依次退出当前链的公共祖先以下的状态
        /// 3. 再依次进入目标链的公共祖先以下的状态
        /// 4. 最后更新当前叶子状态为目标叶子状态
        /// </summary>
        /// <param name="targetLeafState"></param>
        /// <param name="reason"></param>
        /// <param name="transitionName"></param>
        private void PerformTransition(
            StateMachineState targetLeafState,
            StateChangeReason reason,
            string transitionName)
        {
            if (CurrentLeafState == targetLeafState)
            {
                return;
            }

            _currentPathBuffer.Clear();
            _targetPathBuffer.Clear();

            // 先构建当前状态和目标状态的路径列表
            CurrentLeafState.BuildPathFromRoot(_currentPathBuffer);
            targetLeafState.BuildPathFromRoot(_targetPathBuffer);

            // 再拿到两条路径的公共祖先
            int commonPrefixLength = GetCommonPrefixLength(_currentPathBuffer, _targetPathBuffer);

            // 从当前状态链的末尾开始，依次退出公共祖先以下的状态
            for (int index = _currentPathBuffer.Count - 1; index >= commonPrefixLength; index--)
            {
                _currentPathBuffer[index].Exit(_context, reason);
            }

            // 再从公共祖先以下开始，依次进入目标状态链上的状态
            for (int index = commonPrefixLength; index < _targetPathBuffer.Count; index++)
            {
                _targetPathBuffer[index].Enter(_context, reason);
            }

            // 最后设置当前叶子状态为目标叶子状态
            CurrentLeafState = targetLeafState;
            _context.LogStateEvent(
                CurrentLeafState.StateName,
                $"Transitioned via [{transitionName}]");
        }

        /// <summary>
        /// 开始时，进入到目标状态的路径上，依次进入每个状态
        /// </summary>
        /// <param name="targetLeafState"></param>
        /// <param name="reason"></param>
        private void EnterPathToTarget(StateMachineState targetLeafState, StateChangeReason reason)
        {
            _targetPathBuffer.Clear();
            targetLeafState.BuildPathFromRoot(_targetPathBuffer);

            for (int index = 0; index < _targetPathBuffer.Count; index++)
            {
                _targetPathBuffer[index].Enter(_context, reason);
            }
        }

        /// <summary>
        /// 更新时，更新当前状态链上的所有状态，并 Tick 绑定的行为树
        /// </summary>
        private void UpdateActiveStatePath()
        {
            _currentPathBuffer.Clear();
            CurrentLeafState.BuildPathFromRoot(_currentPathBuffer);

            for (int index = 0; index < _currentPathBuffer.Count; index++)
            {
                _currentPathBuffer[index].Update(_context);
            }
        }
        
        /// <summary>
        /// 找到当前状态链和目标状态链的公共祖先的深度
        /// </summary>
        /// <param name="currentPath"></param>
        /// <param name="targetPath"></param>
        /// <returns></returns>
        private static int GetCommonPrefixLength(
            List<StateMachineState> currentPath,
            List<StateMachineState> targetPath)
        {
            int maxSharedLength = Math.Min(currentPath.Count, targetPath.Count);
            int commonLength = 0;

            while (commonLength < maxSharedLength &&
                   currentPath[commonLength] == targetPath[commonLength])
            {
                commonLength++;
            }

            return commonLength;
        }
    }
}