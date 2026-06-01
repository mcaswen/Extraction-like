using System;
using System.Collections.Generic;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>
    /// 抽象图运行时状态容器
    /// 当前只管理 Agent 图上投影，后续可扩展节点风险、收益和占用状态
    /// </summary>
    public sealed class MapGraphRuntimeState
    {
        private readonly Dictionary<string, MapGraphAgentRuntimeState> _agentsById =
            new Dictionary<string, MapGraphAgentRuntimeState>(StringComparer.Ordinal);

        private readonly List<MapGraphAgentRuntimeState> _agentStates =
            new List<MapGraphAgentRuntimeState>();

        /// <summary>
        /// 当前投影到图上的 Agent 列表
        /// </summary>
        public IReadOnlyList<MapGraphAgentRuntimeState> AgentStates => _agentStates;

        /// <summary>
        /// 获取或创建指定 Agent 的图上状态
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public MapGraphAgentRuntimeState GetOrCreateAgentState(string agentId)
        {
            string normalizedAgentId = NormalizeId(agentId);
            if (_agentsById.TryGetValue(normalizedAgentId, out MapGraphAgentRuntimeState state))
                return state;

            state = new MapGraphAgentRuntimeState(normalizedAgentId);
            _agentsById.Add(normalizedAgentId, state);
            _agentStates.Add(state);
            return state;
        }

        /// <summary>
        /// 移除不在当前有效集合中的 Agent 投影
        /// </summary>
        /// <param name="activeAgentIds"></param>
        public void RemoveAgentsExcept(HashSet<string> activeAgentIds)
        {
            for (int index = _agentStates.Count - 1; index >= 0; index--)
            {
                MapGraphAgentRuntimeState state = _agentStates[index];
                if (activeAgentIds != null && activeAgentIds.Contains(state.AgentId))
                    continue;

                _agentsById.Remove(state.AgentId);
                _agentStates.RemoveAt(index);
            }
        }

        /// <summary>
        /// 清空全部运行时投影
        /// </summary>
        public void Clear()
        {
            _agentsById.Clear();
            _agentStates.Clear();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
