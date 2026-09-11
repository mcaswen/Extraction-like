using System.Collections.Generic;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Targets.Input
{
    /// <summary>
    /// Converts gameplay target clusters into agent commands.
    /// </summary>
    public static class TargetClusterDirectiveFactory
    {
        public static bool TryCreateDirective(
            GameplayTargetClusterAuthoringBase cluster,
            AgentRuntimeHandle agentHandle,
            string commandId,
            int priority,
            out AgentDirectiveRequest directiveRequest)
        {
            if (cluster == null || !agentHandle.IsValid)
            {
                directiveRequest = default;
                return false;
            }

            string targetId = cluster.TargetId;

            if (cluster is ActiveEnemyClusterAuthoring activeEnemyCluster)
            {
                return TryCreateActiveEnemyDirective(
                    activeEnemyCluster,
                    agentHandle,
                    targetId,
                    commandId,
                    priority,
                    out directiveRequest);
            }

            if (cluster is EnemySourceClusterAuthoring enemySourceCluster)
            {
                directiveRequest = CreateConcreteDirective(
                    AgentDirectiveType.MoveTo,
                    AgentTargetKind.EnemySource,
                    enemySourceCluster.gameObject,
                    targetId,
                    agentHandle,
                    commandId,
                    priority);
                return true;
            }

            if (cluster is ResourceClusterAuthoring resourceCluster)
            {
                directiveRequest = CreateConcreteDirective(
                    AgentDirectiveType.Search,
                    AgentTargetKind.Resource,
                    resourceCluster.gameObject,
                    targetId,
                    agentHandle,
                    commandId,
                    priority);
                return true;
            }

            if (cluster is ExtractionClusterAuthoring extractionCluster)
            {
                return TryCreateExtractionDirective(
                    extractionCluster,
                    agentHandle,
                    targetId,
                    commandId,
                    priority,
                    out directiveRequest);
            }

            directiveRequest = default;
            return false;
        }

        private static bool TryCreateExtractionDirective(
            ExtractionClusterAuthoring extractionCluster,
            AgentRuntimeHandle agentHandle,
            string targetId,
            string commandId,
            int priority,
            out AgentDirectiveRequest directiveRequest)
        {
            if (!extractionCluster.TryGetNearestExtractionPoint(
                    agentHandle.ReadOnly.Position,
                    out global::ExtractionPointController extractionPoint) ||
                extractionPoint == null)
            {
                Debug.LogWarning(
                    $"[TargetInput] 撤离群 [{extractionCluster.name}] 没有可用的 ExtractionPoint 成员，无法创建撤离指令。请在 Extraction Members 里配置撤离点实体。",
                    extractionCluster);
                directiveRequest = default;
                return false;
            }

            var candidates = new List<AgentDirectiveRequest>();
            foreach (var member in extractionCluster.ExtractionMembers)
            {
                if (member == null || member.HasBeenCompleted ||
                    !member.TryGetComponent(out global::ExtractionPointController point) || point == null || !point.gameObject.activeInHierarchy)
                    continue;
                candidates.Add(CreateConcreteDirective(AgentDirectiveType.Extract, AgentTargetKind.Extraction,
                    point.gameObject, targetId, agentHandle, commandId, priority));
            }
            directiveRequest = TargetClusterDirectiveCandidateSelector.Select(agentHandle.ReadOnly, candidates);
            return true;
        }

        private static bool TryCreateActiveEnemyDirective(
            ActiveEnemyClusterAuthoring activeEnemyCluster,
            AgentRuntimeHandle agentHandle,
            string targetId,
            string commandId,
            int priority,
            out AgentDirectiveRequest directiveRequest)
        {
            var enemies = new List<global::EnemyHealthController>();
            activeEnemyCluster.CopyAliveEnemiesTo(enemies);
            var candidates = new List<AgentDirectiveRequest>();
            foreach (var enemy in enemies)
                candidates.Add(CreateConcreteDirective(AgentDirectiveType.Engage, AgentTargetKind.Enemy,
                    enemy.gameObject, targetId, agentHandle, commandId, priority));
            // 空群仍交给生命周期检查，保持原有无效/完成反馈路径。
            directiveRequest = candidates.Count > 0
                ? TargetClusterDirectiveCandidateSelector.Select(agentHandle.ReadOnly, candidates)
                : CreateConcreteDirective(AgentDirectiveType.Engage, AgentTargetKind.Enemy,
                    activeEnemyCluster.gameObject, targetId, agentHandle, commandId, priority);
            return true;
        }

        private static AgentDirectiveRequest CreateConcreteDirective(
            AgentDirectiveType directiveType,
            AgentTargetKind targetKind,
            GameObject targetObject,
            string targetId,
            AgentRuntimeHandle agentHandle,
            string commandId,
            int priority)
        {
            return new AgentDirectiveRequest(
                directiveType,
                AgentTargetRef.FromConcreteObject(
                    targetKind,
                    targetObject,
                    targetId),
                targetId,
                agentHandle.AgentId,
                commandId,
                priority);
        }
    }
}
