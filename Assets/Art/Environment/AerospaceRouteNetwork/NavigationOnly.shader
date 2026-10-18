// Preserve the original mesh as a navigation-bake source without rendering it.
// Transparent queue excludes it from opaque depth/normal/outline prepasses.
Shader "Aerospace/Navigation Source Only"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            half4 Frag() : SV_Target { clip(-1); return 0; }
            ENDHLSL
        }
    }
}
