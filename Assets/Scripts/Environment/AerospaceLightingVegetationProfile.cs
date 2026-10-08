using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ExtractionLike.Environment
{
    /// <summary>Reversible scene-local materials and art lighting; has no gameplay/perception responsibilities.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(5000)]
    public sealed class AerospaceLightingVegetationProfile : MonoBehaviour
    {
        [Serializable]
        public struct RendererBinding
        {
            public Renderer renderer;
            public Material[] original;
            public Material[] styled;
        }

        [Serializable]
        public struct TerrainBinding
        {
            public Terrain terrain;
            public TerrainCollider collider;
            public TerrainData original;
            public TerrainData styled;
        }

        [Serializable]
        public struct CameraBinding
        {
            public UniversalAdditionalCameraData camera;
            public bool originalPostProcessing;
        }

        [Serializable]
        public struct LocalLightBinding
        {
            public Light light;
            public Color originalColor;
            public float originalIntensity;
            public Color surveyColor;
            public float dayIntensity;
            public float nightIntensity;
        }

        [Serializable]
        private struct LightState
        {
            public Light light;
            public Color color;
            public float intensity;
            public LightShadows shadows;
            public float shadowStrength;
            public static LightState Capture(Light value) => new LightState
            {
                light = value, color = value.color, intensity = value.intensity,
                shadows = value.shadows, shadowStrength = value.shadowStrength
            };
            public void Restore()
            {
                if (light == null) return;
                light.color = color; light.intensity = intensity;
                light.shadows = shadows; light.shadowStrength = shadowStrength;
            }
        }

        [Serializable]
        private struct EnvironmentState
        {
            public bool fog;
            public FogMode fogMode;
            public Color fogColor;
            public float fogStart, fogEnd, fogDensity;
            public AmbientMode ambientMode;
            public Color sky, equator, ground;
            public float ambientIntensity, reflectionIntensity;
            public static EnvironmentState Capture() => new EnvironmentState
            {
                fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor,
                fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance, fogDensity = RenderSettings.fogDensity,
                ambientMode = RenderSettings.ambientMode, sky = RenderSettings.ambientSkyColor,
                equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
                ambientIntensity = RenderSettings.ambientIntensity, reflectionIntensity = RenderSettings.reflectionIntensity
            };
            public void Restore()
            {
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd; RenderSettings.fogDensity = fogDensity;
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.reflectionIntensity = reflectionIntensity;
            }
        }

        [Header("Existing skybox / cycle (textures unchanged; 160-second timing)")]
        public AerospaceDayNightCycle cycle;
        public Volume gradingVolume;

        [Header("Sun / readable moon fill")]
        [Min(0)] public float daySunIntensity = 1.12f;
        [Min(0)] public float nightSunIntensity = .72f;
        public Color daySunColor = new Color(1f, .96f, .90f);
        public Color nightSunColor = new Color(.70f, .80f, 1f);
        public Color sunsetSunColor = new Color(1f, .74f, .54f);
        [Range(0, 1)] public float sunShadowStrength = .58f;
        [Min(0)] public float daySecondaryFillIntensity = .16f;
        [Min(0)] public float nightSecondaryFillIntensity = .035f;

        [Header("Ambient shape (not photorealistic darkness)")]
        public Color daySkyAmbient = new Color(.50f, .57f, .66f);
        public Color dayEquatorAmbient = new Color(.40f, .46f, .52f);
        public Color dayGroundAmbient = new Color(.29f, .33f, .37f);
        public Color nightSkyAmbient = new Color(.35f, .45f, .61f);
        public Color nightEquatorAmbient = new Color(.27f, .35f, .46f);
        public Color nightGroundAmbient = new Color(.20f, .25f, .33f);

        [Header("Subtle far-distance haze (never a perception obstacle)")]
        public bool useDistanceHaze = true;
        [Min(50)] public float hazeStart = 200f;
        [Min(100)] public float hazeEnd = 850f;
        public Color dayHazeColor = new Color(.65f, .73f, .78f);
        public Color nightHazeColor = new Color(.15f, .22f, .33f);
        [Range(0, 1), Tooltip("Used only with skybox/cycle disabled. 0=noon, .5=night.")]
        public float staticPreviewPhase;

        [Header("Foliage line noise (keeps depth silhouettes / existing Toon style)")]
        [Range(0, 1)] public float sobelIntensityScale = .62f;
        [Range(.25f, 2)] public float sobelNormalThreshold = .65f;
        private static readonly Dictionary<int, AerospaceLightingVegetationProfile> activeProfiles = new Dictionary<int, AerospaceLightingVegetationProfile>();
        private static readonly Dictionary<int, AerospaceLightingVegetationProfile> previewProfiles = new Dictionary<int, AerospaceLightingVegetationProfile>();
        // HideAndDontSave preview cameras have no owning scene. Associate them explicitly;
        // do not fall back to the active scene for arbitrary cameras/other scenes.
        public static void AssociatePreviewCamera(Camera camera, AerospaceLightingVegetationProfile profile)
        {
            if (camera == null) return;
            if (profile == null) previewProfiles.Remove(camera.GetInstanceID());
            else previewProfiles[camera.GetInstanceID()] = profile;
        }
        public static AerospaceLightingVegetationProfile ForCamera(Camera camera)
        {
            if (camera == null) return null;
            if (previewProfiles.TryGetValue(camera.GetInstanceID(), out var preview))
                return preview != null && preview.isActiveAndEnabled ? preview : null;
            if (!camera.gameObject.scene.IsValid()) return null;
            return activeProfiles.TryGetValue(camera.gameObject.scene.handle, out var profile) && profile != null && profile.isActiveAndEnabled ? profile : null;
        }

        [SerializeField] private bool configured;
        [SerializeField] private RendererBinding[] rendererBindings = new RendererBinding[0];
        [SerializeField] private TerrainBinding[] terrainBindings = new TerrainBinding[0];
        [SerializeField] private CameraBinding[] cameraBindings = new CameraBinding[0];
        [SerializeField] private LocalLightBinding[] localLightBindings = new LocalLightBinding[0];
        [SerializeField] private LightState sunBaseline;
        [SerializeField] private LightState[] secondaryBaseline = new LightState[0];
        [SerializeField] private EnvironmentState environmentBaseline;
        private bool restoring;
        private VolumeProfile ownedGradingProfile;

        public RendererBinding[] Renderers => (RendererBinding[])rendererBindings.Clone();
        public TerrainBinding[] Terrains => (TerrainBinding[])terrainBindings.Clone();
        public CameraBinding[] Cameras => (CameraBinding[])cameraBindings.Clone();
        public LocalLightBinding[] LocalLights => (LocalLightBinding[])localLightBindings.Clone();
        public bool IsConfigured => configured;

        public void Configure(AerospaceDayNightCycle skyCycle, Volume volume, RendererBinding[] renderers,
            TerrainBinding[] terrains, CameraBinding[] cameras, LocalLightBinding[] localLights)
        {
            if (configured) throw new InvalidOperationException("Art profile already configured; manual edits are not overwritten.");
            cycle = skyCycle; gradingVolume = volume;
            rendererBindings = renderers; terrainBindings = terrains;
            cameraBindings = cameras; localLightBindings = localLights;
            environmentBaseline = EnvironmentState.Capture();
            if (cycle != null && cycle.sunLight != null) sunBaseline = LightState.Capture(cycle.sunLight);
            if (cycle != null)
            {
                var lights = Array.FindAll(cycle.secondaryDirectionalLights, light => light != null);
                secondaryBaseline = Array.ConvertAll(lights, LightState.Capture);
            }
            configured = true;
            if (isActiveAndEnabled) ApplyBindings();
        }

        public void ApplyBindings()
        {
            if (!configured) return;
            activeProfiles[gameObject.scene.handle] = this;
            if (gradingVolume != null) gradingVolume.enabled = true;
            foreach (RendererBinding binding in rendererBindings)
                if (binding.renderer != null && MaterialsEqual(binding.renderer.sharedMaterials, binding.original))
                { binding.renderer.sharedMaterials = binding.styled; RecordEditorOverride(binding.renderer); }
            foreach (TerrainBinding binding in terrainBindings)
            {
                if (binding.terrain != null && binding.terrain.terrainData == binding.original)
                { binding.terrain.terrainData = binding.styled; RecordEditorOverride(binding.terrain); }
                if (binding.collider != null && binding.collider.terrainData == binding.original)
                { binding.collider.terrainData = binding.styled; RecordEditorOverride(binding.collider); }
            }
            foreach (CameraBinding binding in cameraBindings)
                if (binding.camera != null) { binding.camera.renderPostProcessing = true; RecordEditorOverride(binding.camera); }
            ApplyPhase(cycle != null && cycle.isActiveAndEnabled ? cycle.CurrentPhase : staticPreviewPhase);
        }

        public void ApplyPhase(float phase)
        {
            if (!configured || !isActiveAndEnabled || restoring) return;
            float elevation = Mathf.Cos(Mathf.Repeat(phase, 1) * Mathf.PI * 2);
            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f, .35f, elevation));
            float dusk = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .45f, Mathf.Abs(elevation)));
            Light sun = sunBaseline.light;
            if (sun != null)
            {
                sun.color = Color.Lerp(nightSunColor, Color.Lerp(daySunColor, sunsetSunColor, dusk * .65f), day);
                sun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, day);
                sun.shadows = LightShadows.Soft; sun.shadowStrength = sunShadowStrength;
            }
            foreach (LightState state in secondaryBaseline)
                if (state.light != null)
                {
                    state.light.color = Color.Lerp(new Color(.66f, .78f, 1f), new Color(.82f, .89f, .96f), day);
                    state.light.intensity = Mathf.Lerp(nightSecondaryFillIntensity, daySecondaryFillIntensity, day);
                    state.light.shadows = LightShadows.None;
                }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(nightSkyAmbient, daySkyAmbient, day);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightEquatorAmbient, dayEquatorAmbient, day);
            RenderSettings.ambientGroundColor = Color.Lerp(nightGroundAmbient, dayGroundAmbient, day);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = Mathf.Lerp(.65f, .85f, day);
            RenderSettings.fog = useDistanceHaze; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(nightHazeColor, dayHazeColor, day);
            RenderSettings.fogStartDistance = hazeStart;
            RenderSettings.fogEndDistance = Mathf.Max(hazeStart + 100, hazeEnd);
            foreach (LocalLightBinding binding in localLightBindings)
                if (binding.light != null)
                { binding.light.color = binding.surveyColor; binding.light.intensity = Mathf.Lerp(binding.nightIntensity, binding.dayIntensity, day); }
            if (gradingVolume != null && gradingVolume.sharedProfile != null)
            {
                // Do not dirty/edit the profile asset during animation: Volume.profile is a runtime copy.
                ownedGradingProfile = gradingVolume.profile;
                if (ownedGradingProfile.TryGet(out ColorAdjustments adjustment))
                    adjustment.postExposure.Override(Mathf.Lerp(.18f, .05f, day));
            }
        }

        public void RestoreBindings()
        {
            if (!configured) return;
            if (activeProfiles.TryGetValue(gameObject.scene.handle, out var registered) && registered == this)
                activeProfiles.Remove(gameObject.scene.handle);
            restoring = true;
            try
            {
                if (gradingVolume != null) gradingVolume.enabled = false;
                foreach (RendererBinding binding in rendererBindings)
                    if (binding.renderer != null && MaterialsEqual(binding.renderer.sharedMaterials, binding.styled))
                    { binding.renderer.sharedMaterials = binding.original; RecordEditorOverride(binding.renderer); }
                foreach (TerrainBinding binding in terrainBindings)
                {
                    if (binding.terrain != null && binding.terrain.terrainData == binding.styled)
                    { binding.terrain.terrainData = binding.original; RecordEditorOverride(binding.terrain); }
                    if (binding.collider != null && binding.collider.terrainData == binding.styled)
                    { binding.collider.terrainData = binding.original; RecordEditorOverride(binding.collider); }
                }
                foreach (CameraBinding binding in cameraBindings)
                    if (binding.camera != null)
                    { binding.camera.renderPostProcessing = binding.originalPostProcessing; RecordEditorOverride(binding.camera); }
                foreach (LocalLightBinding binding in localLightBindings)
                    if (binding.light != null)
                    { binding.light.color = binding.originalColor; binding.light.intensity = binding.originalIntensity; }
                sunBaseline.Restore();
                foreach (LightState state in secondaryBaseline) state.Restore();
                environmentBaseline.Restore();
                // The sky/cycle remains at its current phase; disabling this profile does not reset time.
                if (cycle != null && cycle.isActiveAndEnabled && cycle.gameObject.scene.isLoaded)
                    cycle.ApplyPhase(cycle.CurrentPhase);
            }
            finally { restoring = false; }
        }

        public static bool MaterialsEqual(Material[] a, Material[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void RecordEditorOverride(Object value)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(value);
                UnityEditor.EditorUtility.SetDirty(value);
            }
#endif
        }

        private void OnEnable() => ApplyBindings();
        private void LateUpdate()
        {
            if (Application.isPlaying) ApplyPhase(cycle != null && cycle.isActiveAndEnabled ? cycle.CurrentPhase : staticPreviewPhase);
        }
        private void OnDisable() => RestoreBindings();
        private void OnDestroy()
        {
            RestoreBindings();
            if (ownedGradingProfile == null) return;
            foreach (VolumeComponent component in ownedGradingProfile.components) CoreUtils.Destroy(component);
            CoreUtils.Destroy(ownedGradingProfile); ownedGradingProfile = null;
        }
        private void OnValidate()
        {
            if (configured && isActiveAndEnabled) ApplyPhase(cycle != null && cycle.isActiveAndEnabled ? cycle.CurrentPhase : staticPreviewPhase);
        }
    }
}
