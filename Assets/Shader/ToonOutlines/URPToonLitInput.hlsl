#ifndef TA_URP_TOON_LIT_INPUT_INCLUDED
#define TA_URP_TOON_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _OutlineMaskMap_ST;
    half4 _BaseColor;
    half4 _ShadeColor;
    half4 _SpecColor;
    half4 _EmissionColor;
    half4 _OutlineColor;
    half4 _OutlineHairColor;
    half4 _OutlineClothColor;
    half4 _OutlineSkinColor;
    half4 _OutlineAccentColor;
    half _ShadowThreshold;
    half _ShadowSoftness;
    half _IndirectStrength;
    half _SpecularStrength;
    half _SpecularThreshold;
    half _SpecularSoftness;
    half _Cutoff;
    half _Smoothness;
    half _Metallic;
    half _Surface;
    half _UseOutlineMaskMap;
    half _UseLightmapAlphaForOutlineRegion;
    half _SmoothNormalSource;
    half _SmoothNormalBlend;
    half _OutlineThickness;
    half _OutlineNearThicknessScale;
    half _OutlineFarThicknessScale;
    half _OutlineDepthFadeStart;
    half _OutlineDepthFadeEnd;
    half _OutlineDistanceThicknessPower;
    half _OutlineIntensity;
    half _OutlineRegionContrast;
    half _OutlineRegionBlend;
    half _FaceOutlineSuppress;
    half _FaceViewSuppress;
    half _FaceViewPower;
    half _OutlineZOffset;
CBUFFER_END

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    outSurfaceData = (SurfaceData)0;
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.alpha = alpha;
    outSurfaceData.metallic = 0.0h;
    outSurfaceData.specular = _SpecColor.rgb;
    outSurfaceData.smoothness = saturate(_SpecularStrength);
    outSurfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
    outSurfaceData.emission = 0.0h;
    outSurfaceData.occlusion = 1.0h;
    outSurfaceData.clearCoatMask = 0.0h;
    outSurfaceData.clearCoatSmoothness = 0.0h;
}

#endif
