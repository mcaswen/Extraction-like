using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 为 URP 主相机开启深度纹理（_CameraDepthTexture），供透明水面等 Shader 做场景深度采样。
/// 挂在主摄像机上；与内置 DepthTextureMode 及 URP 的 requiresDepthTexture 同时设置，兼容不同版本。
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class URPDepthBufferSetup : MonoBehaviour
{
    [Tooltip("启用时请求深度纹理")]
    public bool requireDepthTexture = true;

    [Tooltip("是否同时写入深度+法线（仅当需要 SSAO 等时开启）")]
    public bool depthNormals = false;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Apply()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;

        if (depthNormals)
            cam.depthTextureMode |= DepthTextureMode.DepthNormals;
        else
            cam.depthTextureMode |= DepthTextureMode.Depth;

        var urp = GetComponent<UniversalAdditionalCameraData>();
        if (urp != null)
            urp.requiresDepthTexture = requireDepthTexture;
    }
}
