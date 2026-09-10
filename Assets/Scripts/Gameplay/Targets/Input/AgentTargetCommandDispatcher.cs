using Gameplay.Agent.Data;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;

namespace Gameplay.Targets.Input
{
    /// <summary>
    /// Resolves the target agent and submits cluster commands through the agent command interface.
    /// </summary>
    public sealed class AgentTargetCommandDispatcher
    {
        public bool TrySubmitClusterCommand(
            GameplayTargetClusterAuthoringBase cluster,
            string targetAgentId,
            out AgentDirectiveRequest directiveRequest)
        {
            directiveRequest = default;
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
