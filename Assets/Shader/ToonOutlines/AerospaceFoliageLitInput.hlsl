#ifndef EXTRACTION_AEROSPACE_FOLIAGE_INPUT_INCLUDED
#define EXTRACTION_AEROSPACE_FOLIAGE_INPUT_INCLUDED

// Retain URP's complete material layout, alpha, normal mapping, wind-independent geometry,
// depth/shadow passes and SRP batching. Only chlorophyll-colored albedo is art-directed.
#define InitializeStandardLitSurfaceData AerospaceOriginalLitSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surfaceData)
{
    AerospaceOriginalLitSurfaceData(uv, surfaceData);
    half3 albedo = surfaceData.albedo;
    // Include yellow-green leaves; exclude white bark (no green/blue separation)
    // and brown wood (red-dominant). This avoids repainting trunks mint-green.
    half vegetationMask = saturate((albedo.g - albedo.b) * 6.0h)
        * smoothstep(0.78h, 0.98h, albedo.g / max(albedo.r, 0.02h));
    half luminance = dot(albedo, half3(0.299h, 0.587h, 0.114h));
    half3 mutedForest = luminance * half3(0.72h, 0.97h, 0.79h);
    surfaceData.albedo = lerp(albedo, mutedForest, vegetationMask * 0.82h);
}
#endif
