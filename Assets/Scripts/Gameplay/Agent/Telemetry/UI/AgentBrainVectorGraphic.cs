#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 面板的自绘图形：连线、箭头、状态标记、虚线框和斜纹条在一个网格里批量画，不依赖位图和字体里的符号。
    /// 坐标以图形左上角为原点、向下为正，与布局稿一致。图元列表复用，重画不分配内存。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AgentBrainVectorGraphic : MaskableGraphic
    {
        private enum Shape : byte { Fill, Line, Triangle, Disc, Ring, Hatch }

        private struct Item
        {
            public Shape Shape;
            public Vector2 A, B, C;
            public float Width;
            public Color32 Color;
        }

        private const int DiscSegments = 18;
        private static readonly Vector2[] ClipA = new Vector2[12], ClipB = new Vector2[12];
        private readonly List<Item> _items = new List<Item>(256);

        public int ItemCount => _items.Count;

        public void Clear() => _items.Clear();

        public void Commit() => SetVerticesDirty();

        public void Fill(float x, float y, float w, float h, Color tint)
            => _items.Add(new Item { Shape = Shape.Fill, A = new Vector2(x, y), B = new Vector2(x + w, y + h), Color = tint });

        public void Line(Vector2 a, Vector2 b, Color tint, float width = 1f)
            => _items.Add(new Item { Shape = Shape.Line, A = a, B = b, Width = width, Color = tint });

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
            => _items.Add(new Item { Shape = Shape.Triangle, A = a, B = b, C = c, Color = tint });

        public void Disc(Vector2 center, float radius, Color tint)
            => _items.Add(new Item { Shape = Shape.Disc, A = center, Width = radius, Color = tint });

        public void Ring(Vector2 center, float radius, Color tint, float width = 1f)
            => _items.Add(new Item { Shape = Shape.Ring, A = center, B = new Vector2(radius, 0), Width = width, Color = tint });

        /// <summary>按 135° 斜纹填充矩形：纹宽 width，周期 period（沿纹的法向量量）</summary>
        public void Hatch(float x, float y, float w, float h, Color tint, float period = 5f, float width = 3f)
        {
            if (w <= 0 || h <= 0) return;
            _items.Add(new Item { Shape = Shape.Hatch, A = new Vector2(x, y), B = new Vector2(x + w, y + h), C = new Vector2(period, width), Color = tint });
        }

        /// <summary>箭头：尖端在 tip，朝 direction 方向</summary>
        public void Arrow(Vector2 tip, Vector2 direction, float size, Color tint)
        {
            Vector2 d = direction.normalized, n = new Vector2(-d.y, d.x);
            Triangle(tip, tip - d * size + n * size * .55f, tip - d * size - n * size * .55f, tint);
        }

        public void Outline(float x, float y, float w, float h, Color tint, float width = 1f, bool dashed = false)
        {
            Edge(new Vector2(x, y), new Vector2(x + w, y), tint, width, dashed);
            Edge(new Vector2(x + w, y), new Vector2(x + w, y + h), tint, width, dashed);
            Edge(new Vector2(x + w, y + h), new Vector2(x, y + h), tint, width, dashed);
            Edge(new Vector2(x, y + h), new Vector2(x, y), tint, width, dashed);
        }

        public void Check(Vector2 center, float size, Color tint, float width = 1.6f)
        {
            Vector2 a = center + new Vector2(-.42f, .02f) * size, b = center + new Vector2(-.12f, .32f) * size, c = center + new Vector2(.44f, -.36f) * size;
            Line(a, b, tint, width);
            Line(b - (c - b).normalized * width * .4f, c, tint, width);
        }

        public void Cross(Vector2 center, float size, Color tint, float width = 1.6f)
        {
            float r = size * .34f;
            Line(center + new Vector2(-r, -r), center + new Vector2(r, r), tint, width);
            Line(center + new Vector2(-r, r), center + new Vector2(r, -r), tint, width);
        }

        /// <summary>向右的小三角，表示折叠</summary>
        public void Caret(Vector2 center, float size, Color tint)
            => Triangle(center + new Vector2(-.22f, -.3f) * size, center + new Vector2(.3f, 0) * size, center + new Vector2(-.22f, .3f) * size, tint);

        private void Edge(Vector2 a, Vector2 b, Color tint, float width, bool dashed)
        {
            if (!dashed)
            {
                Line(a, b, tint, width);
                return;
            }

            float length = Vector2.Distance(a, b);
            Vector2 d = (b - a) / Mathf.Max(length, .001f);
            for (float s = 0; s < length; s += 7f) Line(a + d * s, a + d * Mathf.Min(s + 4f, length), tint, width);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int index = 0; index < _items.Count; index++)
            {
                Item item = _items[index];
                switch (item.Shape)
                {
                    case Shape.Fill:
                        Quad(mesh, item.A, new Vector2(item.B.x, item.A.y), item.B, new Vector2(item.A.x, item.B.y), item.Color);
                        break;
                    case Shape.Line:
                    {
                        Vector2 d = item.B - item.A;
                        if (d.sqrMagnitude < 1e-6f) break;
                        Vector2 n = new Vector2(-d.y, d.x).normalized * item.Width * .5f;
                        Quad(mesh, item.A - n, item.A + n, item.B + n, item.B - n, item.Color);
                        break;
                    }
                    case Shape.Triangle:
                    {
                        int start = mesh.currentVertCount;
                        Vertex(mesh, item.A, item.Color); Vertex(mesh, item.B, item.Color); Vertex(mesh, item.C, item.Color);
                        mesh.AddTriangle(start, start + 1, start + 2);
                        break;
                    }
                    case Shape.Disc:
                    {
                        int center = mesh.currentVertCount;
                        Vertex(mesh, item.A, item.Color);
                        for (int i = 0; i <= DiscSegments; i++)
                        {
                            float angle = i * Mathf.PI * 2f / DiscSegments;
                            Vertex(mesh, item.A + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * item.Width, item.Color);
                            if (i > 0) mesh.AddTriangle(center, center + i, center + i + 1);
                        }
                        break;
                    }
                    case Shape.Ring:
                        for (int i = 0; i < DiscSegments; i++)
                        {
                            float a0 = i * Mathf.PI * 2f / DiscSegments, a1 = (i + 1) * Mathf.PI * 2f / DiscSegments;
                            Vector2 p0 = item.A + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * item.B.x;
                            Vector2 p1 = item.A + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * item.B.x;
                            Vector2 n = new Vector2(-(p1 - p0).y, (p1 - p0).x).normalized * item.Width * .5f;
                            Quad(mesh, p0 - n, p0 + n, p1 + n, p1 - n, item.Color);
                        }
                        break;
                    case Shape.Hatch:
                        HatchMesh(mesh, item);
                        break;
                }
            }
        }

        // 每条纹是 k <= x + y <= k + width·√2 的带，与矩形求交后按扇形三角化
        private static void HatchMesh(VertexHelper mesh, Item item)
        {
            float x0 = item.A.x, y0 = item.A.y, x1 = item.B.x, y1 = item.B.y;
            float step = item.C.x * 1.41421356f, band = item.C.y * 1.41421356f;
            for (float k = x0 + y0 - band; k < x1 + y1; k += step)
            {
                ClipA[0] = new Vector2(x0, y0); ClipA[1] = new Vector2(x1, y0); ClipA[2] = new Vector2(x1, y1); ClipA[3] = new Vector2(x0, y1);
                int count = ClipHalfPlane(ClipA, 4, ClipB, k, 1f);
                count = ClipHalfPlane(ClipB, count, ClipA, -(k + band), -1f);
                if (count < 3) continue;
                int start = mesh.currentVertCount;
                for (int i = 0; i < count; i++) Vertex(mesh, ClipA[i], item.Color);
                for (int i = 1; i < count - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        // 保留 sign·(x + y) >= limit 的部分
        private static int ClipHalfPlane(Vector2[] input, int count, Vector2[] output, float limit, float sign)
        {
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 current = input[i], next = input[(i + 1) % count];
                float dc = sign * (current.x + current.y) - limit, dn = sign * (next.x + next.y) - limit;
                if (dc >= 0) output[written++] = current;
                if ((dc >= 0) != (dn >= 0)) output[written++] = Vector2.Lerp(current, next, dc / (dc - dn));
            }
            return written;
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int start = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint); Vertex(mesh, d, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        // 左上角为原点、向下为正，换成轴心在左上角的本地坐标
        private static void Vertex(VertexHelper mesh, Vector2 point, Color32 tint)
            => mesh.AddVert(new Vector3(point.x, -point.y), tint, Vector2.zero);
    }
}
#endif
