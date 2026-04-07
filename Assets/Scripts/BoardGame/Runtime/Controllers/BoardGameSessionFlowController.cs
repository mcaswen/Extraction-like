using System;
using System.Collections.Generic;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 对局流程控制器
    /// 负责串联升级门控、loot 交互门控和状态机推进
    /// </summary>
    public sealed class BoardGameSessionFlowController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly IReadOnlyDictionary<string, BoardNodeRuntimeState> _nodeStatesById;
        private readonly BoardGameProgressionController _progressionController;
        private readonly BoardGameLootInteractionController _lootInteractionController;
        private readonly BoardAgentActionStateMachine _actionStateMachine;

        public BoardGameSessionFlowController(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardGameProgressionController progressionController,
            BoardGameLootInteractionController lootInteractionController,
            BoardAgentActionStateMachine actionStateMachine)
        {
            _sessionState = sessionState;
            _nodeStatesById = nodeStatesById;
            _progressionController = progressionController;
            _lootInteractionController = lootInteractionController;
            _actionStateMachine = actionStateMachine;
        }

        public event Action Changed;

        /// <summary>
        /// 推进当前对局流程
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_sessionState.Outcome != BoardSessionOutcome.None)
            {
                NotifyChanged();
                return;
            }

            _sessionState.EnsureConsistentReferences();
            _progressionController.SyncDisabledState();

            if (_sessionState.IsAwaitingLevelUpChoice)
            {
                NotifyChanged();
                return;
            }

            _lootInteractionController.TickAwaitingLootInteraction(deltaTime);

            if (_sessionState.IsLootInteractionOpen)
            {
                NotifyChanged();
                return;
            }

            _sessionState.ElapsedSeconds += deltaTime;
            _sessionState.SimulationStepIndex += 1;

            foreach (BoardAgentState agentState in _sessionState.AgentStates)
            {
                _actionStateMachine.Tick(_sessionState, agentState, _nodeStatesById, deltaTime);

                if (_sessionState.Outcome != BoardSessionOutcome.None ||
                    _sessionState.IsAwaitingLevelUpChoice ||
                    _sessionState.IsLootInteractionOpen)
                {
                    break;
                }
            }

            _sessionState.EnsureConsistentReferences();
            NotifyChanged();
        }

        /// <summary>
        /// 广播对局流程已推进，供只读查询和旧事件桥接刷新
        /// </summary>
        private void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
