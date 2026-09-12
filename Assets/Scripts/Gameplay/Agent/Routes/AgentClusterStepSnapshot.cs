using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    public enum AgentClusterStepPhase
    { None, Travelling, Processing, WaitingSpawn, WaitingForInventory, WaitingForMember, Extracting, Completed, Failed, Cancelled }

    public readonly struct AgentClusterStepSnapshot
    {
        public AgentDirectiveRouteContext Context { get; }
        public AgentClusterStepPhase Phase { get; }
        public Vector3 Anchor { get; }
        public string CommandId { get; }
        public int AliveMembers { get; }
        public bool IsRetaliating { get; }
        public AgentRouteFailure Failure { get; }
        public bool IsTerminal => Phase == AgentClusterStepPhase.Completed || Phase == AgentClusterStepPhase.Failed || Phase == AgentClusterStepPhase.Cancelled;
        public AgentClusterStepSnapshot(AgentDirectiveRouteContext context, AgentClusterStepPhase phase,
            Vector3 anchor, string commandId, int aliveMembers, bool retaliating, AgentRouteFailure failure)
        { Context = context; Phase = phase; Anchor = anchor; CommandId = commandId; AliveMembers = aliveMembers; IsRetaliating = retaliating; Failure = failure; }
    }
}
