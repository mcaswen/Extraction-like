using System;
using BoardGame.Runtime.Services;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 道具使用控制器
    /// 负责处理药水等消耗品的使用请求
    /// </summary>
    public sealed class BoardGameItemUseController
    {
        private readonly BoardGameSessionState _sessionState;
        private readonly BoardLootResolutionService _lootResolutionService;

        public BoardGameItemUseController(
            BoardGameSessionState sessionState,
            BoardLootResolutionService lootResolutionService)
        {
            _sessionState = sessionState;
            _lootResolutionService = lootResolutionService;
        }

        public event Action Changed;

        /// <summary>
        /// 尝试使用一个道具栏里的道具
        /// </summary>
        public bool TryUseItem(string instanceId)
        {
            if (_sessionState.IsAwaitingLevelUpChoice)
            {
                _sessionState.StatusMessage = "Choose a level-up upgrade before using items";
                NotifyChanged();
                return false;
            }

            bool success = _lootResolutionService.TryConsumeItem(_sessionState.AgentState, instanceId, out string message);
            _sessionState.StatusMessage = message;
            NotifyChanged();
            return success;
        }

        /// <summary>
        /// 广播道具使用模块状态变化
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
