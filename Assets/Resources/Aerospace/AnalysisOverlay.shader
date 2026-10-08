Shader "Aerospace/Private Analysis Overlay"
{
    Properties { _BaseColor("Tint", Color)=(.36,.8,.9,1) _EdgeOnly("Section silhouette",Float)=0 _Opacity("Opacity",Float)=1 _ZTest("Depth test",Float)=4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _EdgeOnly, _Opacity;
            CBUFFER_END
            struct A { float4 position:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float4 color:COLOR; };
            V vert(A i) { V o; o.world=TransformObjectToWorld(i.position.xyz);o.position=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color;return o; }
            half4 frag(V i):SV_Target
            {
                float edge=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),5);
                float4 tint=lerp(i.color,_BaseColor,_EdgeOnly);
                return half4(tint.rgb,tint.a*_Opacity*lerp(1,edge,_EdgeOnly));
            }
            ENDHLSL
        }
    }
}
