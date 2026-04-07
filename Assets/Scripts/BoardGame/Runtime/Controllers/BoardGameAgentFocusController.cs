using System;
using System.Collections.Generic;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 焦点 Agent 控制器
    /// 负责维护当前玩家正在观察和操作的 Agent
    /// </summary>
    public sealed class BoardGameAgentFocusController
    {
        private readonly BoardGameSessionState _sessionState;

        public BoardGameAgentFocusController(BoardGameSessionState sessionState)
        {
            _sessionState = sessionState;
            EnsureValidFocus();
        }

        public event Action Changed;

        /// <summary>
        /// 获取当前焦点 Agent
        /// </summary>
        public BoardAgentState GetFocusedAgentState()
        {
            return _sessionState.GetFocusedAgentState();
        }

        /// <summary>
        /// 切换焦点到指定 Agent
        /// </summary>
        public bool TrySetFocusedAgent(string agentId)
        {
            BoardAgentState targetAgentState = _sessionState.GetAgentState(agentId);

            if (targetAgentState == null || !targetAgentState.IsFocusable)
            {
                return false;
            }

            if (_sessionState.FocusedAgentId == targetAgentState.AgentId)
            {
                return true;
            }

            _sessionState.FocusedAgentId = targetAgentState.AgentId;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 切换到下一个可聚焦 Agent
        /// </summary>
        public bool FocusNextAgent()
        {
            return StepFocus(1);
        }

        /// <summary>
        /// 切换到上一个可聚焦 Agent
        /// </summary>
        public bool FocusPreviousAgent()
        {
            return StepFocus(-1);
        }

        /// <summary>
        /// 校正失效焦点
        /// 例如当前焦点死亡后自动跳到下一个还活着的 Agent
        /// </summary>
        public void EnsureValidFocus()
        {
            if (_sessionState.GetFocusedAgentState() != null)
            {
                return;
            }

            List<BoardAgentState> focusableAgents = GetFocusableAgents();

            if (focusableAgents.Count == 0)
            {
                _sessionState.FocusedAgentId = string.Empty;
                return;
            }

            _sessionState.FocusedAgentId = focusableAgents[0].AgentId;
            Changed?.Invoke();
        }

        private bool StepFocus(int direction)
        {
            List<BoardAgentState> focusableAgents = GetFocusableAgents();

            if (focusableAgents.Count <= 1)
            {
                return focusableAgents.Count == 1 && TrySetFocusedAgent(focusableAgents[0].AgentId);
            }

            string focusedAgentId = _sessionState.GetFocusedAgentState()?.AgentId;
            int focusedIndex = focusableAgents.FindIndex(agentState => agentState.AgentId == focusedAgentId);

            if (focusedIndex < 0)
            {
                focusedIndex = 0;
            }

            int targetIndex = (focusedIndex + direction + focusableAgents.Count) % focusableAgents.Count;
            return TrySetFocusedAgent(focusableAgents[targetIndex].AgentId);
        }

        private List<BoardAgentState> GetFocusableAgents()
        {
            List<BoardAgentState> focusableAgents = new List<BoardAgentState>();

            foreach (BoardAgentState agentState in _sessionState.AgentStates)
            {
                if (agentState != null && agentState.IsFocusable)
                {
                    focusableAgents.Add(agentState);
                }
            }

            if (focusableAgents.Count > 0)
            {
                return focusableAgents;
            }

            foreach (BoardAgentState agentState in _sessionState.AgentStates)
            {
                if (agentState != null)
                {
                    focusableAgents.Add(agentState);
                }
            }

            return focusableAgents;
        }
    }
}
