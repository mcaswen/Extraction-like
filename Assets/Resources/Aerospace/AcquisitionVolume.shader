Shader "Aerospace/Private Acquisition Volume"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz);o.color=i.color;o.uv=i.uv;return o; }
            half4 frag(V i):SV_Target
            {
                float edge=abs(i.uv.y*2-1);
                float feather=1-smoothstep(.40,1,edge);
                return half4(i.color.rgb,i.color.a*feather);
            }
            ENDHLSL
        }
    }
}
