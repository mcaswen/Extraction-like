Shader "Aerospace/Landing Surface"
{
    Properties
    {
        _BaseMap("Base", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = .1
        _Smoothness("Smoothness", Range(0,1)) = .4
        _EmissionColor("Masked status lens", Color) = (0,0,0,1)
        _DetailNormalSoftening("Local detail outline softening", Range(0,.95)) = .78
        _Cutoff("Cutoff", Range(0,1)) = .5
        _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Metallic;
                float _Smoothness;
                float _Cutoff;
                float4 _EmissionColor;
                float _DetailNormalSoftening;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half4 color:COLOR;
                half fog:TEXCOORD2;
                float4 shadowCoord:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.color=input.color; o.fog=ComputeFogFactor(p.positionCS.z); o.shadowCoord=GetShadowCoord(p); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                InputData d=(InputData)0;
                d.positionWS=i.positionWS; d.positionCS=i.positionCS; d.normalWS=NormalizeNormalPerPixel(i.normalWS);
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); d.shadowCoord=i.shadowCoord;
                d.fogCoord=i.fog; d.vertexLighting=VertexLighting(i.positionWS,d.normalWS); d.bakedGI=SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask=half4(1,1,1,1);
                SurfaceData s=(SurfaceData)0;
                s.albedo=i.color.rgb*_BaseColor.rgb; s.alpha=1; s.metallic=_Metallic; s.smoothness=_Smoothness;
                s.normalTS=half3(0,0,1); s.occlusion=1;
                s.emission=i.color.a*_EmissionColor.rgb*(.84+.16*sin(_Time.y*1.3));
                half4 color=UniversalFragmentPBR(d,s); color.rgb=MixFog(color.rgb,d.fogCoord); return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        // Soften only this prop's small panel-normal discontinuities. Real depth,
        // silhouette, lighting normals and the global Sobel configuration stay intact.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineNormalVertex
            #pragma fragment OutlineNormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Metallic;
                float _Smoothness;
                float _Cutoff;
                float4 _EmissionColor;
                float _DetailNormalSoftening;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings {
                float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings OutlineNormalVertex(Attributes input)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // FBX keeps Blender's +Z height in mesh space, with a node rotation to Unity +Y.
                float3 p=input.positionOS.xyz;
                float2 radial=p.xy;
                float active=smoothstep(5.4,6.4,p.z)*(1-smoothstep(2.25,2.55,length(radial)));
                float coneSlope=p.z>20.30 ? .36 : 0;
                if(abs(p.x)>2.4 && p.z<11.45) {
                    radial-=float2(sign(p.x)*3.18,0);
                    active=smoothstep(3.0,4.0,p.z)*(1-smoothstep(1.0,1.2,length(radial)));
                    coneSlope=p.z>9.02 ? .35 : 0;
                }
                float radius=length(radial);
                float3 broad=radius>.0001 ? normalize(float3(radial/radius,coneSlope)) : input.normalOS;
                float blend=active*_DetailNormalSoftening;
                float3 normalOS=SafeNormalize(lerp(input.normalOS,broad,blend));
                o.normalWS=TransformObjectToWorldNormal(normalOS);
                o.positionCS=TransformObjectToHClip(p); return o;
            }
            void OutlineNormalFragment(Varyings i, out half4 normal:SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                ,out float4 renderingLayers:SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=NormalizeNormalPerPixel(i.normalWS);
                #ifdef _GBUFFER_NORMALS_OCT
                normal=half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                normal=half4(n,0);
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                renderingLayers=float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()),0,0,0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
