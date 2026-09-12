using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>建立世界参考和回写地图坐标；不搜索方向，不修改逻辑边或场景。</summary>
    public static class MapGraphLayoutGenerator
    {
        public static MapGraphLayoutDraft CreateReference(MapGraphSceneSnapshot scene, MapGraphGenerationSettings settings,
            MapGraphLayoutDraft previous = null)
        {
            if (scene == null || settings == null) throw new ArgumentNullException();
            if (!scene.IsValid) throw new ArgumentException("请先处理场景采集诊断。", nameof(scene));
            if (!MapGraphGeometry.Positive(settings.MinimumZoneSize) || !MapGraphGeometry.Positive(settings.NameSafeSize))
                throw new ArgumentException("区域和名称最小尺寸必须为有限正数。", nameof(settings));
            float scale = settings.WorldToMapScale;
            var zones = new List<MapGraphZoneDefinition>();
            var zoneIndex = new Dictionary<string, MapGraphZoneDefinition>(StringComparer.Ordinal);
            foreach (var source in scene.Zones)
            {
                MapGraphZoneDefinition zone;
                if (previous != null && previous.Graph.TryGetZone(source.Id, out var saved)) zone = saved;
                else
                {
                    bool empty = true;
                    foreach (var node in scene.Nodes) if (node.ZoneId == source.Id) { empty = false; break; }
                    Vector2 minimum = empty ? settings.NameSafeSize + Vector2.one * settings.ZonePadding * 2 : settings.MinimumZoneSize;
                    Vector2 size = Vector2.Max(minimum, source.WorldBounds.size * scale + Vector2.one * settings.ZonePadding * 2);
                    var bounds = new Rect(source.WorldBounds.center * scale - size * 0.5f, size);
                    zone = new MapGraphZoneDefinition(source.Id, DisplayZoneName(source.Name), bounds, settings.NameSafeSize, false, source.SourceObjectId);
                }
                zones.Add(zone); zoneIndex.Add(zone.ZoneId, zone);
            }
            var nodes = new List<MapGraphNodeDefinition>();
            var lines = new List<MapGraphAlignmentConstraint>();
            foreach (var source in scene.Nodes)
            {
                if (!zoneIndex.TryGetValue(source.ZoneId, out var zone)) throw new ArgumentException("未分区群：" + source.HierarchyPath);
                MapGraphNodeDefinition node;
                if (previous != null && previous.Graph.TryGetNode(source.Id, out var saved))
                {
                    if (saved.SourceObjectId != source.SourceObjectId || saved.ZoneId != source.ZoneId || saved.NodeKind != source.Kind)
                        throw new ArgumentException("群身份或归属变化，需先审查同步差异：" + source.HierarchyPath);
                    Vector2 point = previous.Graph.GetNodePosition(saved.NodeId);
                    node = saved.WithLayout(point - zone.Bounds.center, saved.RowId, saved.ColumnId, saved.PositionLocked);
                }
                else
                {
                    float physicalSize = Mathf.Sqrt(Mathf.Max(0, source.WorldBounds.width * source.WorldBounds.height)) * scale * 0.25f;
                    float diameter = Mathf.Clamp(physicalSize, settings.NodeDiameter, settings.NodeDiameter * 1.5f);
                    var position = new Vector2(source.WorldCenter.x, source.WorldCenter.z) * scale;
                    node = new MapGraphNodeDefinition(source.Id, source.Kind, position - zone.Bounds.center, source.Name,
                        description: source.HierarchyPath, zoneId: source.ZoneId, footprint: Vector2.one * diameter,
                        rowId: "reference-row-" + source.Id, columnId: "reference-column-" + source.Id, sourceObjectId: source.SourceObjectId);
                }
                nodes.Add(node);
                Vector2 global = node.Position + zone.Bounds.center;
                AddLine(node.RowId, MapGraphAxis.Horizontal, global.y);
                AddLine(node.ColumnId, MapGraphAxis.Vertical, global.x);
            }
            return new MapGraphLayoutDraft(zones, nodes, previous?.Edges ?? Array.Empty<MapGraphEdgeDefinition>(),
                new MapGraphLayoutConstraints(lines, previous?.Constraints.ExcludedConnections), previous?.StartNodeId ?? "");

            void AddLine(string id, MapGraphAxis axis, float coordinate)
            {
                foreach (var line in lines) if (line.Id == id) return;
                if (previous != null)
                    foreach (var saved in previous.Constraints.Alignments)
                        if (saved.Id == id) { lines.Add(saved); return; }
                lines.Add(new MapGraphAlignmentConstraint(id, axis, coordinate));
            }
        }

        internal static MapGraphLayoutDraft FromCoordinates(MapGraphLayoutDraft reference, Vector2[] positions,
            MapGraphZoneDefinition[] zones, MapGraphEdgeDefinition[] edges, int[] columns, int[] rows)
        {
            var zoneIndex = new Dictionary<string, MapGraphZoneDefinition>(StringComparer.Ordinal);
            foreach (var zone in zones) zoneIndex.Add(zone.ZoneId, zone);
            var oldLines = new Dictionary<string, MapGraphAlignmentConstraint>(StringComparer.Ordinal);
            foreach (var line in reference.Constraints.Alignments) oldLines[line.Id] = line;
            var lines = new Dictionary<string, MapGraphAlignmentConstraint>(StringComparer.Ordinal);
            var nodes = new List<MapGraphNodeDefinition>();
            for (int i = 0; i < reference.Nodes.Count; i++)
            {
                var source = reference.Nodes[i];
                string row = GroupId(i, rows, true), column = GroupId(i, columns, false);
                Add(row, MapGraphAxis.Horizontal, positions[i].y); Add(column, MapGraphAxis.Vertical, positions[i].x);
                nodes.Add(source.WithLayout(positions[i] - zoneIndex[source.ZoneId].Bounds.center, row, column, source.PositionLocked));
            }
            foreach (var line in reference.Constraints.Alignments) if (line.Locked && !lines.ContainsKey(line.Id)) lines.Add(line.Id, line);
            return new MapGraphLayoutDraft(zones, nodes, edges, new MapGraphLayoutConstraints(lines.Values,
                reference.Constraints.ExcludedConnections), reference.StartNodeId);

            string GroupId(int index, int[] groups, bool row)
            {
                string minimum = reference.Nodes[index].NodeId, lockedId = null;
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[index] != groups[i]) continue;
                    var node = reference.Nodes[i];
                    if (string.CompareOrdinal(node.NodeId, minimum) < 0) minimum = node.NodeId;
                    string id = row ? node.RowId : node.ColumnId;
                    if (oldLines.TryGetValue(id, out var line) && line.Locked &&
                        (lockedId == null || string.CompareOrdinal(id, lockedId) < 0)) lockedId = id;
                }
                return lockedId ?? (row ? "row-" : "column-") + minimum;
            }
            void Add(string id, MapGraphAxis axis, float coordinate)
            {
                if (!lines.ContainsKey(id)) lines.Add(id, new MapGraphAlignmentConstraint(id, axis, coordinate,
                    oldLines.TryGetValue(id, out var old) && old.Locked));
            }
        }

        private static string DisplayZoneName(string name)
        {
            if (name != null && (name.StartsWith("Zone-", StringComparison.Ordinal) || name.StartsWith("Zone_", StringComparison.Ordinal)))
                return name.Substring(5);
            return name;
        }
    }
}
