using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Debugging;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Debugging;
using Core.StateMachine.Runtime;
using Gameplay.Agent.AI.Factories;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;

namespace Gameplay.Agent.Core
{
    /// <summary>
    /// Agent AI 大脑总控
    /// 负责持有 Blackboard、行为树上下文、状态机上下文，并驱动Agent自主决策+行动的 MVP 宏状态机
    /// </summary>
    public sealed class AgentBrainController
    {
        private readonly IAgentReadOnly _agentReadOnly;

        private readonly BehaviorBlackboard _blackboard;
        private readonly BehaviorTreeDebugTrace _behaviorTreeDebugTrace;
        private readonly StateMachineDebugTrace _stateMachineDebugTrace;
        private readonly BehaviorTreeContext _behaviorTreeContext;
        private readonly StateMachineContext _stateMachineContext;

        private readonly HierarchicalStateMachine _stateMachine;

        /// <summary>
        /// Agent Brain 的共享黑板
        /// </summary>
        public BehaviorBlackboard Blackboard => _blackboard;

        /// <summary>
        /// 当前 Brain 宏状态 ID
        /// </summary>
        public AgentMacroStateId CurrentMacroStateId =>
            _blackboard.GetValueOrDefault<AgentMacroStateId>(
                AgentBlackboardKeys.CurrentMacroStateId,
                AgentMacroStateId.None);

        /// <summary>
        /// 当前 Brain 宏状态名称
        /// </summary>
        public string CurrentMacroStateName => CurrentMacroStateId.ToString();

        /// <summary>
        /// 装配各大组件，比如 Blackboard、Context、StateMachine，并将它们连接起来
        /// </summary>
        /// <param name="agentReadOnly"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public AgentBrainController(IAgentReadOnly agentReadOnly)
        {
            _agentReadOnly = agentReadOnly ?? throw new ArgumentNullException(nameof(agentReadOnly));

            _blackboard = new BehaviorBlackboard();
            _behaviorTreeDebugTrace = new BehaviorTreeDebugTrace();
            _stateMachineDebugTrace = new StateMachineDebugTrace();

            _behaviorTreeContext = new BehaviorTreeContext(
                _blackboard,
                _behaviorTreeDebugTrace,
                _agentReadOnly);

            _stateMachineContext = new StateMachineContext(
                _behaviorTreeContext,
                _stateMachineDebugTrace);

            AgentBrainStateFactory stateFactory = new AgentBrainStateFactory();
            AgentBrainTransitionRules transitionRules = new AgentBrainTransitionRules();
            AgentBrainStateMachineFactory stateMachineFactory =
                new AgentBrainStateMachineFactory(stateFactory, transitionRules);

            _stateMachine = stateMachineFactory.Build(_stateMachineContext);
        }

        /// <summary>
        /// 启动 Brain 状态机
        /// 会让状态机自动进入默认叶子状态链
        /// </summary>
        /// <param name="timeSeconds"></param>
        public void Start(double timeSeconds)
        {
            _stateMachine.Start(timeSeconds);
        }

        /// <summary>
        /// 驱动 Brain 每帧更新
        /// 当前阶段主循环只做状态机推进，状态内具体动作逻辑后续再逐步填充
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="timeSeconds"></param>
        public void Tick(float deltaTime, double timeSeconds)
        {
            _stateMachine.Update(deltaTime, timeSeconds);
        }

        /// <summary>
        /// 外部事实写入统一入口
        /// Pawn、感知模块、外部系统都应通过该方法把事实同步给 Brain，而不是直接散写内部状态
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeSeconds"></param>
        public void SetFact<T>(BlackboardKey key, T value, double timeSeconds)
        {
            _blackboard.SetValue(key, value, timeSeconds);
        }
    }
}
