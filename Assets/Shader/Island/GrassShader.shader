Shader "Grass/GrassShader"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _BottomColor ("Bottom Color", Color) = (1,1,1,1)
        _BendRotationRandom ("Bend Rotation Random", Range(0,1)) = 0.2
        _BladeHeightRandom ("Blade Height Random", Float) = 0.3
        _BladeHeight ("Blade Height", Float) = 0.5
        _BladeWidthRandom ("Blade Width Random", Float) = 0.02
        _BladeWidth ("Blade Width", Float) = 0.05
        _TessellationUniform ("Tessellation Uniform", Range(1,16)) = 4
        _WindDistortionMap ("Wind Distortion Map", 2D) = "white" {}
        _WindFrequency ("Wind Frequency", Vector) = (0.05,0.05,0,0)
        _WindStrength ("Wind Strength", Float) = 1
        _WaveDirection ("Wave Direction XZ", Vector) = (1,0,0,0)
        _WaveFrequency ("Wave Frequency", Float) = 1.5
        _WaveSpeed ("Wave Speed", Float) = 1
        _WaveStrength ("Wave Strength", Range(0,1)) = 0.15
        _BladeForward ("Blade Forward", Float) = 0.4
        _BladeCurve ("Blade Curve", Range(1,4)) = 2
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.25
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularStrength ("Specular Strength", Range(0,2)) = 0.25
        _Shininess ("Shininess", Range(8,128)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

        #define PI 3.1415926535
        #define TWO_PI 6.28318530718
        #define BLADE_SEGMENTS 3
        #define WIND_BEND_SCALE 0.25

        TEXTURE2D(_WindDistortionMap);
        SAMPLER(sampler_WindDistortionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BottomColor;
            float4 _WindDistortionMap_ST;
            float4 _WindFrequency;
            float4 _WaveDirection;
            float _BendRotationRandom;
            float _BladeHeight;
            float _BladeHeightRandom;
            float _BladeWidth;
            float _BladeWidthRandom;
            float _TessellationUniform;
            float _WindStrength;
            float _WaveFrequency;
            float _WaveSpeed;
            float _WaveStrength;
            float _BladeForward;
            float _BladeCurve;
            float _AmbientStrength;
            float4 _SpecularColor;
            float _SpecularStrength;
            float _Shininess;
        CBUFFER_END

        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct ControlPoint
        {
            float3 positionOS : TEXCOORD0;
            float3 normalOS : TEXCOORD1;
            float4 tangentOS : TEXCOORD2;
            float2 uv : TEXCOORD3;
        };

        struct TessellationFactors
        {
            float edge[3] : SV_TessFactor;
            float inside : SV_InsideTessFactor;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionOS : TEXCOORD0;
            float3 normalOS : TEXCOORD1;
            float4 tangentOS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            float3 positionWS : TEXCOORD4;
            float3 normalWS : TEXCOORD5;
        };

        float Hash(float3 seed)
        {
            return frac(sin(dot(seed, float3(12.9898, 78.233, 53.539))) * 43758.5453);
        }

        float3 SafeNormalize(float3 value, float3 fallback)
        {
            return dot(value, value) > 1e-6 ? normalize(value) : fallback;
        }

        float2 SafeNormalize2(float2 value, float2 fallback)
        {
            return dot(value, value) > 1e-6 ? normalize(value) : fallback;
        }

        float3x3 AngleAxis3x3(float angle, float3 axis)
        {
            axis = SafeNormalize(axis, float3(0.0, 0.0, 1.0));

            float s, c;
            sincos(angle, s, c);

            float t = 1.0 - c;
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;

            return float3x3(
                t * x * x + c,     t * x * y - s * z, t * x * z + s * y,
                t * x * y + s * z, t * y * y + c,     t * y * z - s * x,
                t * x * z - s * y, t * y * z + s * x, t * z * z + c
            );
        }

        float4 GetGrassShadowPositionHClip(float3 positionOS, float3 normalOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 normalWS = TransformObjectToWorldNormal(normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

            float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

            return positionCS;
        }

        Varyings BuildVaryings(float3 positionOS, float3 normalOS, float4 tangentOS, float2 uv, bool shadowPass)
        {
            Varyings output = (Varyings)0;
            output.positionOS = positionOS;
            output.positionWS = TransformObjectToWorld(positionOS);
            output.normalOS = normalOS;
            output.normalWS = TransformObjectToWorldNormal(normalOS);
            output.tangentOS = tangentOS;
            output.uv = uv;

            if (shadowPass)
            {
                output.positionCS = GetGrassShadowPositionHClip(positionOS, normalOS);
            }
            else
            {
                output.positionCS = TransformWorldToHClip(output.positionWS);
            }

            return output;
        }

        ControlPoint vert(Attributes input)
        {
            ControlPoint output = (ControlPoint)0;
            output.positionOS = input.positionOS;
            output.normalOS = input.normalOS;
            output.tangentOS = input.tangentOS;
            output.uv = input.uv;
            return output;
        }

        TessellationFactors ZeroTessellationFactors()
        {
            TessellationFactors factors;
            factors.edge[0] = 0.0;
            factors.edge[1] = 0.0;
            factors.edge[2] = 0.0;
            factors.inside = 0.0;
            return factors;
        }

        TessellationFactors UniformTessellationFactors()
        {
            float tessellation = clamp(_TessellationUniform, 1.0, 16.0);

            TessellationFactors factors;
            factors.edge[0] = tessellation;
            factors.edge[1] = tessellation;
            factors.edge[2] = tessellation;
            factors.inside = tessellation;
            return factors;
        }

        bool IsPatchOutsideFrustum(InputPatch<ControlPoint, 3> patch)
        {
            float grassBoundsPadding =
                abs(_BladeHeight) + abs(_BladeHeightRandom) +
                abs(_BladeWidth) + abs(_BladeWidthRandom) +
                abs(_BladeForward);

            float3 p0 = TransformObjectToWorld(patch[0].positionOS);
            float3 p1 = TransformObjectToWorld(patch[1].positionOS);
            float3 p2 = TransformObjectToWorld(patch[2].positionOS);

            [unroll]
            for (int i = 0; i < 6; i++)
            {
                float4 plane = unity_CameraWorldClipPlanes[i];
                float d0 = dot(float4(p0, 1.0), plane);
                float d1 = dot(float4(p1, 1.0), plane);
                float d2 = dot(float4(p2, 1.0), plane);

                if (max(max(d0, d1), d2) < -grassBoundsPadding)
                {
                    return true;
                }
            }

            return false;
        }

        TessellationFactors PatchConstantFunction(InputPatch<ControlPoint, 3> patch)
        {
            if (IsPatchOutsideFrustum(patch))
            {
                return ZeroTessellationFactors();
            }

            return UniformTessellationFactors();
        }

        TessellationFactors ShadowPatchConstantFunction(InputPatch<ControlPoint, 3> patch)
        {
            return UniformTessellationFactors();
        }

        [domain("tri")]
        [partitioning("integer")]
        [outputtopology("triangle_cw")]
        [patchconstantfunc("PatchConstantFunction")]
        [outputcontrolpoints(3)]
        ControlPoint hull(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
        {
            return patch[id];
        }

        [domain("tri")]
        [partitioning("integer")]
        [outputtopology("triangle_cw")]
        [patchconstantfunc("ShadowPatchConstantFunction")]
        [outputcontrolpoints(3)]
        ControlPoint shadowHull(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
        {
            return patch[id];
        }

        [domain("tri")]
        Varyings domain(
            TessellationFactors factors,
            OutputPatch<ControlPoint, 3> patch,
            float3 bary : SV_DomainLocation
        )
        {
            float3 positionOS =
                patch[0].positionOS * bary.x +
                patch[1].positionOS * bary.y +
                patch[2].positionOS * bary.z;

            float2 uv =
                patch[0].uv * bary.x +
                patch[1].uv * bary.y +
                patch[2].uv * bary.z;

            float3 normalOS = SafeNormalize(
                patch[0].normalOS * bary.x +
                patch[1].normalOS * bary.y +
                patch[2].normalOS * bary.z,
                float3(0.0, 1.0, 0.0)
            );

            float3 tangentXYZ = SafeNormalize(
                patch[0].tangentOS.xyz * bary.x +
                patch[1].tangentOS.xyz * bary.y +
                patch[2].tangentOS.xyz * bary.z,
                float3(1.0, 0.0, 0.0)
            );

            float tangentW =
                patch[0].tangentOS.w * bary.x +
                patch[1].tangentOS.w * bary.y +
                patch[2].tangentOS.w * bary.z;

            return BuildVaryings(positionOS, normalOS, float4(tangentXYZ, tangentW >= 0.0 ? 1.0 : -1.0), uv, false);
        }

        float3x3 BuildTangentToObject(float3 normalOS, float4 tangentOS)
        {
            float3 tangent = SafeNormalize(tangentOS.xyz, float3(1.0, 0.0, 0.0));
            float tangentSign = tangentOS.w >= 0.0 ? 1.0 : -1.0;
            float3 binormal = SafeNormalize(cross(normalOS, tangent) * tangentSign, float3(0.0, 0.0, 1.0));

            return float3x3(
                tangent.x, binormal.x, normalOS.x,
                tangent.y, binormal.y, normalOS.y,
                tangent.z, binormal.z, normalOS.z
            );
        }

        float3x3 WindMatrix(float3 positionOS)
        {
            float2 windUV =
                positionOS.xz * _WindDistortionMap_ST.xy +
                _WindDistortionMap_ST.zw +
                _WindFrequency.xy * _Time.y;

            float2 windSample =
                SAMPLE_TEXTURE2D_LOD(_WindDistortionMap, sampler_WindDistortionMap, windUV, 0).xy * 2.0 - 1.0;

            float3 windAxis = SafeNormalize(float3(windSample.x, windSample.y, 0.0), float3(1.0, 0.0, 0.0));
            float windAngle = length(windSample) * _WindStrength * WIND_BEND_SCALE;

            return AngleAxis3x3(windAngle, windAxis);
        }

        float3x3 SineWaveMatrix(float3 positionOS)
        {
            float2 waveDirection = SafeNormalize2(_WaveDirection.xy, float2(1.0, 0.0));
            float phase = dot(positionOS.xz, waveDirection) * _WaveFrequency + _Time.y * _WaveSpeed;
            float waveAngle = sin(phase) * _WaveStrength;
            float3 waveAxis = SafeNormalize(float3(-waveDirection.y, waveDirection.x, 0.0), float3(1.0, 0.0, 0.0));

            return AngleAxis3x3(waveAngle, waveAxis);
        }

        Varyings MakeTriangleRoot(Varyings a, Varyings b, Varyings c, bool shadowPass)
        {
            float3 positionOS = (a.positionOS + b.positionOS + c.positionOS) / 3.0;
            float2 uv = (a.uv + b.uv + c.uv) / 3.0;
            float3 normalOS = SafeNormalize(a.normalOS + b.normalOS + c.normalOS, a.normalOS);

            float3 tangentXYZ = SafeNormalize(a.tangentOS.xyz + b.tangentOS.xyz + c.tangentOS.xyz, a.tangentOS.xyz);
            float tangentW = a.tangentOS.w + b.tangentOS.w + c.tangentOS.w;

            return BuildVaryings(positionOS, normalOS, float4(tangentXYZ, tangentW >= 0.0 ? 1.0 : -1.0), uv, shadowPass);
        }

        Varyings MakeGrassVertex(Varyings root, float3 localOffset, float2 uv, float3x3 transform, bool shadowPass)
        {
            float3 positionOS = root.positionOS + mul(transform, localOffset);
            float3 normalOS = SafeNormalize(mul(transform, float3(0.0, 1.0, 0.0)), root.normalOS);
            return BuildVaryings(positionOS, normalOS, root.tangentOS, uv, shadowPass);
        }

        void AppendGrassBlade(
            Varyings a,
            Varyings b,
            Varyings c,
            inout TriangleStream<Varyings> triStream,
            bool shadowPass
        )
        {
            Varyings root = MakeTriangleRoot(a, b, c, shadowPass);
            float3 positionOS = root.positionOS;

            float height = max(0.001, _BladeHeight + (Hash(positionOS.zyx) * 2.0 - 1.0) * _BladeHeightRandom);
            float width = max(0.001, _BladeWidth + (Hash(positionOS.xzy) * 2.0 - 1.0) * _BladeWidthRandom);
            float forward = Hash(positionOS.yyz) * _BladeForward;

            float3 normalOS = SafeNormalize(root.normalOS, float3(0.0, 1.0, 0.0));
            float3x3 tangentToObject = BuildTangentToObject(normalOS, root.tangentOS);
            float3x3 facingRotation = AngleAxis3x3(Hash(positionOS) * TWO_PI, float3(0.0, 0.0, 1.0));
            float3x3 bendRotation = AngleAxis3x3(Hash(positionOS.zzx) * _BendRotationRandom * PI, float3(-1.0, 0.0, 0.0));
            float3x3 windRotation = WindMatrix(positionOS);
            float3x3 waveRotation = SineWaveMatrix(positionOS);

            float3x3 baseTransform = mul(tangentToObject, facingRotation);
            float3x3 bentTransform = mul(mul(mul(baseTransform, bendRotation), windRotation), waveRotation);

            [unroll]
            for (int i = 0; i < BLADE_SEGMENTS; i++)
            {
                float t = i / (float)BLADE_SEGMENTS;
                float segmentHeight = height * t;
                float segmentWidth = width * (1.0 - t);
                float segmentForward = pow(t, _BladeCurve) * forward;

                float3x3 segmentTransform = bentTransform;
                if (i == 0)
                {
                    segmentTransform = baseTransform;
                }

                triStream.Append(MakeGrassVertex(
                    root,
                    float3(segmentWidth, segmentForward, segmentHeight),
                    float2(0.0, t),
                    segmentTransform,
                    shadowPass
                ));

                triStream.Append(MakeGrassVertex(
                    root,
                    float3(-segmentWidth, segmentForward, segmentHeight),
                    float2(1.0, t),
                    segmentTransform,
                    shadowPass
                ));
            }

            triStream.Append(MakeGrassVertex(
                root,
                float3(0.0, forward, height),
                float2(0.5, 1.0),
                bentTransform,
                shadowPass
            ));

            triStream.RestartStrip();
        }

        [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
        void geom(triangle Varyings input[3], inout TriangleStream<Varyings> triStream)
        {
            AppendGrassBlade(input[0], input[1], input[2], triStream, false);
        }

        [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
        void shadowGeom(triangle Varyings input[3], inout TriangleStream<Varyings> triStream)
        {
            AppendGrassBlade(input[0], input[1], input[2], triStream, true);
        }

        half4 frag(Varyings input) : SV_Target
        {
            half4 baseColor = lerp(_BottomColor, _TopColor, input.uv.y);

            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoord);

            half3 normalWS = SafeNormalize(input.normalWS, half3(0.0, 1.0, 0.0));
            half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS), half3(0.0, 0.0, 1.0));
            half3 lightDirectionWS = SafeNormalize(mainLight.direction, half3(0.0, 1.0, 0.0));
            half3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS, normalWS);

            half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
            half diffuse = saturate(dot(normalWS, lightDirectionWS));
            half specular = pow(saturate(dot(normalWS, halfDirectionWS)), _Shininess) * _SpecularStrength;

            half3 ambientColor = baseColor.rgb * _AmbientStrength;
            half3 diffuseColor = baseColor.rgb * mainLight.color * diffuse * attenuation;
            half3 specularColor = _SpecularColor.rgb * mainLight.color * specular * attenuation;

            return half4(ambientColor + diffuseColor + specularColor, baseColor.a);
        }

        half4 shadowFrag(Varyings input) : SV_Target
        {
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardGrass"
            Tags { "LightMode"="UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma geometry geom
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull shadowHull
            #pragma domain domain
            #pragma geometry shadowGeom
            #pragma fragment shadowFrag

            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
}
