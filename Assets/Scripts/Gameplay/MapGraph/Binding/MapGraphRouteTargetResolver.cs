using System;
using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>把场景绑定和原群系统适配为路线事实，不创建敌人、不执行或推进路线。</summary>
    public sealed class MapGraphRouteTargetResolver : IAgentRouteTargetResolver
    {
        private sealed class NodeRecord
        {
            public MapGraphTargetBinding Binding;
            public MapGraphNodeKind Kind;
            public Vector3 Anchor;
            public bool HasAnchor, Available;
        }
        private readonly MapGraphBindingAuthoring _binding;
        private readonly Dictionary<string, NodeRecord> _nodes = new Dictionary<string, NodeRecord>(StringComparer.Ordinal);
        private readonly List<NodeRecord> _ordered = new List<NodeRecord>();
        private SO_MapGraphDefinition _definition;
        private long _bindingRevision = -1, _definitionRevision = -1, _revision;
        private int _anchorCursor;
        private bool _available;
        public long Revision { get { EnsureContext(); return _revision; } }

        public MapGraphRouteTargetResolver(MapGraphBindingAuthoring binding)
        { _binding = binding != null ? binding : throw new ArgumentNullException(nameof(binding)); EnsureContext(); }

        /// <summary>安装器低频分批观察锚点，查询事实时也同步观察当前节点；不做导航查询。</summary>
        public int RefreshAnchors(int budget)
        {
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            EnsureContext(); int count = Math.Min(budget, _ordered.Count);
            for (int i = 0; i < count; i++)
            {
                Observe(_ordered[_anchorCursor]);
                _anchorCursor = (_anchorCursor + 1) % _ordered.Count;
            }
            return count;
        }

        public bool TryResolveNode(AgentTargetRef target, out string nodeId)
        {
            EnsureContext(); nodeId = string.Empty;
            if (!_available) return false;
            GameObject obj = target.TargetObject;
            if (obj == null)
                return !target.IsConcreteObject && _binding.TryGetNodeIdForTargetId(target.TargetId, out nodeId);
            var cluster = obj.GetComponent<GameplayTargetClusterAuthoringBase>() ?? obj.GetComponentInParent<GameplayTargetClusterAuthoringBase>();
            if (cluster != null && TryResolveCluster(cluster, out nodeId)) return true;
            var registry = GameplayTargetRegistry.ActiveInstance;
            if (registry == null) return false;
            var enemy = obj.GetComponent<global::EnemyHealthController>() ?? obj.GetComponentInParent<global::EnemyHealthController>();
            if (enemy != null && registry.TryFindEnemyClusterByEnemy(enemy, out var active))
                return TryResolveCluster(active, out nodeId);
            var point = obj.GetComponent<global::ExtractionPointController>() ?? obj.GetComponentInParent<global::ExtractionPointController>();
            if (point != null && registry.TryFindExtractionClusterByPoint(point, out var extraction))
                return TryResolveCluster(extraction, out nodeId);
            var box = obj.GetComponent<global::LootBoxEntity>() ?? obj.GetComponentInParent<global::LootBoxEntity>();
            var loot = obj.GetComponent<global::WorldLootItem>() ?? obj.GetComponentInParent<global::WorldLootItem>();
            GameObject resource = box != null ? box.gameObject : loot != null ? loot.gameObject : obj;
            return registry.TryFindResourceClusterByEntity(resource, out var resources) && TryResolveCluster(resources, out nodeId);
        }

        public bool TryGetFacts(string nodeId, out AgentRouteTargetFacts facts)
        {
            EnsureContext(); facts = default;
            if (!_nodes.TryGetValue(nodeId ?? string.Empty, out var record)) return false;
            Observe(record);
            var target = record.Binding.DirectTarget as GameplayTargetClusterAuthoringBase;
            var status = target == null || !record.HasAnchor ? AgentRouteTargetStatus.Missing : AgentRouteTargetStatus.Ready;
            int alive = 0;
            if (status != AgentRouteTargetStatus.Missing)
            {
                if (!_available || !record.Available) status = AgentRouteTargetStatus.Unavailable;
                else if (target is EnemySourceClusterAuthoring source) status = SourceStatus(source, out alive);
                else if (target is ActiveEnemyClusterAuthoring active) status = ActiveStatus(active, out alive);
                else if (target.HasBeenCompleted) status = AgentRouteTargetStatus.Completed;
            }
            facts = new AgentRouteTargetFacts(nodeId, record.Kind, record.Anchor, status, alive);
            return true;
        }

        public bool TryCreateProcessingDirective(string nodeId, AgentId agentId, string commandId, int priority,
            out AgentDirectiveRequest directive, out AgentDirectiveFailure failure)
        {
            directive = default; failure = AgentDirectiveFailure.InvalidTarget;
            if (!TryGetFacts(nodeId, out var facts) || !facts.CanTraverse) return false;
            if (facts.Status == AgentRouteTargetStatus.Completed)
            { failure = AgentDirectiveFailure.TargetCompleted; return false; }
            if (facts.Status != AgentRouteTargetStatus.Ready) return false;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null || !registry.Query.TryGetAgent(agentId, out var agent))
            { failure = AgentDirectiveFailure.NoAgent; return false; }
            var target = _nodes[nodeId].Binding.DirectTarget as GameplayTargetClusterAuthoringBase;
            if (target is EnemySourceClusterAuthoring source) target = source.ConfiguredActiveEnemyCluster;
            if (!TargetClusterDirectiveFactory.TryCreateDirective(target, agent, commandId, priority, out directive)) return false;
            // 旧 Factory 的空候选兼容结果是群对象，不能把它当一个真实敌人执行。
            if (directive.DirectiveType == AgentDirectiveType.Engage &&
                (directive.TargetObject == null || directive.TargetObject.GetComponentInParent<global::EnemyHealthController>() == null)) return false;
            failure = AgentDirectiveValidationService.Validate(agent.ReadOnly, directive);
            return failure == AgentDirectiveFailure.None;
        }

        private bool TryResolveCluster(GameplayTargetClusterAuthoringBase cluster, out string nodeId)
        {
            nodeId = string.Empty;
            // 使用真实来源/活跃引用，不能在重复 TargetId 间用距离猜归属。
            foreach (var record in _ordered)
            {
                var target = record.Binding.DirectTarget;
                if (target != cluster && !(target is EnemySourceClusterAuthoring source && source.ConfiguredActiveEnemyCluster == cluster)) continue;
                if (nodeId.Length > 0) { nodeId = string.Empty; return false; }
                nodeId = record.Binding.NodeId;
            }
            return nodeId.Length > 0;
        }

        private static AgentRouteTargetStatus SourceStatus(EnemySourceClusterAuthoring source, out int alive)
        {
            var active = source.ConfiguredActiveEnemyCluster;
            alive = active != null ? active.CountLivingEnemies() : 0;
            if (active != null && !active.isActiveAndEnabled) return AgentRouteTargetStatus.Unavailable;
            var points = source.SpawnPoints;
            if (points == null || points.Count == 0) return AgentRouteTargetStatus.SpawnFailed;
            bool waiting = false;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null || !points[i].TryGetComponent(out global::EnemySpawnPoint point)) return AgentRouteTargetStatus.SpawnFailed;
                if (point.SpawnState == global::EnemySpawnState.Failed) return AgentRouteTargetStatus.SpawnFailed;
                if (point.SpawnState != global::EnemySpawnState.Completed) waiting = true;
                else if (!point.RegisteredToSourceCluster || point.RegisteredSourceCluster != source) return AgentRouteTargetStatus.SpawnFailed;
            }
            if (waiting) return AgentRouteTargetStatus.WaitingSpawn;
            if (active == null || !active.HasRegisteredEnemy) return AgentRouteTargetStatus.SpawnFailed;
            return alive > 0 ? AgentRouteTargetStatus.Ready : AgentRouteTargetStatus.Completed;
        }

        private static AgentRouteTargetStatus ActiveStatus(ActiveEnemyClusterAuthoring active, out int alive)
        {
            alive = active.CountLivingEnemies();
            if (!active.HasRegisteredEnemy) return AgentRouteTargetStatus.WaitingSpawn;
            return alive > 0 ? AgentRouteTargetStatus.Ready : AgentRouteTargetStatus.Completed;
        }

        private void EnsureContext()
        {
            var definition = _binding != null ? _binding.MapDefinition : null;
            long bindingRevision = _binding != null ? _binding.Revision : -1;
            long definitionRevision = definition != null ? definition.Revision : -1;
            bool available = _binding != null && _binding.isActiveAndEnabled && definition != null && definition.IsCommandGraph && _binding.IsValid;
            if (_definition == definition && _bindingRevision == bindingRevision && _definitionRevision == definitionRevision && _available == available) return;
            _definition = definition; _bindingRevision = bindingRevision; _definitionRevision = definitionRevision; _available = available;
            _revision++; _nodes.Clear(); _ordered.Clear(); _anchorCursor = 0;
            if (_binding == null || definition == null) return;
            var graph = new MapGraphService(definition);
            foreach (var item in _binding.TargetBindings)
            {
                if (item == null || _nodes.ContainsKey(item.NodeId) || !graph.TryGetNode(item.NodeId, out var node)) continue;
                var record = new NodeRecord { Binding = item, Kind = node.NodeKind };
                record.HasAnchor = item.TryGetNavigationAnchor(out record.Anchor);
                record.Available = IsAvailable(item.DirectTarget);
                _nodes.Add(item.NodeId, record); _ordered.Add(record);
            }
        }

        private void Observe(NodeRecord record)
        {
            bool hasAnchor = record.Binding.TryGetNavigationAnchor(out var anchor);
            bool available = IsAvailable(record.Binding.DirectTarget);
            if (hasAnchor != record.HasAnchor || available != record.Available || (hasAnchor && anchor != record.Anchor)) _revision++;
            record.HasAnchor = hasAnchor; record.Anchor = anchor; record.Available = available;
        }
        private static bool IsAvailable(GameplayTargetAuthoringBase target) => target != null && target.isActiveAndEnabled &&
            (!(target is EnemySourceClusterAuthoring source) || source.ConfiguredActiveEnemyCluster == null || source.ConfiguredActiveEnemyCluster.isActiveAndEnabled);
    }
}
