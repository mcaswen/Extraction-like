using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Static, texture-free olive studio surface for the sample viewer only.</summary>
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
                float glow = Mathf.Exp(-((u - .42f) * (u - .42f) * 9 + (v - .55f) * (v - .55f) * 5));
                float grain = (Mathf.PerlinNoise(x * 5.37f, y * 3.91f) - .5f) * .006f;
                Color c = Color.Lerp(new Color(.105f, .13f, .077f), new Color(.24f, .28f, .175f), glow);
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
        }
    }
}
