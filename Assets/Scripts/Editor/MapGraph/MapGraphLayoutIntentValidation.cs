using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    [Flags]
    public enum MapGraphIntentPreservation
    {
        None = 0, Locks = 1, ManualConnections = 2, Exclusions = 4, Topology = 8,
        AllIntent = Locks | ManualConnections | Exclusions
    }

    /// <summary>检查再生成是否保留作者意图；显式编辑可仅选择本次必须保留的规则。</summary>
    public static class MapGraphLayoutIntentValidation
    {
        public static MapGraphValidationResult Validate(MapGraphLayoutDraft draft, MapGraphLayoutDraft original,
            MapGraphIntentPreservation preservation = MapGraphIntentPreservation.AllIntent)
        {
            if (draft == null || original == null) throw new ArgumentNullException(draft == null ? nameof(draft) : nameof(original));
            var issues = new List<MapGraphValidationIssue>();
            if (!draft.Graph.IsValid || !original.Graph.IsValid)
                return new MapGraphValidationResult(new[] { new MapGraphValidationIssue("InvalidIntentGraph", "graph") });
            foreach (var node in original.Nodes)
            {
                bool exists = draft.Graph.TryGetNode(node.NodeId, out var current);
                if (exists && (node.SourceObjectId != current.SourceObjectId || node.ZoneId != current.ZoneId || node.NodeKind != current.NodeKind))
                    issues.Add(new MapGraphValidationIssue("NodeIdentityChanged", node.NodeId));
                if (Has(MapGraphIntentPreservation.Locks) && node.PositionLocked && (!exists || !current.PositionLocked ||
                    !MapGraphGeometry.Near(draft.Graph.GetNodePosition(node.NodeId), original.Graph.GetNodePosition(node.NodeId))))
                    issues.Add(new MapGraphValidationIssue("LockedNodeChanged", node.NodeId));
                if (Has(MapGraphIntentPreservation.Topology) && !exists)
                    issues.Add(new MapGraphValidationIssue("TopologyNodeRemoved", node.NodeId));
                if (exists && Has(MapGraphIntentPreservation.Locks))
                    foreach (var line in original.Constraints.Alignments)
                        if (line != null && line.Locked &&
                            (node.RowId == line.Id && current.RowId != line.Id || node.ColumnId == line.Id && current.ColumnId != line.Id))
                            issues.Add(new MapGraphValidationIssue("LockedAlignmentMembershipChanged", node.NodeId, line.Id));
            }
            foreach (var zone in original.Zones)
            {
                bool exists = draft.Graph.TryGetZone(zone.ZoneId, out var current);
                if (exists && (zone.SourceObjectId != current.SourceObjectId || zone.IsSynthetic != current.IsSynthetic))
                    issues.Add(new MapGraphValidationIssue("ZoneIdentityChanged", zone.ZoneId));
                if (Has(MapGraphIntentPreservation.Locks) && zone.LayoutLocked && (!exists || !current.LayoutLocked ||
                    !MapGraphGeometry.Near(zone.Bounds.position, current.Bounds.position) ||
                    !MapGraphGeometry.Near(zone.Bounds.size, current.Bounds.size) || !MapGraphGeometry.Near(zone.NameSafeSize, current.NameSafeSize)))
                    issues.Add(new MapGraphValidationIssue("LockedZoneChanged", zone.ZoneId));
            }
            if (Has(MapGraphIntentPreservation.Locks))
                foreach (var line in original.Constraints.Alignments)
                {
                    if (line == null || !line.Locked) continue;
                    MapGraphAlignmentConstraint current = null;
                    foreach (var candidate in draft.Constraints.Alignments) if (candidate != null && candidate.Id == line.Id) { current = candidate; break; }
                    if (current == null || !current.Locked || current.Axis != line.Axis || !MapGraphGeometry.Near(current.Coordinate, line.Coordinate))
                        issues.Add(new MapGraphValidationIssue("LockedAlignmentChanged", line.Id));
                }
            foreach (var edge in original.Edges)
            {
                bool exists = draft.Graph.TryGetEdge(edge.EdgeId, out var current);
                bool sameEndpoints = exists && MapGraphGeometry.PairKey(edge.FromNodeId, edge.ToNodeId) ==
                    MapGraphGeometry.PairKey(current.FromNodeId, current.ToNodeId);
                if (Has(MapGraphIntentPreservation.Topology) && !sameEndpoints)
                    issues.Add(new MapGraphValidationIssue("TopologyEdgeChanged", edge.EdgeId));
                if (Has(MapGraphIntentPreservation.ManualConnections) && IsPinnedEdge(edge) &&
                    (!sameEndpoints || current.FromNodeId != edge.FromNodeId || current.ToNodeId != edge.ToNodeId ||
                     current.Axis != edge.Axis || current.Origin != edge.Origin ||
                     !MapGraphGeometry.Near(current.FromInset, edge.FromInset) || !MapGraphGeometry.Near(current.ToInset, edge.ToInset) ||
                     !MapGraphGeometry.Near(current.WidthOverride, edge.WidthOverride) || current.UseColorOverride != edge.UseColorOverride ||
                     current.UseColorOverride && current.ColorOverride != edge.ColorOverride))
                    issues.Add(new MapGraphValidationIssue("ManualEdgeChanged", edge.EdgeId));
            }
            if (Has(MapGraphIntentPreservation.Topology))
            {
                foreach (var node in draft.Nodes) if (!original.Graph.TryGetNode(node.NodeId, out _))
                    issues.Add(new MapGraphValidationIssue("TopologyNodeAdded", node.NodeId));
                foreach (var edge in draft.Edges) if (!original.Graph.TryGetEdge(edge.EdgeId, out _))
                    issues.Add(new MapGraphValidationIssue("TopologyEdgeAdded", edge.EdgeId));
            }
            if (Has(MapGraphIntentPreservation.Exclusions))
                foreach (var pair in original.Constraints.ExcludedConnections)
                    if (pair != null && !draft.Constraints.IsExcluded(pair.FirstNodeId, pair.SecondNodeId))
                        issues.Add(new MapGraphValidationIssue("ExclusionRemoved", pair.FirstNodeId, pair.SecondNodeId));
            return new MapGraphValidationResult(issues);
            bool Has(MapGraphIntentPreservation flag) => (preservation & flag) != 0;
        }

        public static bool IsPinnedEdge(MapGraphEdgeDefinition edge)
            => edge.Origin == MapGraphEdgeOrigin.Manual || edge.FromInset != 0 || edge.ToInset != 0 ||
               edge.WidthOverride != 0 || edge.UseColorOverride;
    }
}
