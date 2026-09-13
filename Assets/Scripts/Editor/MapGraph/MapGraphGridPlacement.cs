using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>纯网格坐标操作，不查询导航，不维持旧线路，不改变场景物体。</summary>
    public static class MapGraphGridPlacement
    {
        public static float ValidSpacing(float spacing)
        {
            if (!MapGraphGeometry.Finite(spacing) || spacing < 1 || spacing > 10000) throw new ArgumentOutOfRangeException(nameof(spacing), "网格间距应在 1～10000 之间。");
            return spacing;
        }
        public static Vector2 Snap(Vector2 point, float spacing)
        {
            ValidSpacing(spacing);
            if (!MapGraphGeometry.Finite(point)) throw new ArgumentException("坐标必须有限。", nameof(point));
            return new Vector2((float)Math.Round(point.x / spacing, MidpointRounding.AwayFromZero) * spacing,
                (float)Math.Round(point.y / spacing, MidpointRounding.AwayFromZero) * spacing);
        }
        public static MapGraphLayoutDraft MoveNode(MapGraphLayoutDraft draft, string id, Vector2 point, float spacing)
        {
            if (!draft.Graph.TryGetNode(id, out _)) throw new ArgumentException("群已不存在：" + id);
            return WithPositions(draft, new Dictionary<string, Vector2> { [id] = Snap(point, spacing) });
        }
        public static MapGraphLayoutDraft MoveZone(MapGraphLayoutDraft draft, string id, Rect bounds, float spacing, bool resize = false)
        {
            if (!draft.Graph.TryGetZone(id, out var zone)) throw new ArgumentException("区域已不存在：" + id);
            Vector2 delta = Snap(bounds.position - zone.Bounds.position, spacing);
            Rect moved = resize ? Rect.MinMaxRect(Snap(bounds.min, spacing).x, Snap(bounds.min, spacing).y,
                Snap(bounds.max, spacing).x, Snap(bounds.max, spacing).y) : new Rect(zone.Bounds.position + delta, zone.Bounds.size);
            if (!MapGraphGeometry.Finite(moved) || !MapGraphGeometry.Positive(moved.size)) throw new ArgumentException("区域尺寸必须为正数。");
            var positions = draft.Nodes.ToDictionary(n => n.NodeId, n => draft.Graph.GetNodePosition(n.NodeId) + (!resize && n.ZoneId == id ? delta : Vector2.zero));
            var zones = draft.Zones.Select(z => z.ZoneId == id ? z.WithLayout(moved, z.LayoutLocked) : z).ToArray();
            return WithPositions(draft, positions, zones);
        }
        public static MapGraphLayoutDraft WithPositions(MapGraphLayoutDraft draft, IReadOnlyDictionary<string, Vector2> positions,
            IReadOnlyList<MapGraphZoneDefinition> zones = null)
        {
            zones ??= draft.Zones;
            var alignments = new List<MapGraphAlignmentConstraint>();
            string Line(float coordinate, MapGraphAxis axis)
            {
                var existing = alignments.FirstOrDefault(a => a.Axis == axis && MapGraphGeometry.Near(a.Coordinate, coordinate));
                if (existing != null) return existing.Id;
                var previous = draft.Constraints.Alignments.Where(a => a.Axis == axis && MapGraphGeometry.Near(a.Coordinate, coordinate))
                    .OrderByDescending(a => a.Locked).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
                string id = previous?.Id ?? "grid-" + axis + "-" + coordinate.ToString("R", CultureInfo.InvariantCulture);
                alignments.Add(new MapGraphAlignmentConstraint(id, axis, coordinate, previous?.Locked ?? false)); return id;
            }
            var nodes = new List<MapGraphNodeDefinition>();
            foreach (var node in draft.Nodes)
            {
                var p = positions != null && positions.TryGetValue(node.NodeId, out var value) ? value : draft.Graph.GetNodePosition(node.NodeId);
                if (!MapGraphGeometry.Finite(p)) throw new ArgumentException("坐标必须有限：" + node.NodeId);
                var zone = zones.First(z => z.ZoneId == node.ZoneId);
                nodes.Add(node.WithLayout(p - zone.Bounds.center, Line(p.y, MapGraphAxis.Horizontal), Line(p.x, MapGraphAxis.Vertical), node.PositionLocked));
            }
            foreach (var previous in draft.Constraints.Alignments)
                if (previous.Locked && !alignments.Any(a => a.Id == previous.Id)) alignments.Add(previous);
            return new MapGraphLayoutDraft(zones, nodes, draft.Edges, new MapGraphLayoutConstraints(alignments, draft.Constraints.ExcludedConnections), draft.StartNodeId);
        }
    }
}
