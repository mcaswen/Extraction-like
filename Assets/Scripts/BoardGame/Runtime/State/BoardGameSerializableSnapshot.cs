using System;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 可序列化的快照结构，用于承载整局运行时状态
    /// </summary>
    [Serializable]
    public sealed class BoardGameSerializableSnapshot
    {
        [SerializeField] private string _mapId;
        [SerializeField] private string _ruleSetId;
        [SerializeField] private string _lootSetId;
        [SerializeField] private BoardGameSessionState _sessionState;

        public BoardGameSerializableSnapshot(
            string mapId,
            string ruleSetId,
            string lootSetId,
            BoardGameSessionState sessionState)
        {
            _mapId = mapId;
            _ruleSetId = ruleSetId;
            _lootSetId = lootSetId;
            _sessionState = sessionState;
        }

        public string MapId => _mapId;
        public string RuleSetId => _ruleSetId;
        public string LootSetId => _lootSetId;
        public BoardGameSessionState SessionState => _sessionState;
    }
}
