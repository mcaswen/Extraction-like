Shader "Aerospace/Landing Soft Smoke"
{
    Properties
    {
        _BaseMap("Soft cloud", 2D) = "white" {}
        _BaseColor("Tint", Color) = (.45,.46,.47,.4)
        _VertexColor("Particle color", Float) = 1
        _Motion("Slow flow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "LandingSmoke"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _VertexColor;
                float _Motion;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 position=i.positionOS.xyz;
                position.x+=sin(_Time.y*.48+i.uv.y*5.2)*.12*i.uv.y*_Motion;
                o.positionCS=TransformObjectToHClip(position);
                o.uv=i.uv; o.color=lerp(half4(1,1,1,1),i.color,_VertexColor);
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.uv+float2(sin(_Time.y*.36+i.uv.y*5)*.045,cos(_Time.y*.26)*.035)*_Motion;
                half cloud=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a;
                half pulse=lerp(1,.88+.12*sin(_Time.y*.6+i.uv.y*4),_Motion);
                half4 c=_BaseColor*i.color;
                c.a*=saturate(cloud*2.4)*pulse;
                c.rgb=MixFog(c.rgb,i.fog); return c;
            }
            ENDHLSL
        }
    }
}
