using System;
using System.Collections.Generic;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 玩家改写 AI 目标的运行时控制器
    /// 负责校验是否允许打断，并触发真正的改道
    /// </summary>
    public sealed class BoardGameTargetRedirectController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly IReadOnlyDictionary<string, BoardNodeRuntimeState> _nodeStatesById;
        private readonly BoardInterruptService _interruptService;
        private readonly BoardAgentActionStateMachine _actionStateMachine;

        public BoardGameTargetRedirectController(
            BoardGameSessionState sessionState,
            IReadOnlyDictionary<string, BoardNodeRuntimeState> nodeStatesById,
            BoardInterruptService interruptService,
            BoardAgentActionStateMachine actionStateMachine)
        {
            _sessionState = sessionState;
            _nodeStatesById = nodeStatesById;
            _interruptService = interruptService;
            _actionStateMachine = actionStateMachine;
        }

        public event Action Changed;

        /// <summary>
        /// 尝试把 AI 当前目标改写到指定节点
        /// </summary>
        public bool TryRedirectToNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (_sessionState.IsAwaitingLevelUpChoice)
            {
                _sessionState.StatusMessage = "Choose a level-up upgrade before redirecting";
                NotifyChanged();
                return false;
            }

            if (_sessionState.IsAwaitingLootInteraction)
            {
                _sessionState.StatusMessage = "Finish the current loot interaction before redirecting";
                NotifyChanged();
                return false;
            }

            bool success = _actionStateMachine.TryRedirect(_sessionState, _nodeStatesById, nodeId, out _);
            NotifyChanged();
            return success;
        }

        /// <summary>
        /// 查询某个节点当前是否允许作为玩家改写目标
        /// </summary>
        public bool IsNodeValidRedirectTarget(string nodeId)
        {
            if (_sessionState.IsAwaitingLevelUpChoice || _sessionState.IsAwaitingLootInteraction)
            {
                return false;
            }

            BoardInterruptEvaluation evaluation = _interruptService.Evaluate(_sessionState, _nodeStatesById, nodeId);
            return evaluation.CanInterrupt;
        }

        /// <summary>
        /// 广播改目标模块状态变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
