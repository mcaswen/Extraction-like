using Gameplay.Agent.Data;
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
            if (!TryResolveTargetAgent(targetAgentId, out AgentRuntimeHandle agentHandle))
                return false;

            string commandId = AgentManualDirectiveLock.CreateCommandId(cluster != null ? cluster.TargetId : string.Empty);
            if (!TargetClusterDirectiveFactory.TryCreateDirective(
                    cluster,
                    agentHandle,
                    commandId,
                    AgentManualDirectiveLock.ManualDirectivePriority,
                    out directiveRequest))
            {
                return false;
            }

            ApplyTargetFacts(agentHandle, cluster);
            agentHandle.CommandReceiver.SubmitDirective(directiveRequest);
            return true;
        }

        private static bool TryResolveTargetAgent(
            string targetAgentId,
            out AgentRuntimeHandle agentHandle)
        {
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();

            if (!string.IsNullOrWhiteSpace(targetAgentId) &&
                registry.TryGetHandle(targetAgentId, out agentHandle))
            {
                return true;
            }

            return registry.TryGetFocusedHandle(out agentHandle);
        }

        private static void ApplyTargetFacts(
            AgentRuntimeHandle agentHandle,
            GameplayTargetClusterAuthoringBase cluster)
        {
            bool isActiveEnemy = cluster is ActiveEnemyClusterAuthoring;
            bool isEnemySource = cluster is EnemySourceClusterAuthoring;
            bool isResource = cluster is ResourceClusterAuthoring;
            bool isExtraction = cluster is ExtractionClusterAuthoring;

            agentHandle.CommandReceiver.SetVisibleEnemy(isActiveEnemy);
            agentHandle.CommandReceiver.SetHasEnemySourceTarget(isEnemySource);
            agentHandle.CommandReceiver.SetHasResourceTarget(isResource);
            agentHandle.CommandReceiver.SetHasInteractableTarget(false);
            agentHandle.CommandReceiver.SetShouldExtract(isExtraction);
        }
    }
}
