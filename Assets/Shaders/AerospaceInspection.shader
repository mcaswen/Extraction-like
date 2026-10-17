Shader "Aerospace/Inspection PBR"
{
    Properties
    {
        _BaseMap("Base color",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _NormalMap("Normal",2D)="bump"{}
        _PackedMap("Metallic / smoothness OR recovered ORM",2D)="white"{}
        _AOMap("AO",2D)="white"{}
        _UseORM("Recovered ORM packing",Float)=0
        _Graphic("Graphic surface",Float)=0
        _Studio("Private studio reflection",Cube)="black"{}
        _Highlight("Selection",Float)=0
        _Visibility("Section visibility",Float)=1
        _ContextDim("Context dimming",Float)=0
        _Scan("Scan progress",Float)=-1
        _ScanHeight("Scan plane",Float)=0
        _Acquisition("Temporary sample projection",Float)=0
        _AcquisitionOrigin("Projection origin",Vector)=(0,0,0,0)
        _AcquisitionAge("Projection clock",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "PrivateStudio"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_PackedMap); SAMPLER(sampler_PackedMap);
            TEXTURE2D(_AOMap); SAMPLER(sampler_AOMap);
            TEXTURECUBE(_Studio); SAMPLER(sampler_Studio);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor; float _UseORM, _Graphic, _Highlight, _Visibility, _ContextDim, _Scan, _ScanHeight, _Acquisition;
            float4 _AcquisitionOrigin; float _AcquisitionAge;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float4 t:TEXCOORD2; float2 uv:TEXCOORD3; };
            V vert(A i) { V o; o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.t=float4(TransformObjectToWorldDir(i.t.xyz),i.t.w*GetOddNegativeScale());o.uv=i.uv;return o; }
            float3 lamp(float3 n,float3 v,float3 l,float3 color,float3 base,float metal,float rough)
            {
                float3 h=normalize(v+l);float nl=saturate(dot(n,l)),nv=max(.001,saturate(dot(n,v))),nh=saturate(dot(n,h)),vh=saturate(dot(v,h));
                float a=max(.03,rough*rough),a2=a*a;float d=a2/(PI*pow(nh*nh*(a2-1)+1,2)+.0001);
                float k=(rough+1)*(rough+1)/8;float g=(nl/(nl*(1-k)+k+.0001))*(nv/(nv*(1-k)+k));
                float3 f0=lerp(.04.xxx,base,metal),f=f0+(1-f0)*pow(1-vh,5);
                return (base*(1-metal)*(1-f)/PI+d*g*f/max(.004,4*nl*nv))*nl*color;
            }
            half4 frag(V i):SV_Target
            {
                // Screen-door fade keeps opaque depth correct while opening a teaching section.
                float dither=frac(52.9829189*frac(dot(floor(i.p.xy),float2(.06711056,.00583715))));
                clip(_Visibility-.001-dither*.998);
                float3 n=normalize(i.n),t=normalize(i.t.xyz);float3 b=cross(n,t)*i.t.w;
                float3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv));n=normalize(t*normal.x+b*normal.y+n*normal.z);
                float3 v=normalize(_WorldSpaceCameraPos-i.w);
                float3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                float4 packed=SAMPLE_TEXTURE2D(_PackedMap,sampler_PackedMap,i.uv);
                float metal=lerp(packed.r,packed.b,_UseORM)*(1-_Graphic);
                float rough=clamp(lerp(1-packed.a,packed.g,_UseORM),.16,.92);rough=lerp(rough,.5,_Graphic);
                float ao=lerp(SAMPLE_TEXTURE2D(_AOMap,sampler_AOMap,i.uv).r,packed.r,_UseORM);
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20,up=UNITY_MATRIX_I_V._m01_m11_m21;
                float3 key=normalize(v*.70+right*.58+up*.82),fill=normalize(v*.42-right*.85+up*.4),rim=normalize(-v*.45-right*.8+up*.55);
                // Broad white key plus a cold rim: keep the metal/ceramic colours legible.
                float3 color=lamp(n,v,key,float3(2.8,2.92,3.12),base,metal,max(rough,.31))
                    +lamp(n,v,normalize(key+right*.30),float3(.65,.70,.79),base,metal,max(rough,.35))
                    +lamp(n,v,fill,float3(.75,1.08,1.52),base,metal,max(rough,.30))
                    +lamp(n,v,rim,float3(.85,1.75,2.65),base,metal,max(rough,.28));
                float3 f0=lerp(.04.xxx,base,metal);
                float3 env=SAMPLE_TEXTURECUBE_LOD(_Studio,sampler_Studio,reflect(-v,n),rough*5).rgb;
                color+=(base*(1-metal)*.26+env*(f0+(1-f0)*pow(1-saturate(dot(n,v)),5))*.90)*lerp(.4,1,ao);
                color*=lerp(.75,1,ao)*(1-_ContextDim);
                float edge=pow(1-saturate(dot(n,v)),3);
                color+=float3(.25,.85,1.2)*_Highlight*(.16+edge*.9);
                // A single finite scan reveals the original material; no texture or mesh replacement.
                // Antialiased measurement lattice in sample space, independent of texture UVs and part size.
                // A stable private-stage origin avoids precision loss at the isolated stage's -10000 Y offset.
                if(_Acquisition>.001)
                {
                    float unscanned=1-smoothstep(_ScanHeight-.012,_ScanHeight+.012,i.w.y);
                    float3 q=(i.w-_AcquisitionOrigin.xyz)*34;
                    float3 cells=abs(frac(q-.5)-.5)/max(fwidth(q),.04);
                    float3 lines=1-smoothstep(.45,1.35,cells);
                    float3 weight=pow(abs(n),4); weight/=max(.001,weight.x+weight.y+weight.z);
                    float lattice=dot(weight,float3(max(lines.y,lines.z),max(lines.x,lines.z),max(lines.x,lines.y)));
                    float points=dot(weight,float3(lines.y*lines.z,lines.x*lines.z,lines.x*lines.y));
                    float3 projection=float3(.006,.023,.057)+color*.08
                        +float3(.035,.24,.44)*lattice+float3(.15,.57,.74)*points
                        +float3(.08,.62,.97)*pow(edge,.7);
                    color=lerp(color,projection,_Acquisition*unscanned);
                }
                float distanceToScan=i.w.y-_ScanHeight;
                float scanBand=exp(-pow(distanceToScan/.007,2))*step(0,_Scan);
                float scanWake=exp(-max(0,distanceToScan)/.045)*step(0,distanceToScan)*step(0,_Scan);
                color+=float3(.24,.95,1.7)*scanBand*(.7+edge)+float3(.025,.19,.38)*scanWake;
                // One gentle material-lock rim, not a repeating flash or a screen-wide white pulse.
                float lockPulse=smoothstep(1.23,1.29,_AcquisitionAge)*(1-smoothstep(1.29,1.53,_AcquisitionAge));
                color+=float3(.12,.42,.72)*edge*lockPulse;
                // Local filmic response: independent of the world's day/night grading and exposure.
                color=(color*(2.51*color+.03))/(color*(2.43*color+.59)+.14);
                return half4(saturate(color),1);
            }
            ENDHLSL
        }
    }
}
