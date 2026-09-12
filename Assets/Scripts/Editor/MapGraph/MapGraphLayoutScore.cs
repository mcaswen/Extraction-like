using System;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>只比较地图布局保真和可读性，数值不作为 Agent 的导航边权。</summary>
    public sealed class MapGraphLayoutScore
    {
        public double NormalizedDisplacement { get; private set; }
        public int DirectionReversals { get; private set; }
        public int DiagonalFlattening { get; private set; }
        public double DistanceDistortion { get; private set; }
        public double ZoneAreaChange { get; private set; }
        public int Crossings { get; private set; }
        public double BoundsArea { get; private set; }
        public int HorizontalEdges { get; private set; }
        public int VerticalEdges { get; private set; }
        public double Total { get; private set; }
        public bool IsFinite => !double.IsNaN(Total) && !double.IsInfinity(Total);

        public static MapGraphLayoutScore Evaluate(MapGraphLayoutDraft draft, MapGraphLayoutDraft reference,
            MapGraphGenerationSettings settings)
        {
            if (draft == null || reference == null || settings == null) throw new ArgumentNullException();
            var result = new MapGraphLayoutScore { Total = double.PositiveInfinity };
            if (!draft.Graph.IsValid || !reference.Graph.IsValid || draft.Nodes.Count != reference.Nodes.Count) return result;
            double grid = settings.GridSpacing, weightedDistance = 0, weights = 0;
            int pairs = 0;
            foreach (var node in draft.Nodes)
            {
                if (!reference.Graph.TryGetNode(node.NodeId, out _)) return result;
                Vector2 point = draft.Graph.GetNodePosition(node.NodeId), origin = reference.Graph.GetNodePosition(node.NodeId);
                if (!MapGraphGeometry.Finite(point) || !MapGraphGeometry.Finite(origin)) return result;
                result.NormalizedDisplacement += SquaredDistance(point, origin) / (grid * grid);
            }
            result.NormalizedDisplacement /= Math.Max(1, draft.Nodes.Count);
            for (int i = 0; i < draft.Nodes.Count; i++)
                for (int j = i + 1; j < draft.Nodes.Count; j++)
                {
                    string a = draft.Nodes[i].NodeId, b = draft.Nodes[j].NodeId;
                    Vector2 actual = draft.Graph.GetNodePosition(b) - draft.Graph.GetNodePosition(a);
                    Vector2 expected = reference.Graph.GetNodePosition(b) - reference.Graph.GetNodePosition(a);
                    if ((double)actual.x * expected.x < -MapGraphGeometry.Epsilon) result.DirectionReversals++;
                    if ((double)actual.y * expected.y < -MapGraphGeometry.Epsilon) result.DirectionReversals++;
                    if (Mathf.Abs(expected.x) > MapGraphGeometry.Epsilon && Mathf.Abs(expected.y) > MapGraphGeometry.Epsilon &&
                        (Mathf.Abs(actual.x) <= MapGraphGeometry.Epsilon || Mathf.Abs(actual.y) <= MapGraphGeometry.Epsilon)) result.DiagonalFlattening++;
                    double distance = Math.Sqrt(SquaredDistance(actual, Vector2.zero));
                    double baseline = Math.Sqrt(SquaredDistance(expected, Vector2.zero));
                    double weight = 1 / (1 + baseline / grid), relativeError = (distance - baseline) / Math.Max(grid, baseline);
                    weightedDistance += relativeError * relativeError * weight; weights += weight; pairs++;
                }
            result.DistanceDistortion = weightedDistance / Math.Max(1, weights);
            foreach (var zone in draft.Zones)
            {
                if (!reference.Graph.TryGetZone(zone.ZoneId, out var original) || !MapGraphGeometry.Positive(zone.Bounds.size) ||
                    !MapGraphGeometry.Positive(original.Bounds.size)) return result;
                result.ZoneAreaChange += Math.Abs(Math.Log(Area(zone.Bounds) / Area(original.Bounds)));
            }
            result.ZoneAreaChange /= Math.Max(1, draft.Zones.Count);
            for (int i = 0; i < draft.Edges.Count; i++)
            {
                var edge = draft.Edges[i];
                if (edge.Axis == MapGraphAxis.Horizontal) result.HorizontalEdges++;
                if (edge.Axis == MapGraphAxis.Vertical) result.VerticalEdges++;
                if (!MapGraphGeometry.TryGetVisibleSegment(draft, edge, out var a, out var b)) continue;
                for (int j = i + 1; j < draft.Edges.Count; j++)
                    if (MapGraphGeometry.TryGetVisibleSegment(draft, draft.Edges[j], out var c, out var d) &&
                        MapGraphGeometry.ProperCrossing(a, b, c, d)) result.Crossings++;
            }
            result.BoundsArea = Area(BoundsOf(draft));
            double compactness = result.BoundsArea / Math.Max(1, Area(BoundsOf(reference)));
            result.Total = settings.PositionWeight * result.NormalizedDisplacement +
                settings.DirectionWeight * (result.DirectionReversals * 4d + result.DiagonalFlattening * 0.2d) / Math.Max(1, pairs) +
                settings.DistanceWeight * result.DistanceDistortion + settings.AreaWeight * (result.ZoneAreaChange + compactness * 0.1) +
                settings.CrossingWeight * result.Crossings;
            return result;
        }

        public static Rect BoundsOf(MapGraphLayoutDraft draft)
        {
            Rect bounds = default; bool first = true;
            foreach (var zone in draft.Zones)
            { bounds = first ? zone.Bounds : MapGraphGeometry.Union(bounds, zone.Bounds); first = false; }
            return bounds;
        }
        private static double Area(Rect rect) => (double)rect.width * rect.height;
        private static double SquaredDistance(Vector2 a, Vector2 b)
        { double x = (double)a.x - b.x, y = (double)a.y - b.y; return x * x + y * y; }
    }
}
