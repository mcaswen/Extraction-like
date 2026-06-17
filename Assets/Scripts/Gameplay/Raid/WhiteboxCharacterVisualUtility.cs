using UnityEngine;

/// <summary>
/// 白盒角色和传送点等临时对象的材质染色工具
/// </summary>
public static class WhiteboxCharacterVisualUtility
{
    private static readonly Color CharacterWhite = new Color(0.96f, 0.96f, 0.98f, 1f);

    /// <summary>
    /// 把目标对象的可渲染材质统一染成角色白色
    /// </summary>
    /// <param name="target">需要染色的根对象</param>
    public static void ApplyCharacterWhite(GameObject target)
    {
        ApplySolidColor(target, CharacterWhite);
    }

    /// <summary>
    /// 把目标对象的可渲染材质统一染成指定颜色
    /// </summary>
    /// <param name="target">需要染色的根对象</param>
    /// <param name="color">目标颜色</param>
    public static void ApplySolidColor(GameObject target, Color color)
    {
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];
            if (rendererComponent == null || ShouldSkipRenderer(rendererComponent))
            {
                continue;
            }

            Material[] sourceMaterials = rendererComponent.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                rendererComponent.material = CreateSolidColorMaterial(null, color);
                continue;
            }

            Material[] clonedMaterials = new Material[sourceMaterials.Length];
            for (int j = 0; j < sourceMaterials.Length; j++)
            {
                clonedMaterials[j] = CreateSolidColorMaterial(sourceMaterials[j], color);
            }

            rendererComponent.materials = clonedMaterials;
        }
    }

    private static bool ShouldSkipRenderer(Renderer rendererComponent)
    {
        return rendererComponent is LineRenderer ||
               rendererComponent is TrailRenderer ||
               rendererComponent is ParticleSystemRenderer;
    }

    private static Material CreateSolidColorMaterial(Material sourceMaterial, Color color)
    {
        // 优先复用源材质着色器，缺失时按通用渲染管线和内置管线顺序兜底
        Shader shader = sourceMaterial != null && sourceMaterial.shader != null
            ? sourceMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = sourceMaterial != null
            ? new Material(sourceMaterial)
            : new Material(shader);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        material.renderQueue = -1;
        return material;
    }
}
