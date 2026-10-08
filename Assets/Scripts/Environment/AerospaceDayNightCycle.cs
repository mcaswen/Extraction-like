using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionLike.Environment
{
    /// <summary>Artist-friendly 160-second sky crossfade with readable nighttime lighting.</summary>
    [DisallowMultipleComponent]
    public sealed class AerospaceDayNightCycle : MonoBehaviour
    {
        [Header("Skybox Versions")]
        public Material skyboxVersion1;
        public Material skyboxVersion2;
        public Light sunLight;
        [Tooltip("Optional reversible scene art direction. Null keeps the original V2 lighting behavior.")]
        public AerospaceLightingVegetationProfile artDirection;

        [Header("Cycle")]
        [Min(10f), Tooltip("Seconds for a complete day -> night -> day loop.")]
        public float cycleSeconds = 160f;
        [Range(0f, 1f), Tooltip("0 = noon, 0.25 = sunset, 0.5 = midnight, 0.75 = sunrise.")]
        public float startPhase;
        public bool animate = true;
        [Tooltip("Use real seconds, independent of the game's time scale.")]
        public bool useUnscaledTime = true;

        [Header("Sky Appearance")]
        [Min(0f)] public float dayExposure = 0.55f;
        [Min(0f)] public float nightExposure = 0.08f;
        [Range(0f, 360f)] public float panoramaRotation = 130f;
        public Color nightTint = new Color(0.5f, 0.72f, 1f);
        public Color nightVisibilityFloor = new Color(0.006f, 0.014f, 0.04f);
        public Color nightHorizonGlow = new Color(0.02f, 0.035f, 0.07f);

        [Header("Lighting (Night Remains Playable)")]
        [Min(0f)] public float dayLightIntensity = 1.1f;
        [Min(0f)] public float nightLightIntensity = 0.6f;
        [Min(0f)] public float nightAmbientMultiplier = 1.25f;
        [Range(0f, 360f)] public float lightYaw;
        [Tooltip("Expensive environment/reflection refresh. Off by default: trilight ambient updates without it.")]
        public bool refreshEnvironmentReflections;
        [Min(1f)] public float reflectionRefreshSeconds = 5f;

        [Header("Secondary Directional Fill")]
        [Tooltip("Other global directional lights in the scene, not local roadside lamps.")]
        public Light[] secondaryDirectionalLights = new Light[0];
        [Range(0f, 1f)] public float secondaryDayMultiplier = 0.3f;
        [Range(0f, 1f)] public float secondaryNightMultiplier = 0.05f;

        [SerializeField, HideInInspector] private AerospaceLightingState version1Lighting;
        [SerializeField, HideInInspector] private AerospaceFillLightState[] version1FillLights = new AerospaceFillLightState[0];
        private AerospaceLightingState runtimeLighting;
        private AerospaceFillLightState[] runtimeFillLights;
        private Material runtimeMaterial;
        private float currentPhase;
        private float reflectionTimer;

        public float CurrentPhase => currentPhase;
        public AerospaceLightingState Version1Lighting => version1Lighting;
        public AerospaceFillLightState[] Version1FillLights => version1FillLights;

        public void StoreVersion1Lighting()
        {
            // Only an explicit editor setup calls this, never a Play-mode callback.
            version1Lighting = AerospaceLightingState.Capture(sunLight);
        }

        public void RegisterSecondaryLights(Light[] lights)
        {
            secondaryDirectionalLights = lights;
            var saved = new List<AerospaceFillLightState>(version1FillLights ?? new AerospaceFillLightState[0]);
            foreach (Light light in lights)
                if (light != null && !saved.Exists(s => s.light == light)) saved.Add(AerospaceFillLightState.Capture(light));
            version1FillLights = saved.ToArray();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || skyboxVersion2 == null) return;
            runtimeLighting = AerospaceLightingState.Capture(sunLight);
            var fills = new List<AerospaceFillLightState>();
            foreach (Light light in secondaryDirectionalLights)
                if (light != null) fills.Add(AerospaceFillLightState.Capture(light));
            runtimeFillLights = fills.ToArray();
            runtimeMaterial = new Material(skyboxVersion2) { name = skyboxVersion2.name + " (Runtime)", hideFlags = HideFlags.DontSave };
            currentPhase = Mathf.Repeat(startPhase, 1f);
            reflectionTimer = 0f;
            ApplyPhase(currentPhase, runtimeMaterial);
        }

        private void Update()
        {
            if (runtimeMaterial == null) return;
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (animate) currentPhase = PhaseAfter(currentPhase, delta, cycleSeconds);
            ApplyPhase(currentPhase, runtimeMaterial);
            if (refreshEnvironmentReflections)
            {
                reflectionTimer += delta;
                if (reflectionTimer >= Mathf.Max(1f, reflectionRefreshSeconds))
                {
                    reflectionTimer = 0f;
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        public static float PhaseAfter(float phase, float elapsedSeconds, float duration)
        {
            return Mathf.Repeat(phase + elapsedSeconds / Mathf.Max(10f, duration), 1f);
        }

        /// <summary>In edit mode this only runs when an explicit Preview button is pressed.</summary>
        public void ApplyPhase(float phase, Material material = null)
        {
            material = material != null ? material : runtimeMaterial != null ? runtimeMaterial : skyboxVersion2;
            if (material == null) return;
            currentPhase = Mathf.Repeat(phase, 1f);
            float elevation = Mathf.Cos(currentPhase * Mathf.PI * 2f);
            float dayWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.35f, elevation));
            float twilight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.45f, Mathf.Abs(elevation)));
            material.SetFloat("_Blend", 1f - dayWeight);
            material.SetFloat("_DayExposure", dayExposure);
            material.SetFloat("_NightExposure", nightExposure);
            material.SetFloat("_Rotation", panoramaRotation);
            material.SetColor("_DayTint", Color.Lerp(Color.white, new Color(1f, 0.53f, 0.32f), twilight));
            material.SetColor("_NightFloor", nightVisibilityFloor);
            material.SetColor("_NightTint", nightTint);
            material.SetColor("_HorizonColor", nightHorizonGlow);
            RenderSettings.skybox = material;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // These intentionally stay brighter than physically accurate moonlight.
            float nightBoost = Mathf.Max(0f, nightAmbientMultiplier);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.20f, 0.28f, 0.43f) * nightBoost, new Color(0.42f, 0.51f, 0.64f), dayWeight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.13f, 0.18f, 0.28f) * nightBoost, new Color(0.30f, 0.36f, 0.44f), dayWeight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.09f, 0.12f, 0.18f) * nightBoost, new Color(0.20f, 0.24f, 0.28f), dayWeight);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.65f, 1f, dayWeight);
            if (sunLight != null)
            {
                RenderSettings.sun = sunLight;
                sunLight.color = Color.Lerp(new Color(0.60f, 0.73f, 1f), Color.Lerp(new Color(1f, 0.95f, 0.84f), new Color(1f, 0.58f, 0.32f), twilight), dayWeight);
                sunLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, dayWeight);
                // The same directional light becomes a soft, elevated moon fill at night.
                float pitch = Mathf.Max(8f, Mathf.Abs(elevation) * 65f);
                sunLight.transform.rotation = Quaternion.Euler(pitch, lightYaw + 180f * (1f - dayWeight), 0f);
            }
            foreach (AerospaceFillLightState fill in version1FillLights)
            {
                if (fill.light == null || fill.light == sunLight || Array.IndexOf(secondaryDirectionalLights, fill.light) < 0) continue;
                fill.light.intensity = fill.intensity * Mathf.Lerp(secondaryNightMultiplier, secondaryDayMultiplier, dayWeight);
                fill.light.color = Color.Lerp(new Color(0.6f, 0.73f, 1f), fill.color, dayWeight);
            }
            if (artDirection != null && artDirection.isActiveAndEnabled) artDirection.ApplyPhase(currentPhase);
        }

        public void RestoreVersion1()
        {
            StopAndRestoreRuntime();
            if (version1Lighting != null) version1Lighting.Restore(sunLight);
            foreach (AerospaceFillLightState fill in version1FillLights) fill.Restore();
            if (skyboxVersion1 != null) RenderSettings.skybox = skyboxVersion1;
        }

        private void OnDisable() { StopAndRestoreRuntime(); }
        private void OnDestroy() { StopAndRestoreRuntime(); }

        private void StopAndRestoreRuntime()
        {
            if (runtimeMaterial == null) return;
            if (runtimeLighting != null) runtimeLighting.Restore(sunLight);
            if (runtimeFillLights != null) foreach (AerospaceFillLightState fill in runtimeFillLights) fill.Restore();
            if (Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
            runtimeLighting = null;
            runtimeFillLights = null;
        }
    }

    [Serializable]
    public sealed class AerospaceFillLightState
    {
        public Light light;
        public Color color;
        public float intensity;
        public bool enabled;
        public static AerospaceFillLightState Capture(Light light)
        {
            return new AerospaceFillLightState { light = light, color = light.color, intensity = light.intensity, enabled = light.enabled };
        }
        public void Restore()
        {
            if (light == null) return;
            light.color = color; light.intensity = intensity; light.enabled = enabled;
        }
    }

    [Serializable]
    public sealed class AerospaceLightingState
    {
        public Material skybox;
        public AmbientMode ambientMode;
        public Color skyColor, equatorColor, groundColor;
        public float ambientIntensity, reflectionIntensity;
        public Light environmentSun;
        public Quaternion lightRotation;
        public Color lightColor;
        public float lightIntensity;

        public static AerospaceLightingState Capture(Light light)
        {
            return new AerospaceLightingState
            {
                skybox = RenderSettings.skybox, ambientMode = RenderSettings.ambientMode,
                skyColor = RenderSettings.ambientSkyColor, equatorColor = RenderSettings.ambientEquatorColor,
                groundColor = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity, environmentSun = RenderSettings.sun,
                lightRotation = light != null ? light.transform.rotation : Quaternion.identity,
                lightColor = light != null ? light.color : Color.white, lightIntensity = light != null ? light.intensity : 1f
            };
        }

        public void Restore(Light light)
        {
            RenderSettings.skybox = skybox; RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = skyColor; RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor; RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.sun = environmentSun;
            if (light == null) return;
            light.transform.rotation = lightRotation; light.color = lightColor; light.intensity = lightIntensity;
        }
    }
}
