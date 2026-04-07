using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardGame.Config
{
    /// <summary>
    /// 多 Agent 编队配置
    /// 用于声明四个 AI 的身份 展示色和出生节点
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_BoardGame_AgentRoster",
        menuName = "BoardGame/Agent Roster")]
    public sealed class SO_BoardGame_AgentRoster : ScriptableObject
    {
        [SerializeField] private string _rosterId = "default_agent_roster";
        [SerializeField] private List<BoardGameAgentRosterEntry> _agentEntries = new List<BoardGameAgentRosterEntry>
        {
            new BoardGameAgentRosterEntry("agent_01", "Agent 1", new Color(0.94f, 0.39f, 0.31f, 1f)),
            new BoardGameAgentRosterEntry("agent_02", "Agent 2", new Color(0.27f, 0.62f, 0.98f, 1f)),
            new BoardGameAgentRosterEntry("agent_03", "Agent 3", new Color(0.27f, 0.79f, 0.44f, 1f)),
            new BoardGameAgentRosterEntry("agent_04", "Agent 4", new Color(0.96f, 0.76f, 0.23f, 1f))
        };

        public string RosterId => _rosterId;
        public IReadOnlyList<BoardGameAgentRosterEntry> AgentEntries => _agentEntries;

        /// <summary>
        /// 查询 roster 中是否存在指定 AgentId
        /// </summary>
        public bool HasAgentEntry(string agentId)
        {
            if (string.IsNullOrEmpty(agentId))
            {
                return false;
            }

            foreach (BoardGameAgentRosterEntry agentEntry in _agentEntries)
            {
                if (agentEntry != null && string.Equals(agentEntry.AgentId, agentId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 用场景出生位标记回写 roster 的起始节点
        /// </summary>
        public void ImportSceneSpawnLayout(
            IReadOnlyList<BoardGameAgentSpawnImportEntry> importEntries,
            bool clearMissingAssignments)
        {
            Dictionary<string, string> importedStartNodeIdsByAgentId =
                new Dictionary<string, string>(StringComparer.Ordinal);

            if (importEntries != null)
            {
                foreach (BoardGameAgentSpawnImportEntry importEntry in importEntries)
                {
                    if (string.IsNullOrEmpty(importEntry.AgentId))
                    {
                        continue;
                    }

                    importedStartNodeIdsByAgentId[importEntry.AgentId] = importEntry.StartNodeId ?? string.Empty;
                }
            }

            foreach (BoardGameAgentRosterEntry agentEntry in _agentEntries)
            {
                if (agentEntry == null || string.IsNullOrEmpty(agentEntry.AgentId))
                {
                    continue;
                }

                if (importedStartNodeIdsByAgentId.TryGetValue(agentEntry.AgentId, out string startNodeId))
                {
                    agentEntry.SetStartNodeId(startNodeId);
                    continue;
                }

                if (clearMissingAssignments)
                {
                    agentEntry.SetStartNodeId(string.Empty);
                }
            }
        }

        public void ApplyPlannerSpawnPresetToAsset(bool clearMissingAssignments)
        {
            ImportSceneSpawnLayout(
                BoardGamePlannerPresetConfig.CreatePlannerAgentSpawnPresets(),
                clearMissingAssignments);
        }
    }

    /// <summary>
    /// 单个 Agent 的静态编队定义
    /// </summary>
    [Serializable]
    public sealed class BoardGameAgentRosterEntry
    {
        [SerializeField] private string _agentId;
        [SerializeField] private string _displayName = "Agent";
        [SerializeField] private Color _agentColor = Color.white;
        [SerializeField] private string _startNodeId;

        public BoardGameAgentRosterEntry(string agentId, string displayName, Color agentColor, string startNodeId = "")
        {
            _agentId = agentId;
            _displayName = displayName;
            _agentColor = agentColor;
            _startNodeId = startNodeId;
        }

        public string AgentId => _agentId;
        public string DisplayName => _displayName;
        public Color AgentColor => _agentColor;
        public string StartNodeId => _startNodeId;

        /// <summary>
        /// 更新 Agent 的出生节点
        /// 仅供编辑器导入链回写使用
        /// </summary>
        public void SetStartNodeId(string startNodeId)
        {
            _startNodeId = startNodeId ?? string.Empty;
        }
    }

    /// <summary>
    /// 场景出生位回写 roster 时使用的导入快照
    /// </summary>
    public readonly struct BoardGameAgentSpawnImportEntry
    {
        public BoardGameAgentSpawnImportEntry(string agentId, string startNodeId)
        {
            AgentId = agentId;
            StartNodeId = startNodeId;
        }

        public string AgentId { get; }
        public string StartNodeId { get; }
    }
}
