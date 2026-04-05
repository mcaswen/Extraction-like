using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 2.5D 视角墙体淡出组件。
/// 当墙体挡住玩家时，会自动降低透明度。
/// </summary>
public class PerspectiveFadeWall : MonoBehaviour
{
    public float VisibleAlpha = 1f;
    public float OccludedAlpha = 0.18f;
    public float FadeSpeed = 10f;

    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private float _currentAlpha = 1f;
    private float _targetAlpha = 1f;

    private void Awake()
    {
        CacheMaterials();
        ApplyAlphaImmediate(VisibleAlpha);
    }

    private void Update()
    {
        if (Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            return;
        }

        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, FadeSpeed * Time.deltaTime);
        ApplyAlphaImmediate(_currentAlpha);
    }

    public void SetOccluded(bool isOccluded)
    {
        _targetAlpha = isOccluded ? OccludedAlpha : VisibleAlpha;
    }

    private void CacheMaterials()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        _baseColors = new Color[_renderers.Length][];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer rendererComponent = _renderers[i];
            Material[] rendererMaterials = rendererComponent.materials;
            _materials[i] = rendererMaterials;
            _baseColors[i] = new Color[rendererMaterials.Length];

            for (int j = 0; j < rendererMaterials.Length; j++)
            {
                Material material = rendererMaterials[j];
                ConfigureMaterialForTransparency(material);
                _baseColors[i][j] = GetMaterialColor(material);
            }
        }
    }

    private void ApplyAlphaImmediate(float alpha)
    {
        for (int i = 0; i < _materials.Length; i++)
        {
            for (int j = 0; j < _materials[i].Length; j++)
            {
                Material material = _materials[i][j];
                Color color = _baseColors[i][j];
                color.a = alpha;

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private static void ConfigureMaterialForTransparency(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
