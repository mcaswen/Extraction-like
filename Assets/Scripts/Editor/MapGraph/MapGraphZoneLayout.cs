using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>只负责矩形包含和中央名称空位，中心候选有界，不移动场景对象或改变节点归属。</summary>
    internal static class MapGraphZoneLayout
    {
        private static readonly float[] CenterOffsets = { 0, 0.5f, -0.5f, 1, -1, 2, -2 };

        internal static bool TryFit(MapGraphZoneDefinition reference, IReadOnlyList<MapGraphNodeDefinition> nodes,
            Vector2[] positions, List<int> members, (Vector2 from, Vector2 to, float width)[] segments,
            MapGraphGenerationSettings settings, out MapGraphZoneDefinition result)
        {
            result = null;
            if (members.Count == 0) { result = reference; return NameClear(reference.NameSafeBounds, nodes, positions, segments); }
            if (reference.LayoutLocked)
            {
                foreach (int index in members)
                    if (!MapGraphGeometry.Contains(reference.Bounds, new Rect(positions[index] - nodes[index].Footprint * 0.5f, nodes[index].Footprint))) return false;
                if (!NameClear(reference.NameSafeBounds, nodes, positions, segments)) return false;
                result = reference; return true;
            }
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (int index in members)
            {
                min = Vector2.Min(min, positions[index] - nodes[index].Footprint * 0.5f);
                max = Vector2.Max(max, positions[index] + nodes[index].Footprint * 0.5f);
            }
            Vector2 midpoint = (min + max) * 0.5f;
            Vector2 minimum = Vector2.Max(settings.MinimumZoneSize, reference.NameSafeSize + Vector2.one * settings.ZonePadding * 2);
            double best = double.PositiveInfinity;
            foreach (float x in CenterOffsets)
                foreach (float y in CenterOffsets)
                {
                    Vector2 center = midpoint + new Vector2(x, y) * settings.GridSpacing;
                    Vector2 halfSize = Vector2.Max(max - center, center - min) + Vector2.one * settings.ZonePadding;
                    var rect = new Rect(center - Vector2.Max(halfSize * 2, minimum) * 0.5f, Vector2.Max(halfSize * 2, minimum));
                    var name = new Rect(center - reference.NameSafeSize * 0.5f, reference.NameSafeSize);
                    if (!MapGraphGeometry.Finite(rect) || !NameClear(name, nodes, positions, segments)) continue;
                    double cost = (double)rect.width * rect.height + (center - midpoint).sqrMagnitude * 0.5;
                    if (cost >= best) continue;
                    best = cost; result = reference.WithLayout(rect, false);
                }
            return result != null;
        }

        private static bool NameClear(Rect name, IReadOnlyList<MapGraphNodeDefinition> nodes, Vector2[] positions,
            (Vector2 from, Vector2 to, float width)[] segments)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (MapGraphGeometry.Overlaps(name, new Rect(positions[i] - nodes[i].Footprint * 0.5f, nodes[i].Footprint))) return false;
            foreach (var segment in segments)
                if (MapGraphGeometry.SegmentIntersectsRect(segment.from, segment.to, name, segment.width)) return false;
            return true;
        }
    }
}
