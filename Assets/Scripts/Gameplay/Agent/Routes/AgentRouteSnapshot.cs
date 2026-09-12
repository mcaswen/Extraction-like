using System;
using System.Collections.Generic;

namespace Gameplay.Agent.Routes
{
    public readonly struct AgentRouteSnapshot
    {
        public bool IsInstalled { get; }
        public bool HasRoute { get; }
        public AgentRouteRequest Request { get; }
        public long RouteVersion { get; }
        public AgentRouteStage Stage { get; }
        public AgentRouteFailure Failure { get; }
        public IReadOnlyList<string> NodeIds { get; }
        public int StepIndex { get; }
        public AgentClusterStepSnapshot CurrentStep { get; }
        public bool HasPendingRequest { get; }
        public AgentRouteRequest PendingRequest { get; }
        public long GraphRevision { get; }
        public long CostRevision { get; }
        public long BindingRevision { get; }
        public bool IsActive => HasRoute && Stage == AgentRouteStage.Accepted;
        public bool HoldsPlayerRoute => (IsActive && Request.Source == AgentRouteSource.Player) ||
            (HasPendingRequest && PendingRequest.Source == AgentRouteSource.Player);
        public string CurrentNodeId => NodeIds != null && StepIndex >= 0 && StepIndex < NodeIds.Count ? NodeIds[StepIndex] : string.Empty;
        public string PreviousNodeId => NodeIds != null && StepIndex > 0 && StepIndex < NodeIds.Count ? NodeIds[StepIndex - 1] : string.Empty;

        internal AgentRouteSnapshot(AgentRouteState state, AgentClusterStepSnapshot step, AgentRouteRequest? pending)
        {
            IsInstalled = true; HasRoute = state != null; Request = state?.Request ?? default;
            RouteVersion = state?.Version ?? 0; Stage = state?.Stage ?? AgentRouteStage.Planning;
            Failure = state?.Failure ?? AgentRouteFailure.None; NodeIds = state?.Plan?.NodeIds ?? Array.Empty<string>();
            StepIndex = state?.Cursor ?? -1; CurrentStep = step;
            HasPendingRequest = pending.HasValue; PendingRequest = pending ?? default;
            GraphRevision = state?.Plan?.GraphRevision ?? 0; CostRevision = state?.Plan?.CostRevision ?? 0;
            BindingRevision = state?.Plan?.BindingRevision ?? 0;
        }
    }
}
