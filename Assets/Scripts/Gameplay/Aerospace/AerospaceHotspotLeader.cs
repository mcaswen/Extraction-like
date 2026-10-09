using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Non-interactive leader from a model anchor to its off-model label.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceHotspotLeader : MaskableGraphic
    {
        private Vector2 anchor, end;
        private float reveal = 1;
        public void SetPoints(Vector2 modelPoint, Vector2 labelPoint, Color tint, float progress = 1)
        {
            if (anchor == modelPoint && end == labelPoint && color == tint && reveal == progress) return;
            anchor = modelPoint; end = labelPoint; color = tint; reveal = progress; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 a = new Vector2(anchor.x, -anchor.y), b = new Vector2(end.x, -end.y);
            Vector2 elbow = b + new Vector2(a.x > b.x ? 28 : -28, 0);
            float first = Vector2.Distance(a, elbow), second = Vector2.Distance(elbow, b);
            float length = (first + second) * Mathf.Clamp01(reveal);
            if (length > .01f) Line(mesh, a, Vector2.Lerp(a, elbow, Mathf.Clamp01(length / Mathf.Max(.01f, first))));
            if (length > first) Line(mesh, elbow, Vector2.Lerp(elbow, b, (length - first) / Mathf.Max(.01f, second)));
            const int steps = 16; const float radius = 3.5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2 / steps;
                mesh.AddVert(a + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
        private void Line(VertexHelper mesh, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * .55f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - n, color, Vector2.zero); mesh.AddVert(a + n, color, Vector2.zero);
            mesh.AddVert(b + n, color, Vector2.zero); mesh.AddVert(b - n, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceHotspotPulse : MaskableGraphic
    {
        public float Progress { get; private set; } = 1;
        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value); if (Mathf.Approximately(value, Progress)) return;
            Progress = value; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (Progress >= 1) return;
            var rect = rectTransform.rect; float gap = 2 + 7 * Mathf.SmoothStep(0, 1, Progress);
            Color tint = color; tint.a *= Mathf.Pow(1 - Progress, 2) * .8f;
            foreach (var corner in new[] { new Vector2(rect.xMin - gap, rect.yMin - gap), new Vector2(rect.xMax + gap, rect.yMin - gap), new Vector2(rect.xMin - gap, rect.yMax + gap), new Vector2(rect.xMax + gap, rect.yMax + gap) })
            {
                float sx = corner.x < rect.center.x ? 1 : -1, sy = corner.y < rect.center.y ? 1 : -1;
                Edge(mesh, corner, corner + new Vector2(sx * 9, 0), tint);
                Edge(mesh, corner, corner + new Vector2(0, sy * 9), tint);
            }
        }
        private static void Edge(VertexHelper mesh, Vector2 a, Vector2 b, Color tint)
        {
            Vector2 n = new Vector2(-(b - a).normalized.y, (b - a).normalized.x) * .65f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a - n, tint, Vector2.zero); mesh.AddVert(a + n, tint, Vector2.zero);
            mesh.AddVert(b + n, tint, Vector2.zero); mesh.AddVert(b - n, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }

    public sealed class AerospaceHotspotPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> hover;
        public void OnPointerEnter(PointerEventData e) { hover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { hover?.Invoke(false); }
        private void OnDisable() { hover?.Invoke(false); }
    }
}
