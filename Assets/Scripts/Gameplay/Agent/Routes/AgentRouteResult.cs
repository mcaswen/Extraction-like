namespace Gameplay.Agent.Routes
{
    public enum AgentRouteStage { Planning, Accepted, Rejected, Completed, Failed, Cancelled, Dead, Extracted }
    public enum AgentRouteFailure
    {
        None, NoAgent, AgentUnavailable, MapUnavailable, InvalidRequest, MissingTarget, TargetUnavailable,
        NoReachableEntry, Disconnected, StaleContext, Superseded, Unreachable, NoProgress,
        SpawnFailed, NoExecutableMember, CapacityExtraction, NavigationNotReady
    }

    public readonly struct AgentRouteResult
    {
        public AgentRouteRequest Request { get; }
        public long RouteVersion { get; }
        public AgentRouteStage Stage { get; }
        public AgentRouteFailure Reason { get; }
        public string Detail { get; }
        public bool Accepted => Stage == AgentRouteStage.Accepted;
        public AgentRouteResult(AgentRouteRequest request, long version, AgentRouteStage stage,
            AgentRouteFailure reason = AgentRouteFailure.None, string detail = "")
        { Request = request; RouteVersion = version; Stage = stage; Reason = reason; Detail = detail ?? string.Empty; }
    }
}
