using System;

namespace Gameplay.Agent.Data
{
    /// <summary>动作携带的路线关联，低层只保存身份，不拥有根请求、路径或游标。</summary>
    public readonly struct AgentDirectiveRouteContext : IEquatable<AgentDirectiveRouteContext>
    {
        public string RootRequestId { get; }
        public long RouteVersion { get; }
        public string NodeId { get; }
        public int StepIndex { get; }
        public bool IsPlayerRoute { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(RootRequestId) && RouteVersion > 0 &&
            !string.IsNullOrWhiteSpace(NodeId) && StepIndex >= 0;
        public AgentDirectiveRouteContext(string rootRequestId, long routeVersion, string nodeId, int stepIndex, bool isPlayerRoute)
        { RootRequestId = rootRequestId; RouteVersion = routeVersion; NodeId = nodeId; StepIndex = stepIndex; IsPlayerRoute = isPlayerRoute; }
        public bool Equals(AgentDirectiveRouteContext other) => RootRequestId == other.RootRequestId && RouteVersion == other.RouteVersion &&
            NodeId == other.NodeId && StepIndex == other.StepIndex && IsPlayerRoute == other.IsPlayerRoute;
        public override bool Equals(object obj) => obj is AgentDirectiveRouteContext other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RootRequestId, RouteVersion, NodeId, StepIndex, IsPlayerRoute);
    }
}
