Shader "Unlit/EdgeExtend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}     
        _OutLineColor ("Out Line Color", Color) = (1,1,1,1)
        _OutLineLength ("OutLine Length", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" } 

        Pass
        {
            Name "Outline"
            Cull Front          
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _OutLineColor;
            float _OutLineLength;

            v2f vert (appdata v)
            {
                v2f o;

                // （沿法线方向向外扩展）
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos += worldNormal * _OutLineLength;

                o.vertex = UnityWorldToClipPos(worldPos);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutLineColor;
            }
            ENDCG
        }

         Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        // Pass { ... 正常渲染 _MainTex ... }
    }
}
