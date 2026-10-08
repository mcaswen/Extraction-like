Shader "ExtractionLike/Skybox/Aerospace Day Night Blend"
{
    Properties
    {
        [NoScaleOffset] _DayTex("Day HDR (2D Panorama)", 2D) = "white" {}
        [NoScaleOffset] _NightTex("Night HDR (2D Panorama)", 2D) = "black" {}
        _Blend("Night Blend", Range(0, 1)) = 0
        _DayExposure("Day Exposure (Multiplier)", Range(0, 4)) = 0.55
        _NightExposure("Night Exposure (Multiplier)", Range(0, 1)) = 0.08
        _Rotation("Panorama Rotation", Range(0, 360)) = 130
        [HDR] _DayTint("Day / Twilight Tint", Color) = (1, 1, 1, 1)
        _NightTint("Night Blue Tint", Color) = (0.5, 0.72, 1, 1)
        [HDR] _NightFloor("Night Visibility Floor", Color) = (0.006, 0.014, 0.04, 1)
        [HDR] _HorizonColor("Night Horizon Glow", Color) = (0.02, 0.035, 0.07, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_DayTex); SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
            CBUFFER_START(UnityPerMaterial)
                float _Blend, _DayExposure, _NightExposure, _Rotation;
                half4 _DayTint, _NightTint, _NightFloor, _HorizonColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.direction);
                float2 uv = float2(frac(0.5 - atan2(direction.z, direction.x) / TWO_PI + _Rotation / 360.0),
                                   1.0 - acos(clamp(direction.y, -1.0, 1.0)) / PI);
                half3 day = SAMPLE_TEXTURE2D(_DayTex, sampler_DayTex, uv).rgb * _DayExposure * _DayTint.rgb;
                half3 night = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, uv).rgb * _NightExposure * _NightTint.rgb;
                // Keep the real Milky Way, but conceal the source HDRI's desert below the horizon.
                night = lerp(_HorizonColor.rgb, night + _NightFloor.rgb, smoothstep(0.0, 0.12, direction.y));
                half3 color = lerp(day, night, saturate(_Blend));
                #ifdef UNITY_COLORSPACE_GAMMA
                    color = LinearToSRGB(color);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
