using Gameplay.Agent.Data;

namespace Gameplay.Agent.Commands
{
    public enum AgentDirectiveStage { Accepted, Rejected, Completed, Failed, Cancelled, Suspended, Resumed }
    public enum AgentDirectiveFailure { None, NoAgent, AgentUnavailable, InvalidTarget, TargetCompleted, NavigationNotReady, Unreachable, NoProgress, LostSight, Superseded, AttackUnavailable }
    public readonly struct AgentDirectiveResult
    {
        public AgentDirectiveRequest Request { get; }
        public AgentDirectiveStage Stage { get; }
        public AgentDirectiveFailure Reason { get; }
        public bool Accepted => Stage == AgentDirectiveStage.Accepted || Stage == AgentDirectiveStage.Resumed;
        public AgentDirectiveResult(AgentDirectiveRequest request, AgentDirectiveStage stage, AgentDirectiveFailure reason = AgentDirectiveFailure.None)
        { Request = request; Stage = stage; Reason = reason; }
    }
}
