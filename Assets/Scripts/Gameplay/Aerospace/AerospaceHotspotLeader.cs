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
            Vector2 a = new Vector2(anchor.x, -anchor.y), b = Vector2.Lerp(a, new Vector2(end.x, -end.y), reveal);
            Vector2 elbow = b + new Vector2(a.x > b.x ? 28 : -28, 0);
            Line(mesh, a, elbow); Line(mesh, elbow, b);
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

    public sealed class AerospaceHotspotPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> hover;
        public void OnPointerEnter(PointerEventData e) { hover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { hover?.Invoke(false); }
        private void OnDisable() { hover?.Invoke(false); }
    }
}
