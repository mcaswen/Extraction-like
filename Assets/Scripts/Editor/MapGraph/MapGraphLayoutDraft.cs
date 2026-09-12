using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>一次生成或编辑的待验收数据，独立于 SO 和场景，丢弃草稿即可取消。</summary>
    public sealed class MapGraphLayoutDraft
    {
        public IReadOnlyList<MapGraphZoneDefinition> Zones { get; }
        public IReadOnlyList<MapGraphNodeDefinition> Nodes { get; }
        public IReadOnlyList<MapGraphEdgeDefinition> Edges { get; }
        public MapGraphLayoutConstraints Constraints { get; }
        public string StartNodeId { get; }
        public MapGraphService Graph { get; }

        public MapGraphLayoutDraft(IEnumerable<MapGraphZoneDefinition> zones, IEnumerable<MapGraphNodeDefinition> nodes,
            IEnumerable<MapGraphEdgeDefinition> edges, MapGraphLayoutConstraints constraints = null, string startNodeId = "")
        {
            Zones = new List<MapGraphZoneDefinition>(zones ?? throw new ArgumentNullException(nameof(zones))).AsReadOnly();
            Nodes = new List<MapGraphNodeDefinition>(nodes ?? throw new ArgumentNullException(nameof(nodes))).AsReadOnly();
            Edges = new List<MapGraphEdgeDefinition>(edges ?? throw new ArgumentNullException(nameof(edges))).AsReadOnly();
            Constraints = new MapGraphLayoutConstraints(constraints?.Alignments, constraints?.ExcludedConnections);
            StartNodeId = startNodeId ?? string.Empty;
            Graph = new MapGraphService(Nodes, Edges, StartNodeId, Zones, true);
        }

        public static MapGraphLayoutDraft FromDefinition(SO_MapGraphDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definition.IsCommandGraph) throw new ArgumentException("需要正式指挥图，不能隐式迁移旧 MVP。", nameof(definition));
            return new MapGraphLayoutDraft(definition.Zones, definition.Nodes, definition.Edges,
                definition.LayoutConstraints, definition.StartNodeId);
        }
    }
}
