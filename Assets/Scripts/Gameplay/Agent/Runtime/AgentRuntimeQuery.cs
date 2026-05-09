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

        public AgentRuntimeQuery(AgentRuntimeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int AgentCount => _registry.AgentCount;

        public bool TryGetAgent(AgentId agentId, out AgentRuntimeHandle handle)
        {
            return _registry.TryGetHandle(agentId, out handle);
        }

        public bool TryGetAgent(string agentId, out AgentRuntimeHandle handle)
        {
            return TryGetAgent(AgentId.FromString(agentId), out handle);
        }

        public bool TryGetPrimaryAgent(out AgentRuntimeHandle handle)
        {
            return _registry.TryGetPrimaryHandle(out handle);
        }

        public void CopyAgentsTo(List<AgentRuntimeHandle> results)
        {
            _registry.CopyHandlesTo(results);
        }

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
