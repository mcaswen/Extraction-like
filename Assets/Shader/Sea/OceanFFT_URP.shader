Shader "TA/OceanFFT_URP"
{
    Properties
    {
        [Header(Surface)]
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.55, 0.72, 1)
        _DeepColor ("Deep Color", Color) = (0.02, 0.12, 0.22, 1)
        _DepthFadeDistance ("Depth Fade Distance", Range(0.05, 50)) = 3.0
        _HeightScale ("Height Scale", Float) = 1.0

        [Header(FFT Textures)]
        _HeightMap ("Height Map R", 2D) = "black" {}
        _NormalMap ("Normal Map RG nx nz", 2D) = "bump" {}
        _PatchSize ("Patch Size", Float) = 40.0
        _OceanOrigin ("Ocean Origin XZ", Vector) = (0, 0, 0, 0)

        [Header(Lighting)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.5
        _FresnelBias ("Fresnel Bias", Range(0, 0.2)) = 0.04

        [Header(Alpha)]
        _AlphaBase ("Alpha Base", Range(0, 1)) = 0.65
        _EdgeSoftness ("Edge Softness", Range(0.01, 5)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthFadeDistance;
                float _HeightScale;
                float _PatchSize;
                float4 _OceanOrigin;
                half _Smoothness;
                half4 _SpecularColor;
                half _FresnelPower;
                half _FresnelBias;
                half _AlphaBase;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float2 OceanUV(float3 worldPos)
            {
                float2 o = _OceanOrigin.xz;
                return frac((worldPos.xz - o) / max(_PatchSize, 1e-4));
            }

            float3 SampleOceanNormal(float2 oceanUV)
            {
                float2 ng = SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, oceanUV, 0).rg;
                float nx = ng.r;
                float nz = ng.g;
                float ny = sqrt(max(1.0 - nx * nx - nz * nz, 0.0));
                return float3(nx, ny, nz);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(positionOS);
                float2 ouv = OceanUV(worldPos);
                float h = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, ouv, 0).r;
                worldPos.y += h * _HeightScale;

                o.positionWS = worldPos;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 oceanUV = OceanUV(input.positionWS);
                float3 normalWS = normalize(SampleOceanNormal(oceanUV));

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = (unity_OrthoParams.w == 0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
                float surfaceZ = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());

                float depth01 = Linear01Depth(rawDepth, _ZBufferParams);
                float skyMask = smoothstep(0.999f, 1.0f, depth01);

                float waterThickness = max(sceneZ - surfaceZ, 0.0);
                waterThickness *= (1.0 - skyMask);

                float depthFactor = 1.0 - exp(-waterThickness / max(_DepthFadeDistance, 1e-4));
                half3 baseCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depthFactor));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * baseCol * NdotL * mainLight.shadowAttenuation;

                half3 viewDir = normalize(input.viewDirWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half NdotH = saturate(dot(normalWS, halfDir));
                half spec = pow(NdotH, (1.0 - _Smoothness) * 128.0 + 4.0);
                half3 specular = mainLight.color * _SpecularColor.rgb * spec * mainLight.shadowAttenuation;

                half NdotV = saturate(dot(normalWS, viewDir));
                half fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - NdotV, _FresnelPower);

                half edgeBlend = saturate(waterThickness / max(_EdgeSoftness, 1e-4));
                half alpha = _AlphaBase * edgeBlend + fresnel * (1.0 - edgeBlend) * 0.5;

                half3 color = lerp(diffuse + specular * _Smoothness, half3(0.7, 0.85, 1.0) * 0.35 + diffuse, fresnel * 0.35);

                color = MixFog(color, input.fogFactor);

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
