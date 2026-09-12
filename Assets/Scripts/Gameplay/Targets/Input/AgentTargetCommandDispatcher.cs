using Gameplay.Agent.Data;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Routes;
using Gameplay.Targets.Authoring;

namespace Gameplay.Targets.Input
{
    /// <summary>
    /// Resolves the target agent and submits cluster commands through the agent command interface.
    /// </summary>
    public sealed class AgentTargetCommandDispatcher
    {
        public bool UsesRoutes(string targetAgentId) => TryResolveTargetAgent(targetAgentId, out var handle) && handle.ReadOnly.RouteSnapshot.IsInstalled;

        public AgentRouteResult TrySubmitClusterRoute(GameplayTargetClusterAuthoringBase cluster, string targetAgentId = "")
        {
            AgentTargetKind kind = cluster is ResourceClusterAuthoring ? AgentTargetKind.Resource :
                cluster is ExtractionClusterAuthoring ? AgentTargetKind.Extraction :
                cluster is EnemySourceClusterAuthoring ? AgentTargetKind.EnemySource : AgentTargetKind.Enemy;
            var target = cluster != null ? AgentTargetRef.FromConcreteObject(kind, cluster.gameObject, cluster.TargetId) : AgentTargetRef.None;
            return AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest(string.Empty, AgentRouteSource.Player,
                AgentId.FromString(targetAgentId), targetRef: target));
        }

        public bool TrySubmitClusterCommand(
            GameplayTargetClusterAuthoringBase cluster,
            string targetAgentId,
            out AgentDirectiveRequest directiveRequest)
        {
            directiveRequest = default;
            if (UsesRoutes(targetAgentId))
            {
                // 旧 out Directive 不能伪造根已接受。正式调用方使用上面的根结果接口。
                return TrySubmitClusterRoute(cluster, targetAgentId).Accepted;
            }
            if (cluster != null && (!cluster.isActiveAndEnabled || cluster.HasBeenCompleted))
            {
                AgentDirectiveFeedbackChannel.Publish(new AgentDirectiveResult(default, AgentDirectiveStage.Rejected,
                    cluster.HasBeenCompleted ? AgentDirectiveFailure.TargetCompleted : AgentDirectiveFailure.InvalidTarget));
                return false;
            }
            if (!TryResolveTargetAgent(targetAgentId, out AgentRuntimeHandle agentHandle))
            {
                AgentDirectiveFeedbackChannel.Publish(new AgentDirectiveResult(default, AgentDirectiveStage.Rejected, AgentDirectiveFailure.NoAgent));
                return false;
            }

            string commandId = AgentManualDirectiveLock.CreateCommandId(cluster != null ? cluster.TargetId : string.Empty);
            if (!TargetClusterDirectiveFactory.TryCreateDirective(
                    cluster,
                    agentHandle,
                    commandId,
                    AgentManualDirectiveLock.ManualDirectivePriority,
                    out directiveRequest))
            {
                AgentDirectiveFeedbackChannel.Publish(new AgentDirectiveResult(default, AgentDirectiveStage.Rejected, AgentDirectiveFailure.InvalidTarget));
                return false;
            }

            return agentHandle.CommandReceiver.TrySubmitDirective(directiveRequest).Accepted;
        }

        private static bool TryResolveTargetAgent(
            string targetAgentId,
            out AgentRuntimeHandle agentHandle)
        {
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();

            if (!string.IsNullOrWhiteSpace(targetAgentId))
                return registry.TryGetHandle(targetAgentId, out agentHandle);

            return registry.TryGetFocusedHandle(out agentHandle);
        }

    }
}
