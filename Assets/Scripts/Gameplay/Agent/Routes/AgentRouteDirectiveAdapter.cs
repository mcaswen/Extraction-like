using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Routes
{
    /// <summary>兼容旧高层调用，正式场景始终转为群根请求；不制造已执行的子动作。</summary>
    public static class AgentRouteDirectiveAdapter
    {
        public static AgentDirectiveResult Submit(AgentRouteController controller, AgentDirectiveRequest request, AgentId agentId)
        {
            request=request.WithTargetAgentId(agentId);
            var result=controller.Submit(new AgentRouteRequest(string.Empty,
                AgentManualDirectiveLock.IsManualDirective(request)?AgentRouteSource.Player:AgentRouteSource.Autonomous,
                agentId, request.CommandId, request.TargetRef));
            if(result.Stage==AgentRouteStage.Planning) return new AgentDirectiveResult(request,AgentDirectiveStage.Planning);
            if(result.Accepted) return new AgentDirectiveResult(request,AgentDirectiveStage.Accepted);
            var reason=result.Reason switch {
                AgentRouteFailure.NoAgent=>AgentDirectiveFailure.NoAgent,
                AgentRouteFailure.AgentUnavailable=>AgentDirectiveFailure.AgentUnavailable,
                AgentRouteFailure.NavigationNotReady=>AgentDirectiveFailure.NavigationNotReady,
                AgentRouteFailure.NoReachableEntry or AgentRouteFailure.Disconnected or AgentRouteFailure.Unreachable=>AgentDirectiveFailure.Unreachable,
                AgentRouteFailure.NoProgress=>AgentDirectiveFailure.NoProgress,
                AgentRouteFailure.Superseded=>AgentDirectiveFailure.Superseded,
                _=>AgentDirectiveFailure.InvalidTarget
            };
            return new AgentDirectiveResult(request,AgentDirectiveStage.Rejected,reason);
        }
    }
}
