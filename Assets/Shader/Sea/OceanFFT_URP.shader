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
        _SpecularColor ("Specular Tint (× dielectric F0)", Color) = (1, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.5
        _FresnelBias ("Fresnel Bias", Range(0, 0.2)) = 0.04
        _IndirectDiffuse ("Indirect Diffuse (SH)", Range(0, 1)) = 0.42
        _EnvironmentSpecular ("Environment Specular", Range(0, 1)) = 0.38

        [Header(Skybox Reflection)]
        _SkyboxReflectionBlend ("Skybox vs Probes Mix", Range(0, 1)) = 0.22
        _SkyboxHorizonBoost ("Extra Sky At Grazing", Range(0, 2)) = 0.55

        [Header(Refraction)]
        [HDR] _RefractionTint ("Refraction Tint", Color) = (0.82, 0.94, 1.0, 1)
        _RefractionStrength ("Screen UV Offset (TS Normal)", Range(0, 0.08)) = 0.022
        _RefractionBlend ("Refraction Blend", Range(0, 1)) = 0.92
        _RefractionAbsorption ("Depth Absorption On Scene", Range(0, 1)) = 0.55

        [Header(Alpha)]
        _AlphaBase ("Alpha Base", Range(0, 1)) = 0.65
        _EdgeSoftness ("Edge Softness", Range(0.01, 5)) = 1.2

        [Header(Near Shore Waves)]
        _ShoreDepthRange ("Shore Depth Range", Range(0.1, 40)) = 6.0
        _ShoreFalloff ("Shore Mask Falloff", Range(0.25, 8)) = 1.5
        _ShoreWaveAmplitude ("Shore Wave Amplitude", Range(0, 2)) = 0.12
        _ShoreWaveFrequency ("Shore Wave Frequency", Range(0.01, 1)) = 0.18
        _ShoreWaveSpeed ("Shore Wave Speed", Range(0, 8)) = 2.2
        _ShoreWaveDir ("Shore Wave Dir XZ", Vector) = (1, 0, 0.35, 0)
        _ShallowFFTScale ("Shallow FFT Height Scale", Range(0, 1)) = 0.55
        _ShoreNormalBlend ("Shore Normal Blend", Range(0, 1)) = 0.45
        [Header(Shore Foam)]
        _FoamDepth ("Foam Depth Threshold", Range(0.05, 8)) = 0.85
        _FoamColor ("Foam Color", Color) = (0.92, 0.95, 1.0, 1)
        _FoamStrength ("Foam Strength", Range(0, 2)) = 0.55
        _FoamCrestSharpness ("Foam Crest Sharpness", Range(0.5, 8)) = 2.5
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
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
                half _IndirectDiffuse;
                half _EnvironmentSpecular;
                half _SkyboxReflectionBlend;
                half _SkyboxHorizonBoost;
                half4 _RefractionTint;
                half _RefractionStrength;
                half _RefractionBlend;
                half _RefractionAbsorption;
                half _AlphaBase;
                half _EdgeSoftness;
                half _ShoreDepthRange;
                half _ShoreFalloff;
                half _ShoreWaveAmplitude;
                half _ShoreWaveFrequency;
                half _ShoreWaveSpeed;
                float4 _ShoreWaveDir;
                half _ShallowFFTScale;
                half _ShoreNormalBlend;
                half _FoamDepth;
                half4 _FoamColor;
                half _FoamStrength;
                half _FoamCrestSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
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
                float2 shoreWaveXZ : TEXCOORD5;
                float3 normalWS : TEXCOORD6;
                float4 tangentWS : TEXCOORD7;
                float3 bitangentWS : TEXCOORD8;
            };

            float2 OceanUV(float3 worldPos)
            {
                float2 o = _OceanOrigin.xz;
                return frac((worldPos.xz - o) / max(_PatchSize, 1e-4));
            }

            // FFT 贴图：R=nx，G=nz（OceanFFTGenerator）；切线空间分量 (nx, nz, ny)，ny 由单位长度重建。
            void SampleOceanNormalTS(float2 oceanUV, out float3 nTS)
            {
                float2 ng = SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, oceanUV, 0).rg;
                float nx = ng.r;
                float nz = ng.g;
                float ny = sqrt(max(1.0 - nx * nx - nz * nz, 0.0));
                nTS = float3(nx, nz, ny);
            }

            float3 TangentNormalToWorld(float3 nTS, float3 T, float3 B, float3 N)
            {
                return normalize(T * nTS.x + B * nTS.y + N * nTS.z);
            }

            float2 ShoreWaveDir2()
            {
                float2 d = _ShoreWaveDir.xz;
                float len2 = dot(d, d);
                return len2 > 1e-6 ? d * rsqrt(len2) : float2(1, 0);
            }

            // 不在 VS 中采样 _CameraDepthTexture：vs_4_0 无法映射该表达式；浅水/近岸仅在 PS 中处理。
            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 positionOS = input.positionOS.xyz;
                float3 worldFlat = TransformObjectToWorld(positionOS);

                float2 ouv = OceanUV(worldFlat);
                float hFFT = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, ouv, 0).r * _HeightScale;
                float3 worldPos = worldFlat;
                worldPos.y += hFFT;

                o.positionWS = worldPos;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.shoreWaveXZ = worldFlat.xz;

                VertexNormalInputs vni = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = vni.normalWS;
                o.tangentWS = float4(vni.tangentWS, input.tangentOS.w);
                o.bitangentWS = vni.bitangentWS;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 oceanUV = OceanUV(input.positionWS);

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

                float shoreMask = saturate(1.0 - waterThickness / max(_ShoreDepthRange, 1e-4));
                shoreMask = pow(shoreMask, _ShoreFalloff);

                float3 N = normalize(input.normalWS);
                float3 T = normalize(input.tangentWS.xyz);
                float3 B = normalize(input.bitangentWS);

                float3 nTS;
                SampleOceanNormalTS(oceanUV, nTS);
                float3 normalWS = TangentNormalToWorld(nTS, T, B, N);

                float t = _TimeParameters.x;

                half3 viewDir = normalize(input.viewDirWS);
                half NdotV = saturate(dot(normalWS, viewDir));
                half fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - NdotV, _FresnelPower);

                float depthFactor = 1.0 - exp(-waterThickness / max(_DepthFadeDistance, 1e-4));
                half3 baseCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(depthFactor));

                half edgeBlend = saturate(waterThickness / max(_EdgeSoftness, 1e-4));
                half refractMask = (half)(1.0 - skyMask) * edgeBlend;

                // 折射管线：不透明物体已写入 _CameraOpaqueTexture → 用切线空间法线经 TBN→世界→视图，偏移屏幕 UV → SampleSceneColor。
                float3 nVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 refractUV = screenUV + nVS.xy * (float)_RefractionStrength;
                refractUV = clamp(refractUV, float2(0.001, 0.001), float2(0.999, 0.999));
                half3 sceneRefract = SampleSceneColor(refractUV);
                half refractionWeight = saturate(_RefractionBlend * refractMask * (1.0 - fresnel));
                half absorb = exp(-waterThickness / max(_DepthFadeDistance, 1e-4));
                half3 refractedCol = sceneRefract * _RefractionTint.rgb * lerp((half)1.0, absorb, (half)_RefractionAbsorption);
                // 折射主导：透过水看到的是单次折射采样 + 吸收染色；仅在无有效折射权重时保留体积色，减轻“叠两层”的假重影。
                half3 waterBase = lerp(baseCol, refractedCol, refractionWeight);

                float2 dir = ShoreWaveDir2();
                float k = _ShoreWaveFrequency * 6.2831853;
                float phase = dot(input.shoreWaveXZ, dir) * k + t * _ShoreWaveSpeed;
                float waveDeriv = abs(cos(phase)) + abs(cos(phase * 2.03 + 1.7)) * 0.35 * 2.03;
                float foamByCrest = pow(saturate(waveDeriv), _FoamCrestSharpness);
                float foamByDepth = saturate(1.0 - waterThickness / max(_FoamDepth, 1e-4));
                half foam = saturate(foamByDepth * shoreMask * foamByCrest) * _FoamStrength;

                BRDFData brdfData;
                {
                    half3 f0 = kDielectricSpec.rgb * _SpecularColor.rgb;
                    half refl = ReflectivitySpecular(f0);
                    half oneMinusRefl = half(1.0) - refl;
                    half3 diffuseTerm = waterBase * oneMinusRefl;
                    half brdfAlpha = 1.0h;
                    InitializeBRDFDataDirect(waterBase, diffuseTerm, f0, refl, oneMinusRefl, _Smoothness, brdfAlpha, brdfData);
                }

                half3 bakedGI = SampleSH(normalWS) * _IndirectDiffuse;
                half fresnelTermGI = Pow4(1.0 - NdotV);
                half3 reflectVector = reflect(-viewDir, normalWS);
                half3 envFromPipeline = GlossyEnvironmentReflection(reflectVector, input.positionWS, brdfData.perceptualRoughness, 1.0h, screenUV);
                half3 indirectSpecular = envFromPipeline;
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
                {
                    half skyMip = PerceptualRoughnessToMipmapLevel(brdfData.perceptualRoughness);
                    half4 skyEnc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, skyMip);
                    half3 skyboxRefl = DecodeHDREnvironment(skyEnc, unity_SpecCube0_HDR);
                    half skyMix = saturate(_SkyboxReflectionBlend + (1.0h - NdotV) * _SkyboxHorizonBoost);
                    indirectSpecular = lerp(envFromPipeline, skyboxRefl, skyMix);
                }
#endif
                indirectSpecular *= _EnvironmentSpecular;
                half3 indirectLit = EnvironmentBRDF(brdfData, bakedGI, indirectSpecular, fresnelTermGI);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 directLit = LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDir);

                half alpha = _AlphaBase * edgeBlend + fresnel * (1.0 - edgeBlend) * 0.5;

                half3 horizonTint = half3(0.62, 0.78, 0.95);
                half3 color = indirectLit + directLit;
                color = lerp(color, color + horizonTint * 0.22, fresnel * 0.28);
                color = lerp(color, _FoamColor.rgb, foam);

                color = MixFog(color, input.fogFactor);

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
