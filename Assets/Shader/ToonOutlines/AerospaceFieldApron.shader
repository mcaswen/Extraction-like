Shader "Aerospace/Field Apron"
{
    Properties { _BaseColor("Work surface", Color) = (.24,.26,.23,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.world=TransformObjectToWorld(i.p.xyz); o.p=TransformWorldToHClip(o.world);
                o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 d=min(i.uv,1-i.uv);
                float edge=min(d.x,d.y);
                float grain=frac(sin(dot(floor(i.world.xz*3),float2(12.9898,78.233)))*43758.5453);
                half alpha=smoothstep(.005,.085,edge+(grain-.5)*.018);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 normal=normalize(i.n);
                half3 light=SampleSH(normal)+sun.color*saturate(dot(normal,sun.direction))*sun.shadowAttenuation;
                float2 panel=abs(frac(i.world.xz*.65)-.5);
                half seam=1-.12*smoothstep(.46,.495,max(panel.x,panel.y));
                return half4(_BaseColor.rgb*(.96+.08*grain)*seam*max(light,.18),alpha*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
