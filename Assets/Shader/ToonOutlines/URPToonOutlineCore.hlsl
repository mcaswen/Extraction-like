#ifndef TA_URP_TOON_OUTLINE_CORE_INCLUDED
#define TA_URP_TOON_OUTLINE_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"

TEXTURE2D(_OutlineMaskMap);
SAMPLER(sampler_OutlineMaskMap);
TEXTURE2D(_FaceMaskMap);
SAMPLER(sampler_FaceMaskMap);

half _GlobalOutlineThicknessScale;

struct ToonOutlineColorData
{
    half4 color;
    half faceMask;
    half regionStrength;
};

float3 TA_DecodeUnitVector01(float3 encoded)
{
    float3 decoded = encoded * 2.0 - 1.0;
    float lengthSq = dot(decoded, decoded);
    return lengthSq > 0.0001 ? decoded * rsqrt(lengthSq) : float3(0.0, 1.0, 0.0);
}

float3 TA_DecodeSmoothNormalOS(float3 normalOS, float4 tangentOS, float4 uv2)
{
    // Tangent mode stores the smooth normal directly in tangent.xyz. This is cheap,
    // but it replaces the mesh tangent, so use UV2 mode if a material needs normal maps.
    float tangentLengthSq = dot(tangentOS.xyz, tangentOS.xyz);
    float3 tangentNormal = tangentLengthSq > 0.0001 ? tangentOS.xyz * rsqrt(tangentLengthSq) : normalOS;

    // UV2 mode stores a 0..1 encoded vector in uv2.xyz. If UV2 is not baked, the
    // normal blend can be lowered in the material to fall back to the original normal.
    float3 uv2Normal = TA_DecodeUnitVector01(uv2.xyz);
    float3 selectedNormal = lerp(tangentNormal, uv2Normal, step(0.5, _SmoothNormalSource));

    return normalize(lerp(normalOS, selectedNormal, saturate(_SmoothNormalBlend)));
}

float TA_GetDistance01(float3 positionWS)
{
    float viewDistance = distance(GetCameraPositionWS(), positionWS);
    float fadeRange = max(_OutlineDepthFadeEnd - _OutlineDepthFadeStart, 0.0001);
    return saturate((viewDistance - _OutlineDepthFadeStart) / fadeRange);
}

float TA_GetDistanceThicknessScale(float3 positionWS)
{
    float distanceT = TA_GetDistance01(positionWS);
    float curvedT = pow(distanceT, max(_OutlineDistanceThicknessPower, 0.001));
    return lerp(_OutlineNearThicknessScale, _OutlineFarThicknessScale, curvedT);
}

float4 TA_ApplyScreenSpaceOutlineOffset(float3 positionOS, float3 normalOS)
{
    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
    VertexNormalInputs normalInput = GetVertexNormalInputs(normalOS);

    float4 positionCS = vertexInput.positionCS;
    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalInput.normalWS);
    float2 normalSS = normalVS.xy;
    float normalSSLengthSq = dot(normalSS, normalSS);
    normalSS = normalSSLengthSq > 0.000001 ? normalSS * rsqrt(normalSSLengthSq) : float2(0.0, 0.0);

    float distanceScale = TA_GetDistanceThicknessScale(vertexInput.positionWS);
    float pixelWidth = max(0.0, _OutlineThickness * distanceScale * _GlobalOutlineThicknessScale);

    // Convert pixel width to NDC, then multiply by clip.w so the hull width is
    // stable on screen. This still follows the smoothed normal direction; it just
    // applies the final displacement in clip space for distance-stable thickness.
    float2 ndcOffset = normalSS * pixelWidth * 2.0 / max(_ScreenParams.xy, float2(1.0, 1.0));
    positionCS.xy += ndcOffset * positionCS.w;
    positionCS.z += _OutlineZOffset * positionCS.w;

    return positionCS;
}

half4 TA_ReadOutlineRegionMask(float2 uv, float2 lightmapUV)
{
    half4 mask = half4(0.0, 0.0, 0.0, 0.0);

    if (_UseOutlineMaskMap > 0.5h)
    {
        float2 maskUV = uv * _OutlineMaskMap_ST.xy + _OutlineMaskMap_ST.zw;
        mask = SAMPLE_TEXTURE2D(_OutlineMaskMap, sampler_OutlineMaskMap, maskUV);
    }

#if defined(LIGHTMAP_ON)
    if (_UseLightmapAlphaForOutlineRegion > 0.5h)
    {
        float2 lmUV = lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
        half lmAlpha = SAMPLE_TEXTURE2D(unity_Lightmap, samplerunity_Lightmap, lmUV).a;

        // Interpret lightmap alpha as compact region IDs:
        // 0.20..0.45 hair, 0.45..0.70 cloth, 0.70..0.95 skin, 0.95..1 accent.
        // Values below 0.20 use the default outline color.
        half hair = step(0.20h, lmAlpha) * (1.0h - step(0.45h, lmAlpha));
        half cloth = step(0.45h, lmAlpha) * (1.0h - step(0.70h, lmAlpha));
        half skin = step(0.70h, lmAlpha) * (1.0h - step(0.95h, lmAlpha));
        half accent = step(0.95h, lmAlpha);
        mask = half4(hair, cloth, skin, accent);
    }
#endif

    return mask;
}

half4 TA_SelectOutlineRegionColor(half4 mask)
{
    half4 baseColor = _OutlineColor;
    half4 hairColor = _OutlineHairColor;
    half4 clothColor = _OutlineClothColor;
    half4 skinColor = _OutlineSkinColor;
    half4 accentColor = _OutlineAccentColor;

    half4 weightedColor = baseColor;
    half weight = 1.0h;

    weightedColor += hairColor * mask.r;
    weightedColor += clothColor * mask.g;
    weightedColor += skinColor * mask.b;
    weightedColor += accentColor * mask.a;
    weight += mask.r + mask.g + mask.b + mask.a;

    half4 blended = weightedColor / max(weight, 0.0001h);
    half regionAmount = saturate(dot(mask, half4(1.0h, 1.0h, 1.0h, 1.0h)) * _OutlineRegionContrast);
    return lerp(baseColor, blended, regionAmount * saturate(_OutlineRegionBlend));
}

ToonOutlineColorData TA_GetOutlineColorData(float2 uv, float2 lightmapUV, float3 normalWS, float3 viewDirWS)
{
    half4 regionMask = TA_ReadOutlineRegionMask(uv, lightmapUV);
    half4 color = TA_SelectOutlineRegionColor(regionMask);

    half faceMask = SAMPLE_TEXTURE2D(_FaceMaskMap, sampler_FaceMaskMap, uv).r;
    half viewFacing = saturate(dot(normalize(normalWS), normalize(viewDirWS)));
    half faceViewKeep = pow(saturate(1.0h - viewFacing), max(_FaceViewPower, 0.001h));
    half faceSuppress = saturate(faceMask * _FaceOutlineSuppress);
    half viewSuppress = lerp(1.0h, faceViewKeep, saturate(_FaceViewSuppress) * faceMask);

    color.a *= (1.0h - faceSuppress) * viewSuppress;
    color.rgb *= _OutlineIntensity;

    ToonOutlineColorData data;
    data.color = color;
    data.faceMask = faceMask;
    data.regionStrength = saturate(dot(regionMask, half4(1.0h, 1.0h, 1.0h, 1.0h)));
    return data;
}

void TA_ToonOutlineColor_float(
    float2 UV,
    float2 LightmapUV,
    float3 NormalWS,
    float3 ViewDirWS,
    out float4 Color,
    out float FaceMask,
    out float RegionStrength)
{
    ToonOutlineColorData data = TA_GetOutlineColorData(UV, LightmapUV, NormalWS, ViewDirWS);
    Color = data.color;
    FaceMask = data.faceMask;
    RegionStrength = data.regionStrength;
}

void TA_DecodeSmoothNormalOS_float(
    float3 NormalOS,
    float4 TangentOS,
    float4 UV2,
    out float3 SmoothNormalOS)
{
    SmoothNormalOS = TA_DecodeSmoothNormalOS(NormalOS, TangentOS, UV2);
}

void TA_OutlineClipPosition_float(
    float3 PositionOS,
    float3 SmoothNormalOS,
    out float4 PositionCS)
{
    PositionCS = TA_ApplyScreenSpaceOutlineOffset(PositionOS, SmoothNormalOS);
}

#endif
