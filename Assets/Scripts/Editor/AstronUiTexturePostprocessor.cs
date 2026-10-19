using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Normalize the four opaque white-backed menu exports on import; source PNGs remain intact.</summary>
public sealed class AstronUiTexturePostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/Art/Sprites/UI_New_Astron/";
    public static bool HasWhiteMatte(string path) => path == Root + "UI-标题.png" ||
        path == Root + "UI-开始游戏Button.png" || path == Root + "UI-设置Button.png" || path == Root + "UI-退出游戏Button.png";

    public override uint GetVersion() => 1;
    void OnPostprocessTexture(Texture2D texture)
    {
        if (!HasWhiteMatte(assetPath)) return;
        var pixels = texture.GetPixels32();
        RemoveExteriorWhite(pixels, texture.width, texture.height);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    // Flood only from the canvas perimeter so enclosed white lettering/highlights survive.
    public static void RemoveExteriorWhite(Color32[] pixels, int width, int height)
    {
        var seen = new bool[pixels.Length];
        var queue = new Queue<int>();
        void Enqueue(int index)
        {
            if (seen[index]) return;
            seen[index] = true;
            var c = pixels[index];
            if (c.r < 238 || c.g < 238 || c.b < 238) return;
            queue.Enqueue(index);
        }
        for (int x = 0; x < width; x++) { Enqueue(x); Enqueue((height - 1) * width + x); }
        for (int y = 0; y < height; y++) { Enqueue(y * width); Enqueue(y * width + width - 1); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            pixels[i].a = 0;
            if (i % width > 0) Enqueue(i - 1);
            if (i % width < width - 1) Enqueue(i + 1);
            if (i >= width) Enqueue(i - width);
            if (i < pixels.Length - width) Enqueue(i + width);
        }
    }
}
