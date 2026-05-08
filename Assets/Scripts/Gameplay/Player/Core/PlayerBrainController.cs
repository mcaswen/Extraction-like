using System;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Debugging;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Debugging;
using Core.StateMachine.Runtime;
using Gameplay.Player.AI.Factories;
using Gameplay.Player.Data;
using Gameplay.Player.Interfaces;

namespace Gameplay.Player.Core
{
    /// <summary>
    /// 主角 AI 大脑总控
    /// 负责持有 Blackboard、行为树上下文、状态机上下文，并驱动主角自主决策+行动的 MVP 宏状态机
    /// </summary>
    public sealed class PlayerBrainController
    {
        private readonly IPlayerReadOnly _playerReadOnly;

        private readonly BehaviorBlackboard _blackboard;
        private readonly BehaviorTreeDebugTrace _behaviorTreeDebugTrace;
        private readonly StateMachineDebugTrace _stateMachineDebugTrace;
        private readonly BehaviorTreeContext _behaviorTreeContext;
        private readonly StateMachineContext _stateMachineContext;

        private readonly HierarchicalStateMachine _stateMachine;

        public BehaviorBlackboard Blackboard => _blackboard;

        public PlayerMacroStateId CurrentMacroStateId =>
            _blackboard.GetValueOrDefault<PlayerMacroStateId>(
                PlayerBlackboardKeys.CurrentMacroStateId,
                PlayerMacroStateId.None);

        public string CurrentMacroStateName => CurrentMacroStateId.ToString();

        /// <summary>
        /// 装配各大组件，比如 Blackboard、Context、StateMachine，并将它们连接起来
        /// </summary>
        /// <param name="playerReadOnly"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public PlayerBrainController(IPlayerReadOnly playerReadOnly)
        {
            _playerReadOnly = playerReadOnly ?? throw new ArgumentNullException(nameof(playerReadOnly));

            _blackboard = new BehaviorBlackboard();
            _behaviorTreeDebugTrace = new BehaviorTreeDebugTrace();
            _stateMachineDebugTrace = new StateMachineDebugTrace();

            _behaviorTreeContext = new BehaviorTreeContext(
                _blackboard,
                _behaviorTreeDebugTrace,
                _playerReadOnly);

            _stateMachineContext = new StateMachineContext(
                _behaviorTreeContext,
                _stateMachineDebugTrace);

            PlayerBrainStateFactory stateFactory = new PlayerBrainStateFactory();
            PlayerBrainTransitionRules transitionRules = new PlayerBrainTransitionRules();
            PlayerBrainStateMachineFactory stateMachineFactory =
                new PlayerBrainStateMachineFactory(stateFactory, transitionRules);

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
        /// 外部事实写入统一入口。
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
