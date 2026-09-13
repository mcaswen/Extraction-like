using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>精确几何内容签名：规范枚举顺序，保留顶点共享关系、区域、绕序和重复几何。</summary>
    public static class MapGraphNavigationGeometrySignature
    {
        private readonly struct Incident : IComparable<Incident>
        {
            private readonly Vector3 _next, _previous;
            private readonly int _area;
            public Incident(Vector3 next, Vector3 previous, int area)
            { _next = next; _previous = previous; _area = area; }
            public int CompareTo(Incident other)
            {
                int next = CompareVector(_next, other._next); if (next != 0) return next;
                int previous = CompareVector(_previous, other._previous);
                return previous != 0 ? previous : _area.CompareTo(other._area);
            }
        }
        private readonly struct Triangle : IComparable<Triangle>
        {
            private readonly int _a, _b, _c;
            private readonly int _area;
            public Triangle(int a, int b, int c, int area)
            {
                // 仅循环旋转，不反转绕序。相同最小顶点时比较剩余顶点，保证退化三角形也有确定表示。
                if (CompareRotation(b, c, a, a, b, c) < 0 && CompareRotation(b, c, a, c, a, b) <= 0)
                { _a = b; _b = c; _c = a; }
                else if (CompareRotation(c, a, b, a, b, c) < 0)
                { _a = c; _b = a; _c = b; }
                else { _a = a; _b = b; _c = c; }
                _area = area;
            }
            public int CompareTo(Triangle other)
            {
                int geometry = CompareRotation(_a, _b, _c, other._a, other._b, other._c);
                return geometry != 0 ? geometry : _area.CompareTo(other._area);
            }
            public void Write(BinaryWriter writer)
            { writer.Write(_a); writer.Write(_b); writer.Write(_c); writer.Write(_area); }
        }

        /// <summary>仅启动、发布或导航变更调用。坏数据返回空签名，不能被当作有效缓存依据。</summary>
        public static string Capture(NavMeshTriangulation geometry)
        {
            var vertices = geometry.vertices; var indices = geometry.indices; var areas = geometry.areas;
            if (vertices == null || vertices.Length == 0 || indices == null || indices.Length == 0 ||
                indices.Length % 3 != 0 || areas == null || areas.Length != indices.Length / 3) return string.Empty;
            foreach (var vertex in vertices)
                if (!Finite(vertex.x) || !Finite(vertex.y) || !Finite(vertex.z)) return string.Empty;
            foreach (int index in indices) if (index < 0 || index >= vertices.Length) return string.Empty;
            // 原生三角网会为相同坐标保留多个顶点。用各自有向邻接面的精确内容区分，不合并顶点。
            // CSR 连续缓冲避免为每个顶点创建 List；邻接内容仍相同的歧义顶点才保留原相对顺序。
            var offsets = new int[vertices.Length + 1];
            foreach (int index in indices) offsets[index + 1]++;
            for (int i = 1; i < offsets.Length; i++) offsets[i] += offsets[i - 1];
            var cursor = (int[])offsets.Clone(); var incidents = new Incident[indices.Length];
            for (int i = 0; i < areas.Length; i++)
            {
                int a = indices[i * 3], b = indices[i * 3 + 1], c = indices[i * 3 + 2];
                incidents[cursor[a]++] = new Incident(vertices[b], vertices[c], areas[i]);
                incidents[cursor[b]++] = new Incident(vertices[c], vertices[a], areas[i]);
                incidents[cursor[c]++] = new Incident(vertices[a], vertices[b], areas[i]);
            }
            for (int i = 0; i < vertices.Length; i++)
                if (offsets[i + 1] - offsets[i] > 1) Array.Sort(incidents, offsets[i], offsets[i + 1] - offsets[i]);
            var order = new int[vertices.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int position = CompareVector(vertices[a], vertices[b]); if (position != 0) return position;
                int count = offsets[a + 1] - offsets[a], otherCount = offsets[b + 1] - offsets[b];
                if (count != otherCount) return count.CompareTo(otherCount);
                for (int i = 0; i < count; i++)
                { int incident = incidents[offsets[a] + i].CompareTo(incidents[offsets[b] + i]); if (incident != 0) return incident; }
                return a.CompareTo(b);
            });
            var canonicalIndex = new int[vertices.Length];
            for (int i = 0; i < order.Length; i++) canonicalIndex[order[i]] = i;
            var triangles = new Triangle[areas.Length];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new Triangle(canonicalIndex[indices[i * 3]], canonicalIndex[indices[i * 3 + 1]], canonicalIndex[indices[i * 3 + 2]], areas[i]);
            Array.Sort(triangles);
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write("MapNavigationGeometry:v3");
            writer.Write(order.Length);
            foreach (int index in order) WriteVector(writer, vertices[index]);
            writer.Write(triangles.Length);
            foreach (var triangle in triangles) triangle.Write(writer);
            writer.Flush(); stream.Position = 0;
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static int CompareVector(Vector3 a, Vector3 b)
        {
            int x = a.x.CompareTo(b.x); if (x != 0) return x;
            int y = a.y.CompareTo(b.y); return y != 0 ? y : a.z.CompareTo(b.z);
        }
        private static int CompareRotation(int a, int b, int c, int x, int y, int z)
        {
            int first = a.CompareTo(x); if (first != 0) return first;
            int second = b.CompareTo(y); return second != 0 ? second : c.CompareTo(z);
        }
        private static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            // -0 和 +0 是相同几何；除此之外不做量化或容差合并。
            writer.Write(value.x == 0 ? 0f : value.x);
            writer.Write(value.y == 0 ? 0f : value.y);
            writer.Write(value.z == 0 ? 0f : value.z);
        }
    }
}
