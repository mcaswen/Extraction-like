using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>显式作者操作，只生成意图，不求解、不写对象。</summary>
    public static class MapGraphEditOperations
    {
        public static MapGraphLayoutDraft MoveNode(MapGraphLayoutDraft graph, string id, Vector2 position)
        {
            if (!graph.Graph.TryGetNode(id, out var node)) throw new ArgumentException("UnknownNode:" + id);
            graph.Graph.TryGetZone(node.ZoneId, out var zone);
            return Copy(graph, nodes: graph.Nodes.Select(n => n.NodeId == id ? n.WithLayout(position - zone.Bounds.center, n.RowId, n.ColumnId, true) : n));
        }
        public static MapGraphLayoutDraft MoveZone(MapGraphLayoutDraft graph, string id, Rect bounds)
        {
            if (!graph.Graph.TryGetZone(id, out _)) throw new ArgumentException("UnknownZone:" + id);
            return Copy(graph, zones: graph.Zones.Select(z => z.ZoneId == id ? z.WithLayout(bounds, true) : z));
        }
        public static MapGraphLayoutDraft LockNode(MapGraphLayoutDraft graph, string id, bool locked)
            => Copy(graph, nodes: graph.Nodes.Select(n => n.NodeId == id ? n.WithLayout(n.Position, n.RowId, n.ColumnId, locked) : n));
        public static MapGraphLayoutDraft LockZone(MapGraphLayoutDraft graph, string id, bool locked)
            => Copy(graph, zones: graph.Zones.Select(z => z.ZoneId == id ? z.WithLayout(z.Bounds, locked) : z));
        public static MapGraphLayoutDraft LockAlignment(MapGraphLayoutDraft graph, string id, bool locked)
            => Copy(graph, constraints: new MapGraphLayoutConstraints(graph.Constraints.Alignments.Select(a => a.Id == id ? new MapGraphAlignmentConstraint(a.Id, a.Axis, a.Coordinate, locked) : a), graph.Constraints.ExcludedConnections));

        public static MapGraphLayoutDraft AddEdge(MapGraphLayoutDraft graph, string from, string to, MapGraphAxis axis)
        {
            CheckEndpoints(graph, from, to, axis);
            if (graph.Graph.TryGetEdgeBetween(from, to, out _)) throw new ArgumentException("ConnectionAlreadyExists");
            var edge = new MapGraphEdgeDefinition(MapGraphSceneNavigationScan.ConnectionId(from, to), from, to, 1, axis, MapGraphEdgeOrigin.Manual);
            return Copy(graph, edges: graph.Edges.Concat(new[] { edge }), constraints: Exclusions(graph, null, null, from, to));
        }
        public static MapGraphLayoutDraft DeleteEdge(MapGraphLayoutDraft graph, string id)
        {
            if (!graph.Graph.TryGetEdge(id, out var edge)) throw new ArgumentException("UnknownEdge:" + id);
            return Copy(graph, edges: graph.Edges.Where(e => e.EdgeId != id), constraints: Exclusions(graph, edge.FromNodeId, edge.ToNodeId, null, null));
        }
        public static MapGraphLayoutDraft RebindEdge(MapGraphLayoutDraft graph, string id, string from, string to, MapGraphAxis axis)
        {
            CheckEndpoints(graph, from, to, axis);
            if (!graph.Graph.TryGetEdge(id, out var edge)) throw new ArgumentException("UnknownEdge:" + id);
            if (graph.Graph.TryGetEdgeBetween(from, to, out var other) && other.EdgeId != id) throw new ArgumentException("ConnectionAlreadyExists");
            var changed = new MapGraphEdgeDefinition(id, from, to, edge.LengthUnits, axis, MapGraphEdgeOrigin.Manual,
                edge.FromInset, edge.ToInset, edge.WidthOverride, edge.UseColorOverride, edge.ColorOverride);
            return Copy(graph, edges: graph.Edges.Select(e => e.EdgeId == id ? changed : e), constraints: Exclusions(graph, edge.FromNodeId, edge.ToNodeId, from, to));
        }
        public static MapGraphLayoutDraft StyleEdge(MapGraphLayoutDraft graph, string id, float fromInset, float toInset, float width, bool useColor, Color color)
            => Copy(graph, edges: graph.Edges.Select(e => e.EdgeId == id ? e.WithPresentation(e.Axis, fromInset, toInset, width, useColor, color, e.Origin) : e));

        private static void CheckEndpoints(MapGraphLayoutDraft graph, string from, string to, MapGraphAxis axis)
        {
            if (from == to || !graph.Graph.TryGetNode(from, out _) || !graph.Graph.TryGetNode(to, out _) || axis == MapGraphAxis.Unspecified)
                throw new ArgumentException("InvalidConnectionEndpoints");
        }
        private static MapGraphLayoutConstraints Exclusions(MapGraphLayoutDraft graph, string addA, string addB, string removeA, string removeB)
        {
            var pairs = graph.Constraints.ExcludedConnections.ToList();
            if (addA != null && !graph.Constraints.IsExcluded(addA, addB)) pairs.Add(new MapGraphConnectionExclusion(addA, addB));
            if (removeA != null) pairs.RemoveAll(p => MapGraphGeometry.PairKey(p.FirstNodeId, p.SecondNodeId) == MapGraphGeometry.PairKey(removeA, removeB));
            return new MapGraphLayoutConstraints(graph.Constraints.Alignments, pairs);
        }
        private static MapGraphLayoutDraft Copy(MapGraphLayoutDraft graph, IEnumerable<MapGraphZoneDefinition> zones = null,
            IEnumerable<MapGraphNodeDefinition> nodes = null, IEnumerable<MapGraphEdgeDefinition> edges = null, MapGraphLayoutConstraints constraints = null)
            => new MapGraphLayoutDraft(zones ?? graph.Zones, nodes ?? graph.Nodes, edges ?? graph.Edges, constraints ?? graph.Constraints, graph.StartNodeId);
    }
}
