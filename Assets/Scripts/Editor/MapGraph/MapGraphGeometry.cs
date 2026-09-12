using System;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>Editor 平面几何原语，不查询场景，不增加节点或决定逻辑连接。</summary>
    public static class MapGraphGeometry
    {
        // 仅容纳 Zone 中心与局部坐标相加的浮点误差；边还必须共享同一个行/列约束。
        public const float Epsilon = 0.001f;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        public static bool Finite(Rect value) => Finite(value.position) && Finite(value.size) && Finite(value.max);
        public static bool Positive(Vector2 value) => Finite(value) && value.x > 0 && value.y > 0;
        public static bool Near(float a, float b) => Finite(a) && Finite(b) && Mathf.Abs(a - b) <= Epsilon;
        public static bool Near(Vector2 a, Vector2 b) => Near(a.x, b.x) && Near(a.y, b.y);
        public static Rect NodeBounds(MapGraphLayoutDraft draft, MapGraphNodeDefinition node)
            => new Rect(draft.Graph.GetNodePosition(node.NodeId) - node.Footprint * 0.5f, node.Footprint);
        public static bool Contains(Rect outer, Rect inner)
            => inner.xMin >= outer.xMin - Epsilon && inner.xMax <= outer.xMax + Epsilon &&
               inner.yMin >= outer.yMin - Epsilon && inner.yMax <= outer.yMax + Epsilon;
        public static bool Overlaps(Rect a, Rect b)
            => Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > Epsilon &&
               Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > Epsilon;
        public static Rect Union(Rect a, Rect b)
            => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        public static Rect Expand(Rect rect, float amount)
            => Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

        public static bool TryGetVisibleSegment(MapGraphLayoutDraft draft, MapGraphEdgeDefinition edge,
            out Vector2 from, out Vector2 to)
        {
            from = to = default;
            if (edge == null || !draft.Graph.TryGetNode(edge.FromNodeId, out var first) ||
                !draft.Graph.TryGetNode(edge.ToNodeId, out var second)) return false;
            var a = draft.Graph.GetNodePosition(first.NodeId); var b = draft.Graph.GetNodePosition(second.NodeId);
            bool horizontal = edge.Axis == MapGraphAxis.Horizontal;
            if (!Finite(a) || !Finite(b) || !Positive(first.Footprint) || !Positive(second.Footprint) ||
                !Finite(edge.FromInset) || !Finite(edge.ToInset) || edge.FromInset < 0 || edge.ToInset < 0 ||
                (horizontal ? !Near(a.y, b.y) : edge.Axis != MapGraphAxis.Vertical || !Near(a.x, b.x))) return false;
            float delta = horizontal ? b.x - a.x : b.y - a.y;
            float firstRadius = (horizontal ? first.Footprint.x : first.Footprint.y) * 0.5f + edge.FromInset;
            float secondRadius = (horizontal ? second.Footprint.x : second.Footprint.y) * 0.5f + edge.ToInset;
            if (!Finite(delta) || Mathf.Abs(delta) <= firstRadius + secondRadius + Epsilon) return false;
            Vector2 direction = (horizontal ? Vector2.right : Vector2.up) * Mathf.Sign(delta);
            from = a + direction * firstRadius; to = b - direction * secondRadius;
            // 可见线使用同一轴坐标，禁止对两端独立投影造成像素级斜线。
            if (horizontal) to.y = from.y; else to.x = from.x;
            return true;
        }

        public static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect, float width = 0)
        {
            rect = Expand(rect, Mathf.Max(0, width) * 0.5f);
            if (Near(a.y, b.y)) return a.y >= rect.yMin - Epsilon && a.y <= rect.yMax + Epsilon &&
                Mathf.Min(a.x, b.x) <= rect.xMax + Epsilon && Mathf.Max(a.x, b.x) >= rect.xMin - Epsilon;
            if (Near(a.x, b.x)) return a.x >= rect.xMin - Epsilon && a.x <= rect.xMax + Epsilon &&
                Mathf.Min(a.y, b.y) <= rect.yMax + Epsilon && Mathf.Max(a.y, b.y) >= rect.yMin - Epsilon;
            return false;
        }

        public static bool CollinearOverlap(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (Near(a.y, b.y) && Near(c.y, d.y) && Near(a.y, c.y))
                return Mathf.Min(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) - Mathf.Max(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) > Epsilon;
            if (Near(a.x, b.x) && Near(c.x, d.x) && Near(a.x, c.x))
                return Mathf.Min(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)) - Mathf.Max(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) > Epsilon;
            return false;
        }

        public static bool ProperCrossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            bool firstHorizontal = Near(a.y, b.y) && Mathf.Abs(a.x - b.x) > Epsilon;
            bool firstVertical = Near(a.x, b.x) && Mathf.Abs(a.y - b.y) > Epsilon;
            bool secondHorizontal = Near(c.y, d.y) && Mathf.Abs(c.x - d.x) > Epsilon;
            bool secondVertical = Near(c.x, d.x) && Mathf.Abs(c.y - d.y) > Epsilon;
            if (firstVertical && secondHorizontal) return ProperCrossing(c, d, a, b);
            return firstHorizontal && secondVertical &&
                c.x > Mathf.Min(a.x, b.x) + Epsilon && c.x < Mathf.Max(a.x, b.x) - Epsilon &&
                a.y > Mathf.Min(c.y, d.y) + Epsilon && a.y < Mathf.Max(c.y, d.y) - Epsilon;
        }

        public static string PairKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            return a.Length + ":" + a + b;
        }
    }
}
