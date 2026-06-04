using BoardGame.Config;
using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 快照导出控制器
    /// 负责把当前对局运行时状态序列化成可保存快照
    /// </summary>
    public sealed class BoardGameSnapshotController
    {
        private readonly SO_BoardGame_MapDefinition _mapDefinition;
        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly SO_BoardGame_LootTableSet _lootTableSet;
        private readonly SO_BoardGame_AgentRoster _agentRoster;
        private readonly BoardGameSessionState _sessionState;

        public BoardGameSnapshotController(
            SO_BoardGame_MapDefinition mapDefinition,
            SO_BoardGame_RuleSet ruleSet,
            SO_BoardGame_LootTableSet lootTableSet,
            SO_BoardGame_AgentRoster agentRoster,
            BoardGameSessionState sessionState)
        {
            _mapDefinition = mapDefinition;
            _ruleSet = ruleSet;
            _lootTableSet = lootTableSet;
            _agentRoster = agentRoster;
            _sessionState = sessionState;
        }

        /// <summary>
        /// 生成当前对局快照
        /// </summary>
        public BoardGameSerializableSnapshot CreateSnapshot()
        {
            return new BoardGameSerializableSnapshot(
                _mapDefinition.MapId,
                _ruleSet.RuleSetId,
                _lootTableSet.LootSetId,
                _agentRoster != null ? _agentRoster.RosterId : string.Empty,
                _sessionState);
        }
    }
}
