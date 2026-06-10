Shader "Hidden/TA/Toon/Sobel Outline"
{
    Properties
    {
        _BlitTexture ("Blit Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SobelOutline"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_LinearClamp);

            half4 _SobelOutlineColor;
            half _SobelIntensity;
            half _SobelDepthThreshold;
            half _SobelNormalThreshold;
            half _SobelDepthWeight;
            half _SobelNormalWeight;
            half _SobelThickness;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float SampleLinearEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float3 SampleNormalWS(float2 uv)
            {
                return normalize(SampleSceneNormals(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);

                float2 texel = _ScreenParams.zw - 1.0;
                texel *= max(_SobelThickness, 1.0h);

                float2 uv = input.uv;
                float depthCenter = SampleLinearEyeDepth(uv);
                float depthL = SampleLinearEyeDepth(uv + float2(-texel.x, 0.0));
                float depthR = SampleLinearEyeDepth(uv + float2(texel.x, 0.0));
                float depthU = SampleLinearEyeDepth(uv + float2(0.0, texel.y));
                float depthD = SampleLinearEyeDepth(uv + float2(0.0, -texel.y));

                float depthScale = max(depthCenter, 0.001);
                float depthEdge = (abs(depthR - depthL) + abs(depthU - depthD)) / depthScale;

                float3 normalL = SampleNormalWS(uv + float2(-texel.x, 0.0));
                float3 normalR = SampleNormalWS(uv + float2(texel.x, 0.0));
                float3 normalU = SampleNormalWS(uv + float2(0.0, texel.y));
                float3 normalD = SampleNormalWS(uv + float2(0.0, -texel.y));
                float normalEdge = length(normalR - normalL) + length(normalU - normalD);

                half depthMask = smoothstep(_SobelDepthThreshold, _SobelDepthThreshold * 2.0h + 0.0001h, depthEdge * _SobelDepthWeight);
                half normalMask = smoothstep(_SobelNormalThreshold, _SobelNormalThreshold * 2.0h + 0.0001h, normalEdge * _SobelNormalWeight);
                half edge = saturate(max(depthMask, normalMask) * _SobelIntensity);

                half3 color = lerp(source.rgb, _SobelOutlineColor.rgb, edge * _SobelOutlineColor.a);
                return half4(color, source.a);
            }
            ENDHLSL
        }
    }
}
