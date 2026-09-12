using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>独立的几何和对齐验收；不相信求解器的成功标记，不查询导航或修改草稿。</summary>
    public static class MapGraphValidation
    {
        public static MapGraphValidationResult Validate(MapGraphLayoutDraft draft, MapGraphLayoutDraft original = null,
            MapGraphIntentPreservation preservation = MapGraphIntentPreservation.AllIntent)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var issues = new List<MapGraphValidationIssue>();
            if (!draft.Graph.IsValid)
            {
                foreach (string error in draft.Graph.ValidationErrors) issues.Add(new MapGraphValidationIssue("Topology", error));
                return new MapGraphValidationResult(issues);
            }
            CheckZonesAndNodes(draft, issues);
            CheckAlignments(draft, issues);
            CheckEdges(draft, issues);
            if (original != null) issues.AddRange(MapGraphLayoutIntentValidation.Validate(draft, original, preservation).Issues);
            return new MapGraphValidationResult(issues);
        }

        private static void CheckZonesAndNodes(MapGraphLayoutDraft draft, List<MapGraphValidationIssue> issues)
        {
            foreach (var zone in draft.Zones)
            {
                if (!MapGraphGeometry.Finite(zone.Bounds) || !MapGraphGeometry.Positive(zone.Bounds.size) ||
                    !MapGraphGeometry.Positive(zone.NameSafeSize))
                { issues.Add(new MapGraphValidationIssue("InvalidZoneGeometry", zone.ZoneId)); continue; }
                if (!MapGraphGeometry.Contains(zone.Bounds, zone.NameSafeBounds))
                    issues.Add(new MapGraphValidationIssue("NameOutsideZone", zone.ZoneId));
            }
            for (int i = 0; i < draft.Zones.Count; i++)
                for (int j = i + 1; j < draft.Zones.Count; j++)
                    if (MapGraphGeometry.Overlaps(draft.Zones[i].Bounds, draft.Zones[j].Bounds))
                        issues.Add(new MapGraphValidationIssue("ZoneOverlap", draft.Zones[i].ZoneId, draft.Zones[j].ZoneId));
            foreach (var node in draft.Nodes)
            {
                if (!MapGraphGeometry.Finite(draft.Graph.GetNodePosition(node.NodeId)) || !MapGraphGeometry.Positive(node.Footprint))
                { issues.Add(new MapGraphValidationIssue("InvalidNodeGeometry", node.NodeId)); continue; }
                var bounds = MapGraphGeometry.NodeBounds(draft, node);
                draft.Graph.TryGetZone(node.ZoneId, out var zone);
                if (!MapGraphGeometry.Contains(zone.Bounds, bounds))
                    issues.Add(new MapGraphValidationIssue("NodeOutsideZone", node.NodeId, zone.ZoneId));
                foreach (var area in draft.Zones)
                    if (MapGraphGeometry.Overlaps(bounds, area.NameSafeBounds))
                        issues.Add(new MapGraphValidationIssue("NodeOverName", node.NodeId, area.ZoneId));
            }
            for (int i = 0; i < draft.Nodes.Count; i++)
                for (int j = i + 1; j < draft.Nodes.Count; j++)
                    if (MapGraphGeometry.Overlaps(MapGraphGeometry.NodeBounds(draft, draft.Nodes[i]), MapGraphGeometry.NodeBounds(draft, draft.Nodes[j])))
                        issues.Add(new MapGraphValidationIssue("NodeOverlap", draft.Nodes[i].NodeId, draft.Nodes[j].NodeId));
        }

        private static void CheckAlignments(MapGraphLayoutDraft draft, List<MapGraphValidationIssue> issues)
        {
            var alignments = new Dictionary<string, MapGraphAlignmentConstraint>(StringComparer.Ordinal);
            foreach (var line in draft.Constraints.Alignments)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.Id) || !MapGraphGeometry.Finite(line.Coordinate) ||
                    line.Axis != MapGraphAxis.Horizontal && line.Axis != MapGraphAxis.Vertical)
                { issues.Add(new MapGraphValidationIssue("InvalidAlignment", line?.Id ?? "null")); continue; }
                if (!alignments.TryAdd(line.Id, line)) issues.Add(new MapGraphValidationIssue("DuplicateAlignment", line.Id));
            }
            foreach (var node in draft.Nodes)
            {
                Vector2 point = draft.Graph.GetNodePosition(node.NodeId);
                Check(node.RowId, MapGraphAxis.Horizontal, point.y);
                Check(node.ColumnId, MapGraphAxis.Vertical, point.x);
                void Check(string id, MapGraphAxis axis, float coordinate)
                {
                    if (!alignments.TryGetValue(id, out var line)) issues.Add(new MapGraphValidationIssue("MissingAlignment", node.NodeId, id));
                    else if (line.Axis != axis || !MapGraphGeometry.Near(line.Coordinate, coordinate))
                        issues.Add(new MapGraphValidationIssue("AlignmentMismatch", node.NodeId, id));
                }
            }
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in draft.Constraints.ExcludedConnections)
            {
                if (pair == null || pair.FirstNodeId == pair.SecondNodeId ||
                    !draft.Graph.TryGetNode(pair.FirstNodeId, out _) || !draft.Graph.TryGetNode(pair.SecondNodeId, out _))
                { issues.Add(new MapGraphValidationIssue("InvalidExclusion", pair?.FirstNodeId ?? "null", pair?.SecondNodeId ?? "")); continue; }
                if (!excluded.Add(MapGraphGeometry.PairKey(pair.FirstNodeId, pair.SecondNodeId)))
                    issues.Add(new MapGraphValidationIssue("DuplicateExclusion", pair.FirstNodeId, pair.SecondNodeId));
            }
        }

        private static void CheckEdges(MapGraphLayoutDraft draft, List<MapGraphValidationIssue> issues)
        {
            var segments = new List<(MapGraphEdgeDefinition edge, Vector2 from, Vector2 to)>();
            var ports = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var edge in draft.Edges)
            {
                draft.Graph.TryGetNode(edge.FromNodeId, out var a); draft.Graph.TryGetNode(edge.ToNodeId, out var b);
                Vector2 first = draft.Graph.GetNodePosition(a.NodeId), second = draft.Graph.GetNodePosition(b.NodeId);
                if (!MapGraphGeometry.Finite(edge.WidthOverride) || edge.WidthOverride < 0 ||
                    !MapGraphGeometry.Finite(edge.FromInset) || !MapGraphGeometry.Finite(edge.ToInset) || edge.FromInset < 0 || edge.ToInset < 0 ||
                    edge.UseColorOverride && !Finite(edge.ColorOverride))
                    issues.Add(new MapGraphValidationIssue("InvalidEdgeStyle", edge.EdgeId));
                if (draft.Constraints.IsExcluded(a.NodeId, b.NodeId))
                    issues.Add(new MapGraphValidationIssue("ExcludedConnection", edge.EdgeId, a.NodeId + "/" + b.NodeId));
                bool horizontal = edge.Axis == MapGraphAxis.Horizontal, vertical = edge.Axis == MapGraphAxis.Vertical;
                if (!horizontal && !vertical || horizontal && !MapGraphGeometry.Near(first.y, second.y) ||
                    vertical && !MapGraphGeometry.Near(first.x, second.x))
                { issues.Add(new MapGraphValidationIssue("NonOrthogonalEdge", edge.EdgeId)); continue; }
                if (horizontal ? a.RowId != b.RowId : a.ColumnId != b.ColumnId)
                    issues.Add(new MapGraphValidationIssue("EdgeAlignmentNotShared", edge.EdgeId));
                if (!MapGraphGeometry.TryGetVisibleSegment(draft, edge, out var from, out var to))
                { issues.Add(new MapGraphValidationIssue("InvalidEdgeSpan", edge.EdgeId)); continue; }
                segments.Add((edge, from, to));
                int direction = horizontal ? to.x > from.x ? 0 : 1 : to.y > from.y ? 2 : 3;
                UsePort(a.NodeId, direction); UsePort(b.NodeId, direction ^ 1);
                void UsePort(string nodeId, int port)
                {
                    string key = nodeId + "/" + port;
                    if (ports.TryGetValue(key, out string previous))
                        issues.Add(new MapGraphValidationIssue("PortOverlap", edge.EdgeId, previous, nodeId));
                    else ports.Add(key, edge.EdgeId);
                }
                float width = edge.WidthOverride > 0 ? edge.WidthOverride : 2;
                float portSize = horizontal ? Mathf.Min(a.Footprint.y, b.Footprint.y) : Mathf.Min(a.Footprint.x, b.Footprint.x);
                if (width > portSize + MapGraphGeometry.Epsilon)
                    issues.Add(new MapGraphValidationIssue("EdgeWiderThanPort", edge.EdgeId));
                foreach (var node in draft.Nodes)
                    if (node.NodeId != a.NodeId && node.NodeId != b.NodeId &&
                        MapGraphGeometry.SegmentIntersectsRect(from, to, MapGraphGeometry.NodeBounds(draft, node), width))
                        issues.Add(new MapGraphValidationIssue("EdgeThroughNode", edge.EdgeId, node.NodeId));
                foreach (var zone in draft.Zones)
                    if (MapGraphGeometry.SegmentIntersectsRect(from, to, zone.NameSafeBounds, width))
                        issues.Add(new MapGraphValidationIssue("EdgeThroughName", edge.EdgeId, zone.ZoneId));
            }
            for (int i = 0; i < segments.Count; i++)
                for (int j = i + 1; j < segments.Count; j++)
                    if (MapGraphGeometry.CollinearOverlap(segments[i].from, segments[i].to, segments[j].from, segments[j].to))
                        issues.Add(new MapGraphValidationIssue("CollinearEdges", segments[i].edge.EdgeId, segments[j].edge.EdgeId));
        }

        private static bool Finite(Color color) => MapGraphGeometry.Finite(color.r) && MapGraphGeometry.Finite(color.g) &&
            MapGraphGeometry.Finite(color.b) && MapGraphGeometry.Finite(color.a);
    }
}
