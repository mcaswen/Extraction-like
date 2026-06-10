Shader "TA/Toon/URP Toon Lit Outline"
{
    Properties
    {
        [Header(Toon Lighting)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.72, 0.68, 0.62, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.1
        _ShadowSoftness ("Shadow Softness", Range(0.001, 1)) = 0.08
        _IndirectStrength ("Indirect Strength", Range(0, 2)) = 0.35
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.15
        _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 0.78
        _SpecularSoftness ("Specular Softness", Range(0.001, 1)) = 0.05

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Per Object Inverted Hull Outline)]
        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Enable Outline Pass", Float) = 1
        _OutlineThickness ("Outline Thickness Pixels", Range(0, 12)) = 2
        _OutlineNearThicknessScale ("Near Thickness Scale", Range(0, 4)) = 1
        _OutlineFarThicknessScale ("Far Thickness Scale", Range(0, 4)) = 0.35
        _OutlineDepthFadeStart ("Distance Thickness Start", Float) = 8
        _OutlineDepthFadeEnd ("Distance Thickness End", Float) = 45
        _OutlineDistanceThicknessPower ("Distance Thickness Curve", Range(0.1, 8)) = 1
        _OutlineIntensity ("Outline Intensity", Range(0, 4)) = 1
        _OutlineZOffset ("Outline Depth Offset", Range(-0.01, 0.01)) = 0
        _SmoothNormalSource ("Smooth Normal Source 0 Tangent 1 UV2", Range(0, 1)) = 0
        _SmoothNormalBlend ("Smooth Normal Blend", Range(0, 1)) = 1
        _OutlineColor ("Default Outline Color", Color) = (0.03, 0.025, 0.022, 1)
        _OutlineHairColor ("Hair Outline Color", Color) = (0.018, 0.015, 0.012, 1)
        _OutlineClothColor ("Cloth Outline Color", Color) = (0.035, 0.038, 0.055, 1)
        _OutlineSkinColor ("Skin Outline Color", Color) = (0.18, 0.095, 0.065, 1)
        _OutlineAccentColor ("Accent Outline Color", Color) = (0.04, 0.025, 0.08, 1)
        _OutlineMaskMap ("Outline Region Mask RGBA", 2D) = "black" {}
        _UseOutlineMaskMap ("Use Outline Mask Map", Range(0, 1)) = 0
        _UseLightmapAlphaForOutlineRegion ("Use Lightmap Alpha Region", Range(0, 1)) = 0
        _OutlineRegionContrast ("Region Mask Contrast", Range(0, 4)) = 1
        _OutlineRegionBlend ("Region Color Blend", Range(0, 1)) = 1

        [Header(Face Outline Control)]
        _FaceMaskMap ("Face Mask R Suppress", 2D) = "black" {}
        _FaceOutlineSuppress ("Face Outline Suppress", Range(0, 1)) = 0.65
        _FaceViewSuppress ("Face Front View Suppress", Range(0, 1)) = 0.5
        _FaceViewPower ("Face View Falloff", Range(0.25, 8)) = 2

        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonForwardVertex
            #pragma fragment ToonForwardFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonForwardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInput.positionCS;
                output.positionWS = positionInput.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half ToonRamp(half ndl)
            {
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                return lit;
            }

            half4 ToonForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
#if defined(_ALPHATEST_ON)
                clip(baseSample.a - _Cutoff);
#endif

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = dot(normalWS, mainLight.direction);
                half litStep = ToonRamp(ndl);
                half3 indirect = SampleSH(normalWS) * _IndirectStrength;
                half3 direct = lerp(_ShadeColor.rgb, half3(1.0h, 1.0h, 1.0h), litStep) * mainLight.color * mainLight.shadowAttenuation;
                half3 color = baseSample.rgb * max(direct, indirect);

#if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half addNdl = saturate(dot(normalWS, light.direction));
                    color += baseSample.rgb * light.color * light.distanceAttenuation * light.shadowAttenuation * ToonRamp(addNdl) * 0.35h;
                }
#endif

                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half specTerm = smoothstep(_SpecularThreshold - _SpecularSoftness, _SpecularThreshold + _SpecularSoftness, saturate(dot(normalWS, halfDir)));
                color += _SpecColor.rgb * specTerm * _SpecularStrength * mainLight.shadowAttenuation;

                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ToonOutline" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shader/ToonOutlines/URPToonOutlineCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 smoothNormalOS = TA_DecodeSmoothNormalOS(input.normalOS, input.tangentOS, input.uv2);
                float3 normalWS = TransformObjectToWorldNormal(smoothNormalOS);
                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = TA_ApplyScreenSpaceOutlineOffset(input.positionOS.xyz, smoothNormalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.lightmapUV = input.uv2.xy;
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInput.positionWS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if !defined(_OUTLINE_ON)
                discard;
#endif

#if defined(_ALPHATEST_ON)
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
#endif

                ToonOutlineColorData data = TA_GetOutlineColorData(input.uv, input.lightmapUV, input.normalWS, input.viewDirWS);
                clip(data.color.a - 0.001h);
                return data.color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Assets/Shader/ToonOutlines/URPToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
