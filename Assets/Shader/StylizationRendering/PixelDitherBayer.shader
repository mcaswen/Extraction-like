Shader "TA/PixelDitherBayer"
{
    Properties
    {
        _BlitTexture ("Blit Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BayerDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D_X(_BlitTexture);                  // URP 推荐写法
            SAMPLER(sampler_PointClamp);

            float _DownSampleFactor;                    // 从 C# 传过来的降采样倍数

            // 经典 8x8 Bayer 矩阵（0~63）
            static const int bayerMatrix[8][8] = {
                { 0,32, 8,40, 2,34,10,42},
                {48,16,56,24,50,18,58,26},
                {12,44, 4,36,14,46, 6,38},
                {60,28,52,20,62,30,54,22},
                { 3,35,11,43, 1,33, 9,41},
                {51,19,59,27,49,17,57,25},
                {15,47, 7,39,13,45, 5,37},
                {63,31,55,23,61,29,53,21}
            };

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Point 采样低分辨率颜色（形成像素块）
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.uv);

                // 2. 关键修复：Bayer 坐标跟随低分辨率块（不再用全屏分辨率）
                //    input.uv * _ScreenParams.xy = 全屏像素坐标
                //    除以 _DownSampleFactor 后 = 低分辨率像素坐标
                float2 screenPos = input.uv * _ScreenParams.xy / _DownSampleFactor;
                int x = (int)screenPos.x % 8;
                int y = (int)screenPos.y % 8;

                float threshold = bayerMatrix[y][x] / 64.0;

                // 3. 亮度（Obra Dinn 经典权重）
                float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));

                // 4. 1-bit 抖动（纯黑白）
                float dithered = step(threshold, lum);

                return half4(dithered, dithered, dithered, 1.0);
            }
            ENDHLSL
        }
    }
}