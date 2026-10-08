using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Resolution-independent instrument graphics, drawn locally without bitmap backgrounds.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceTerminalGraphic : MaskableGraphic
    {
        public string locationCode, glyph;
        private static readonly Color Accent = new Color(.86f, .88f, .46f, .85f), Faint = new Color(.55f, .61f, .41f, .20f), White = new Color(.70f, .74f, .56f, .60f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); float w = rectTransform.rect.width, h = rectTransform.rect.height;
            if (!string.IsNullOrEmpty(glyph)) { Glyph(mesh, w, h); return; }
            if (!string.IsNullOrEmpty(locationCode)) { Rocket(mesh, w, h); return; }
            // Stationary orientation datum, not an animated decorative radar.
            float cx = w * .50f, cy = h * .91f;
            for (int i = 0; i < 72; i++)
            {
                float a = i * Mathf.PI * 2 / 72, b = (i + 1) * Mathf.PI * 2 / 72;
                Line(mesh, new Vector2(cx + Mathf.Cos(a) * 176, cy + Mathf.Sin(a) * 17), new Vector2(cx + Mathf.Cos(b) * 176, cy + Mathf.Sin(b) * 17), Faint);
            }
            for (int i = -6; i <= 6; i++) Line(mesh, new Vector2(cx + i * 24, cy + 25), new Vector2(cx + i * 24, cy + (i % 3 == 0 ? 34 : 29)), Faint);
            Line(mesh, new Vector2(cx - 196, cy), new Vector2(cx - 183, cy), Faint);
            Line(mesh, new Vector2(cx + 183, cy), new Vector2(cx + 196, cy), Faint);
            foreach (var origin in new[] { new Vector2(0, 8), new Vector2(w, 8), new Vector2(0, h - 8), new Vector2(w, h - 8) })
            {
                float sx = origin.x == 0 ? 1 : -1, sy = origin.y < h / 2 ? 1 : -1;
                Line(mesh, origin, origin + new Vector2(sx * 27, 0), Faint);
                Line(mesh, origin, origin + new Vector2(0, sy * 19), Faint);
            }
        }
        private void Rocket(VertexHelper mesh, float w, float h)
        {
            Vector2 Scale(float x, float y) => new Vector2(x * w / 104, y * h / 228);
            void Segment(float x1, float y1, float x2, float y2, Color tint) => Line(mesh, Scale(x1, y1), Scale(x2, y2), tint, .85f);
            Vector2[] outline = { Scale(52, 12), Scale(40, 34), Scale(35, 52), Scale(35, 173), Scale(69, 173), Scale(69, 52), Scale(64, 34), Scale(52, 12) };
            for (int i = 1; i < outline.Length; i++) Line(mesh, outline[i - 1], outline[i], White, .85f);
            foreach (float y in new[] { 52f, 65f, 89f, 103f, 111f, 131f, 155f }) Segment(35, y, 69, y, Faint);
            Segment(35, 123, 25, 153, White); Segment(25, 153, 25, 185, White); Segment(25, 185, 35, 173, White);
            Segment(69, 123, 79, 153, White); Segment(79, 153, 79, 185, White); Segment(79, 185, 69, 173, White);
            Segment(46, 173, 46, 184, White); Segment(58, 173, 58, 184, White);
            Segment(46, 184, 35, 211, locationCode == "R01" ? Accent : White); Segment(58, 184, 69, 211, locationCode == "R01" ? Accent : White);
            Segment(35, 211, 69, 211, locationCode == "R01" ? Accent : White); Segment(46, 184, 58, 184, White);
            Segment(20, 34, 20, 211, Faint); Segment(84, 34, 84, 211, Faint);
            Segment(52, 2, 52, 8, White); Segment(52, 220, 52, 228, White);
            float locationY = locationCode == "R01" ? 199 : locationCode == "R02" ? 148 : locationCode == "R03" ? 166 : locationCode == "R04" ? 106 : 65;
            float locationX = locationCode == "R04" ? 29 : 52;
            Segment(locationX - 7, locationY - 5, locationX + 7, locationY - 5, Accent); Segment(locationX - 7, locationY + 5, locationX + 7, locationY + 5, Accent);
            Segment(locationX - 7, locationY - 5, locationX - 7, locationY + 5, Accent); Segment(locationX + 7, locationY - 5, locationX + 7, locationY + 5, Accent);
            Segment(locationX + 7, locationY, 91, locationY, Accent); Segment(91, locationY, 99, locationY - 9, Accent);
        }
        private void Glyph(VertexHelper mesh, float w, float h)
        {
            Vector2 P(float x, float y) => new Vector2(x / 24 * w, y / 24 * h);
            void L(float a, float b, float c, float d) => Line(mesh, P(a, b), P(c, d), color, 1.1f);
            if (glyph == "cube" || glyph == "cut")
            {
                L(12, 2, 22, 7); L(22, 7, 22, 17); L(22, 17, 12, 22); L(12, 22, 2, 17); L(2, 17, 2, 7); L(2, 7, 12, 2);
                L(2, 7, 12, 12); L(22, 7, 12, 12); L(12, 12, 12, 22);
                if (glyph == "cut") { L(8, 4, 8, 19); L(16, 4, 16, 19); }
            }
            else if (glyph == "layers")
            {
                L(12, 2, 23, 7); L(23, 7, 12, 12); L(12, 12, 1, 7); L(1, 7, 12, 2);
                L(2, 12, 12, 17); L(12, 17, 22, 12); L(2, 17, 12, 22); L(12, 22, 22, 17);
            }
            else if (glyph == "reset")
            {
                for (int i = 0; i < 26; i++) { float a = (i * 10 + 35) * Mathf.Deg2Rad, b = ((i + 1) * 10 + 35) * Mathf.Deg2Rad; L(12 + Mathf.Cos(a) * 9, 12 + Mathf.Sin(a) * 9, 12 + Mathf.Cos(b) * 9, 12 + Mathf.Sin(b) * 9); }
                L(3, 1, 3, 8); L(3, 8, 10, 8);
            }
            else if (glyph == "eye")
            {
                for (int i = 0; i < 32; i++)
                { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; L(12 + Mathf.Cos(a) * 11, 12 + Mathf.Sin(a) * 6, 12 + Mathf.Cos(b) * 11, 12 + Mathf.Sin(b) * 6); L(12 + Mathf.Cos(a) * 3, 12 + Mathf.Sin(a) * 3, 12 + Mathf.Cos(b) * 3, 12 + Mathf.Sin(b) * 3); }
            }
            else if (glyph == "play")
            {
                int start = mesh.currentVertCount;
                foreach (var point in new[] { P(6, 3), P(21, 12), P(6, 21) }) mesh.AddVert(new Vector3(point.x, -point.y, 0), color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }
            else if (glyph == "gear")
            {
                // Filled vector ring with eight teeth: no font-symbol fallback or bitmap scaling.
                int start = mesh.currentVertCount;
                const int segments = 64;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2 / segments;
                    float radius = i % 8 >= 2 && i % 8 <= 5 ? 11 : 8.3f;
                    foreach (float r in new[] { radius, 3.7f })
                    {
                        Vector2 point = P(12 + Mathf.Cos(angle) * r, 12 + Mathf.Sin(angle) * r);
                        mesh.AddVert(new Vector3(point.x, -point.y, 0), color, Vector2.zero);
                    }
                    int a = start + i * 2, b = start + (i + 1) % segments * 2;
                    mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a + 1, b, b + 1);
                }
            }
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
