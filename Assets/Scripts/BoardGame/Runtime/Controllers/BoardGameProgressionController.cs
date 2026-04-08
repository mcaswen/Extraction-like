using System;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 升级流程控制器
    /// 负责处理升级选择和禁用升级系统时的状态清理
    /// </summary>
    public sealed class BoardGameProgressionController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly BoardProgressionService _progressionService;
        private readonly bool _isFeatureEnabled;

        public BoardGameProgressionController(
            BoardGameSessionState sessionState,
            BoardProgressionService progressionService,
            bool isFeatureEnabled)
        {
            _sessionState = sessionState;
            _progressionService = progressionService;
            _isFeatureEnabled = isFeatureEnabled;
        }

        public event Action Changed;

        /// <summary>
        /// 在升级系统关闭时，清空历史遗留的待选状态
        /// </summary>
        public void SyncDisabledState()
        {
            if (_isFeatureEnabled ||
                (!_sessionState.IsAwaitingLevelUpChoice &&
                 _sessionState.PendingLevelUpCount <= 0 &&
                 _sessionState.PendingLevelUpChoices.Count <= 0))
            {
                return;
            }

            ClearPendingState();
        }

        /// <summary>
        /// 应用一个升级选项
        /// </summary>
        public bool TryApplyLevelUpChoice(int choiceIndex)
        {
            if (!_isFeatureEnabled)
            {
                _sessionState.StatusMessage = BoardGameStatusMessageUtility.System("Level-up progression is currently disabled");
                NotifyChanged();
                return false;
            }

            bool success = _progressionService.TryApplyLevelUpChoice(_sessionState, choiceIndex, out string message);

            if (!string.IsNullOrEmpty(message))
            {
                _sessionState.StatusMessage = message;
            }

            NotifyChanged();
            return success;
        }

        /// <summary>
        /// 清空所有等待中的升级选择状态
        /// </summary>
        public void ClearPendingState()
        {
            _sessionState.ActiveLevelUpAgentId = string.Empty;
            _sessionState.PendingLevelUpChoices.Clear();

            foreach (BoardAgentState agentState in _sessionState.AgentStates)
            {
                if (agentState != null)
                {
                    agentState.PendingLevelUpCount = 0;
                }
            }

            NotifyChanged();
        }

        /// <summary>
        /// 广播升级模块状态变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
