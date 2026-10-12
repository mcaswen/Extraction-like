using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Cold-blue inspection light well. Restrained engineering marks stay outside the specimen.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceInspectionBackdrop : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            const int columns = 64, rows = 36;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float u = (float)x / columns, v = (float)y / rows;
                float glow = Mathf.Exp(-((u - .49f) * (u - .49f) * 12 + (v - .57f) * (v - .57f) * 7));
                float upper = Mathf.Exp(-((u - .78f) * (u - .78f) * 34 + (v - .87f) * (v - .87f) * 24));
                float grain = (Mathf.PerlinNoise(x * 5.37f, y * 3.91f) - .5f) * .006f;
                Color c = Color.Lerp(new Color(.017f, .034f, .076f), new Color(.070f, .165f, .275f), glow);
                c += new Color(.012f, .031f, .051f, 0) * upper;
                c += new Color(grain, grain, grain, 0); c *= color;
                mesh.AddVert(new Vector3(r.xMin + u * r.width, r.yMin + v * r.height), c, Vector2.zero);
            }
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x;
                mesh.AddTriangle(a, a + columns + 1, a + 1);
                mesh.AddTriangle(a + 1, a + columns + 1, a + columns + 2);
            }
            // Sparse calibration lattice at the margin; no text, fake readings or continuous motion.
            for (int y = 0; y < 12; y++)
            for (int x = 0; x < 4; x++)
            {
                float px = r.xMax - 22 - x * 27, py = r.yMax - 146 - y * 44;
                Color tint = new Color(.25f, .57f, .82f, .035f + .012f * (3 - x));
                Segment(mesh, new Vector2(px - 2, py), new Vector2(px + 2, py), tint);
                Segment(mesh, new Vector2(px, py - 2), new Vector2(px, py + 2), tint);
            }
            Color seam = new Color(.20f, .49f, .76f, .18f);
            Segment(mesh, new Vector2(r.xMin + 1, r.yMax - 109), new Vector2(r.xMin + 1, r.yMin + 102), seam);
        }
        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, Color tint)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * .4f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a - n, tint, Vector2.zero); mesh.AddVert(a + n, tint, Vector2.zero);
            mesh.AddVert(b + n, tint, Vector2.zero); mesh.AddVert(b - n, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
