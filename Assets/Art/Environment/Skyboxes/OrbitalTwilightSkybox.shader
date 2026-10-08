Shader "ExtractionLike/Skybox/Orbital Twilight"
{
    Properties
    {
        [HDR] _ZenithColor("Zenith Color", Color) = (0.055, 0.30, 0.75, 1)
        [HDR] _HorizonColor("Horizon Glow", Color) = (0.28, 0.62, 0.95, 1)
        [HDR] _NadirColor("Nadir Color", Color) = (0.07, 0.18, 0.34, 1)
        _HorizonSharpness("Horizon Sharpness", Range(1, 16)) = 4.5
        _HorizonIntensity("Horizon Intensity", Range(0, 3)) = 1.1

        _SunDirection("Sun Direction", Vector) = (0, 0.77, -0.64, 0)
        [HDR] _SunColor("Sun Color", Color) = (1, 0.86, 0.64, 1)
        _SunSize("Sun Size", Range(0.97, 0.9999)) = 0.996
        _SunHaloPower("Sun Halo Focus", Range(8, 256)) = 64
        _SunIntensity("Sun Intensity", Range(0, 5)) = 1.25

        [HDR] _StarColor("Star Color", Color) = (0.72, 0.86, 1.0, 1)
        _StarDensity("Star Density", Range(40, 320)) = 145
        _StarSize("Star Size", Range(0.02, 0.24)) = 0.075
        _StarIntensity("Star Intensity", Range(0, 5)) = 0.28

        [HDR] _NebulaColor("Orbital Dust Color", Color) = (0.14, 0.23, 0.48, 1)
        _NebulaIntensity("Orbital Dust Intensity", Range(0, 1)) = 0.018
        _Rotation("Rotation", Range(0, 360)) = 18
        _Exposure("Exposure", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "OrbitalTwilightSkybox"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _NadirColor;
                half4 _SunColor;
                half4 _StarColor;
                half4 _NebulaColor;
                float _HorizonSharpness;
                float _HorizonIntensity;
                float4 _SunDirection;
                float _SunSize;
                float _SunHaloPower;
                float _SunIntensity;
                float _StarDensity;
                float _StarSize;
                float _StarIntensity;
                float _NebulaIntensity;
                float _Rotation;
                float _Exposure;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float2 Hash22(float2 value)
            {
                float first = Hash21(value);
                float second = Hash21(value + 37.17);
                return float2(first, second);
            }

            float3 StarLayer(float2 sphericalUV, float density, float size, float seed)
            {
                float2 grid = sphericalUV * float2(density * 2.0, density);
                float2 cell = floor(grid);
                float2 localPosition = frac(grid);
                float2 starPosition = Hash22(cell + seed);
                float distanceToStar = length(localPosition - starPosition);
                float antiAlias = max(fwidth(distanceToStar), 0.002);

                float starSeed = Hash21(cell + seed * 3.71);
                float visible = step(0.988, starSeed);
                float disc = 1.0 - smoothstep(size - antiAlias, size + antiAlias, distanceToStar);
                float core = 1.0 - smoothstep(size * 0.16, size * 0.48 + antiAlias, distanceToStar);
                float brightness = lerp(0.32, 1.0, Hash21(cell + seed * 7.13));
                float warmStar = step(0.93, Hash21(cell + seed * 11.91));
                float3 starTint = lerp(_StarColor.rgb, float3(1.0, 0.72, 0.48), warmStar * 0.45);

                return starTint * visible * (disc * brightness + core * 0.75);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionOS);
                float rotationRadians = radians(_Rotation);
                float sineRotation;
                float cosineRotation;
                sincos(rotationRadians, sineRotation, cosineRotation);
                direction.xz = mul(float2x2(cosineRotation, -sineRotation, sineRotation, cosineRotation), direction.xz);

                float verticalBlend = smoothstep(-0.28, 0.82, direction.y);
                float3 skyColor = lerp(_NadirColor.rgb, _ZenithColor.rgb, verticalBlend);

                float horizon = pow(saturate(1.0 - abs(direction.y)), _HorizonSharpness);
                skyColor += _HorizonColor.rgb * horizon * _HorizonIntensity;

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = dot(direction, sunDirection);
                float sunDisc = smoothstep(_SunSize, min(1.0, _SunSize + 0.0015), sunAlignment);
                float sunHalo = pow(saturate(sunAlignment), _SunHaloPower) * 0.22;
                skyColor += _SunColor.rgb * (sunDisc + sunHalo) * _SunIntensity;

                float2 sphericalUV;
                sphericalUV.x = atan2(direction.z, direction.x) * (0.5 / PI) + 0.5;
                sphericalUV.y = asin(clamp(direction.y, -1.0, 1.0)) / PI + 0.5;

                float3 stars = StarLayer(sphericalUV, _StarDensity, _StarSize, 13.0);
                stars += StarLayer(sphericalUV, _StarDensity * 0.47, _StarSize * 0.72, 91.0) * 0.65;

                float3 orbitalPlane = normalize(float3(0.24, 0.79, -0.56));
                float distanceToPlane = abs(dot(direction, orbitalPlane));
                float orbitalBand = smoothstep(0.24, 0.015, distanceToPlane);
                float dustVariation = 0.52 + 0.48 * sin(dot(direction, float3(39.0, 71.0, 27.0)) + sin(direction.x * 23.0));
                float3 orbitalDust = _NebulaColor.rgb * orbitalBand * dustVariation * _NebulaIntensity;

                float starsAboveHorizon = smoothstep(-0.18, 0.16, direction.y);
                float3 finalColor = skyColor + orbitalDust + stars * _StarIntensity * starsAboveHorizon;
                return half4(finalColor * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
