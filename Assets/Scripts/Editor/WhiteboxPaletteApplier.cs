using UnityEditor;
using UnityEngine;

public static class WhiteboxPaletteApplier
{
    [MenuItem("Tools/Whitebox/Apply High Contrast Palette To Active Scene")]
    private static void ApplyHighContrastPaletteToActiveScene()
    {
        Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];
            if (rendererComponent == null || rendererComponent.gameObject == null)
            {
                continue;
            }

            string objectName = rendererComponent.gameObject.name.ToLowerInvariant();
            Color? targetColor = ResolveColor(objectName);
            if (!targetColor.HasValue)
            {
                continue;
            }

            ApplyColor(rendererComponent, targetColor.Value);
        }
    }

    private static Color? ResolveColor(string objectName)
    {
        if (objectName.Contains("wall") || objectName.Contains("cliff"))
        {
            return new Color(0.1f, 0.12f, 0.16f, 1f);
        }

        if (objectName.Contains("floor") || objectName.Contains("ground") || objectName.Contains("platform") || objectName.Contains("deck"))
        {
            return new Color(0.74f, 0.76f, 0.8f, 1f);
        }

        if (objectName.Contains("water") || objectName.Contains("pool"))
        {
            return new Color(0.18f, 0.52f, 0.68f, 1f);
        }

        if (objectName.Contains("marker") || objectName.Contains("extract"))
        {
            return new Color(0.2f, 0.9f, 0.6f, 1f);
        }

        if (objectName.Contains("crate") || objectName.Contains("cabinet") || objectName.Contains("bench") || objectName.Contains("desk"))
        {
            return new Color(0.67f, 0.53f, 0.36f, 1f);
        }

        if (objectName.Contains("container") || objectName.Contains("locker") || objectName.Contains("console") || objectName.Contains("vending"))
        {
            return new Color(0.46f, 0.58f, 0.72f, 1f);
        }

        if (objectName.Contains("player"))
        {
            return new Color(0.96f, 0.96f, 0.98f, 1f);
        }

        if (objectName.Contains("lootbox") || objectName.Contains("chest"))
        {
            return new Color(0.96f, 0.96f, 0.98f, 1f);
        }

        if (objectName.Contains("enemy") || objectName.Contains("strander") || objectName.Contains("tidal") || objectName.Contains("hunter") || objectName.Contains("sentinel"))
        {
            return new Color(0.96f, 0.96f, 0.98f, 1f);
        }

        if (objectName.Contains("rock") || objectName.Contains("reef") || objectName.Contains("dragonbone"))
        {
            return new Color(0.34f, 0.36f, 0.4f, 1f);
        }

        return null;
    }

    private static void ApplyColor(Renderer rendererComponent, Color color)
    {
        Undo.RecordObject(rendererComponent, "Apply High Contrast Whitebox Color");
        Material sourceMaterial = rendererComponent.sharedMaterial;
        Material material;
        if (sourceMaterial != null)
        {
            material = new Material(sourceMaterial);
        }
        else
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        if (material.shader == null)
        {
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        rendererComponent.sharedMaterial = material;
        EditorUtility.SetDirty(rendererComponent);
    }
}
