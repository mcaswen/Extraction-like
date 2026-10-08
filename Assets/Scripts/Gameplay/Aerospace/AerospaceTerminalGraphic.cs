using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Resolution-independent instrument graphics, drawn locally without bitmap backgrounds.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceTerminalGraphic : MaskableGraphic
    {
        public string locationCode;
        private static readonly Color Cyan = new Color(.32f, .83f, .90f, .66f), Faint = new Color(.29f, .52f, .61f, .26f), White = new Color(.67f, .78f, .82f, .66f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); float w = rectTransform.rect.width, h = rectTransform.rect.height;
            if (!string.IsNullOrEmpty(locationCode)) { Rocket(mesh, w, h); return; }
            // Stationary orientation datum, not an animated decorative radar.
            float cx = w * .50f, cy = h * .91f;
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2 / 72, b = (i + 1) * Mathf.PI * 2 / 72;
                Line(mesh, new Vector2(cx + Mathf.Cos(a) * 176, cy + Mathf.Sin(a) * 17), new Vector2(cx + Mathf.Cos(b) * 176, cy + Mathf.Sin(b) * 17), Faint);
            }
            for (int i = -6; i <= 6; i++) Line(mesh, new Vector2(cx + i * 24, cy + 25), new Vector2(cx + i * 24, cy + (i % 3 == 0 ? 34 : 29)), Faint);
            Line(mesh, new Vector2(cx - 196, cy), new Vector2(cx - 183, cy), Cyan);
            Line(mesh, new Vector2(cx + 183, cy), new Vector2(cx + 196, cy), Cyan);
            foreach (var origin in new[] { new Vector2(0, 8), new Vector2(w, 8), new Vector2(0, h - 8), new Vector2(w, h - 8) })
            {
                float sx = origin.x == 0 ? 1 : -1, sy = origin.y < h / 2 ? 1 : -1;
                Line(mesh, origin, origin + new Vector2(sx * 27, 0), Faint);
                Line(mesh, origin, origin + new Vector2(0, sy * 19), Faint);
            }
        }
        private void Rocket(VertexHelper mesh, float w, float h)
        {
            float mid = h * .50f;
            Vector2[] outline = { new Vector2(18, mid), new Vector2(52, mid - 14), new Vector2(83, mid - 14), new Vector2(83, mid - 10), new Vector2(w - 47, mid - 10), new Vector2(w - 33, mid - 6), new Vector2(w - 25, mid - 16), new Vector2(w - 25, mid + 16), new Vector2(w - 33, mid + 6), new Vector2(w - 47, mid + 10), new Vector2(83, mid + 10), new Vector2(83, mid + 14), new Vector2(52, mid + 14), new Vector2(18, mid) };
            for (int i = 1; i < outline.Length; i++) Line(mesh, outline[i - 1], outline[i], White);
            foreach (float x in new[] { 83f, 137f, w - 66 }) Line(mesh, new Vector2(x, mid - 10), new Vector2(x, mid + 10), Faint);
            Line(mesh, new Vector2(174, mid - 10), new Vector2(183, mid - 24), Faint);
            Line(mesh, new Vector2(183, mid - 24), new Vector2(193, mid - 10), Faint);
            Line(mesh, new Vector2(174, mid + 10), new Vector2(183, mid + 24), Faint);
            Line(mesh, new Vector2(183, mid + 24), new Vector2(193, mid + 10), Faint);
            float point = locationCode == "R01" ? w - 29 : locationCode == "R02" ? w - 65 : locationCode == "R03" ? w - 49 : locationCode == "R04" ? 183 : 83;
            if (locationCode == "R04") mid -= 20;
            Line(mesh, new Vector2(point, mid - 19), new Vector2(point, mid + 19), Cyan, 2.5f);
            Line(mesh, new Vector2(point - 8, mid - 19), new Vector2(point + 8, mid - 19), Cyan);
            Line(mesh, new Vector2(point - 8, mid + 19), new Vector2(point + 8, mid + 19), Cyan);
        }
        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float thickness = 1.5f)
        {
            from.y = -from.y; to.y = -to.y;
            Vector2 n = new Vector2(-(to - from).normalized.y, (to - from).normalized.x) * thickness * .5f;
            int i = mesh.currentVertCount;
            mesh.AddVert(from - n, tint, Vector2.zero); mesh.AddVert(from + n, tint, Vector2.zero);
            mesh.AddVert(to + n, tint, Vector2.zero); mesh.AddVert(to - n, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
