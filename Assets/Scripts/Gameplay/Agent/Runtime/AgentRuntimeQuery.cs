using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 运行时查询入口
    /// 用独立查询对象承接“找 Agent”的需求，避免查询逻辑散落到 UI、敌人、任务系统里
    /// </summary>
    public sealed class AgentRuntimeQuery
    {
        private readonly AgentRuntimeRegistry _registry;

        /// <summary>
        /// 创建一个绑定指定 Registry 的查询对象
        /// </summary>
        /// <param name="registry"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public AgentRuntimeQuery(AgentRuntimeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 当前 Registry 中的 Agent 数量
        /// </summary>
        public int AgentCount => _registry.AgentCount;

        /// <summary>
        /// 按 AgentId 查询 Agent 运行时句柄
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetAgent(AgentId agentId, out AgentRuntimeHandle handle)
        {
            return _registry.TryGetHandle(agentId, out handle);
        }

        /// <summary>
        /// 按字符串 AgentId 查询 Agent 运行时句柄
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetAgent(string agentId, out AgentRuntimeHandle handle)
        {
            return TryGetAgent(AgentId.FromString(agentId), out handle);
        }

        /// <summary>
        /// 查询当前默认 Agent 运行时句柄
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetPrimaryAgent(out AgentRuntimeHandle handle)
        {
            return _registry.TryGetPrimaryHandle(out handle);
        }

        /// <summary>
        /// 查询当前焦点 Agent 运行时句柄
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetFocusedAgent(out AgentRuntimeHandle handle)
        {
            return _registry.TryGetFocusedHandle(out handle);
        }

        /// <summary>
        /// 将当前有效 Agent 复制到外部缓冲区
        /// </summary>
        /// <param name="results"></param>
        public void CopyAgentsTo(List<AgentRuntimeHandle> results)
        {
            _registry.CopyHandlesTo(results);
        }

        /// <summary>
        /// 查询离指定世界坐标最近的有效 Agent
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetNearestAgent(Vector3 worldPosition, out AgentRuntimeHandle handle)
        {
            handle = default;
            float bestSqrDistance = float.PositiveInfinity;
            IReadOnlyList<AgentRuntimeHandle> registeredAgents = _registry.RegisteredAgents;

            for (int i = 0; i < registeredAgents.Count; i++)
            {
                AgentRuntimeHandle candidate = registeredAgents[i];
                if (!candidate.IsValid)
                    continue;

                float sqrDistance = (candidate.ReadOnly.Position - worldPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                handle = candidate;
            }

            return handle.IsValid;
        }
    }
}
