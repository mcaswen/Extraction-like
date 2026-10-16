using System;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Data;

namespace Gameplay.Agent.Routes
{
    public enum AgentRouteSource { Autonomous, Player }

    /// <summary>一条群路线的根请求，和具体动作的 CommandId/PayloadId 独立。</summary>
    public readonly struct AgentRouteRequest
    {
        public AgentId TargetAgentId { get; }
        public string RequestId { get; }
        public string TargetNodeId { get; }
        public AgentTargetRef TargetRef { get; }
        public AgentRouteSource Source { get; }
        public ResourceSearchPass ResourceSearch { get; }

        public AgentRouteRequest(string targetNodeId, AgentRouteSource source,
            AgentId targetAgentId = default, string requestId = "", AgentTargetRef targetRef = default,
            ResourceSearchPass resourceSearch = null)
        {
            TargetNodeId = targetNodeId?.Trim() ?? string.Empty;
            Source = source; TargetAgentId = targetAgentId; TargetRef = targetRef;
            RequestId = string.IsNullOrWhiteSpace(requestId) ? "Route_" + Guid.NewGuid().ToString("N") : requestId;
            ResourceSearch = source == AgentRouteSource.Player ? resourceSearch ?? new ResourceSearchPass() : null;
        }

        public AgentRouteRequest WithTargetAgentId(AgentId id)
            => new AgentRouteRequest(TargetNodeId, Source, id, RequestId, TargetRef, ResourceSearch);
        public AgentRouteRequest WithTargetNodeId(string nodeId)
            => new AgentRouteRequest(nodeId, Source, TargetAgentId, RequestId, TargetRef, ResourceSearch);
    }
}
