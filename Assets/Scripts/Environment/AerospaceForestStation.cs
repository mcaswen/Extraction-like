using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExtractionLike.Environment
{
    /// <summary>Reversible scenery only. Never changes terrain collision, navigation, loot or Agents.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(5200)]
    public sealed class AerospaceForestStation : MonoBehaviour
    {
        [Serializable] public sealed class TerrainDisplay
        {
            public Terrain terrain;
            public TerrainData sourceOriginal, sourceStyled, displayOriginal, displayStyled;
            public int[] removedInstanceIndices = Array.Empty<int>();
        }
        [Serializable] public sealed class RendererDisplay
        {
            public Renderer renderer;
            public bool originalEnabled;
            public Material[] originalMaterials, hiddenMaterials;
        }
        [Serializable] public sealed class Motion
        {
            public Transform target;
            public Vector3 originPosition;
            public Quaternion originRotation;
            public Vector3 axis = Vector3.up;
            public float degrees, distance, period = 18f, phase;
        }
        [Serializable] public sealed class Indicator
        {
            public Renderer renderer;
            public Color baseEmission = new Color(.06f, .5f, .7f);
            public float offset;
        }
        public TerrainDisplay[] terrainDisplays = Array.Empty<TerrainDisplay>();
        public RendererDisplay[] hiddenFoliage = Array.Empty<RendererDisplay>();
        public Motion[] motions = Array.Empty<Motion>();
        public Indicator[] indicators = Array.Empty<Indicator>();
        public Light[] workLights = Array.Empty<Light>();
        public AudioSource machineryAmbience;
        public AerospaceDayNightCycle cycle;
        [Range(0, 4)] public float nightLightIntensity = 1.65f;
        MaterialPropertyBlock block;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        float clock;

        public void ApplyDisplay()
        {
            if (!isActiveAndEnabled) return;
            foreach (var b in terrainDisplays)
            {
                if (b == null || b.terrain == null) continue;
                if (b.terrain.terrainData == b.sourceOriginal && b.displayOriginal != null) b.terrain.terrainData = b.displayOriginal;
                else if (b.terrain.terrainData == b.sourceStyled && b.displayStyled != null) b.terrain.terrainData = b.displayStyled;
            }
            foreach (var b in hiddenFoliage)
                if (b != null && b.renderer != null && b.hiddenMaterials != null)
                {
                    if (!AerospaceLightingVegetationProfile.MaterialsEqual(b.renderer.sharedMaterials,b.hiddenMaterials))
                        b.originalMaterials=b.renderer.sharedMaterials;
                    // Keep renderer and mesh enabled as identical future navigation-bake sources.
                    b.renderer.sharedMaterials=b.hiddenMaterials;
                }
        }
        public void ReleaseTerrainDisplay()
        {
            foreach (var b in terrainDisplays)
            {
                if (b == null || b.terrain == null) continue;
                if (b.terrain.terrainData == b.displayOriginal) b.terrain.terrainData = b.sourceOriginal;
                else if (b.terrain.terrainData == b.displayStyled) b.terrain.terrainData = b.sourceStyled;
            }
        }
        public static void ReleaseDisplays(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var station in root.GetComponentsInChildren<AerospaceForestStation>(true))
                { station.ReleaseTerrainDisplay(); station.ReleaseFoliageDisplay(); }
        }
        public static void RefreshDisplays(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var station in root.GetComponentsInChildren<AerospaceForestStation>(true)) station.ApplyDisplay();
        }
        public static TerrainData ResolveDisplay(Terrain terrain, TerrainData source)
        {
            if (terrain == null || !terrain.gameObject.scene.IsValid() || !terrain.gameObject.scene.isLoaded) return source;
            foreach (var root in terrain.gameObject.scene.GetRootGameObjects())
                foreach (var station in root.GetComponentsInChildren<AerospaceForestStation>(true))
                    if (station.isActiveAndEnabled)
                        foreach (var b in station.terrainDisplays)
                            if (b != null && b.terrain == terrain)
                            {
                                if (source == b.sourceStyled) return b.displayStyled;
                                if (source == b.sourceOriginal) return b.displayOriginal;
                            }
            return source;
        }
        public static Material[] ResolveMaterials(Renderer renderer, Material[] source)
        {
            if (renderer==null || !renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded) return source;
            foreach(var root in renderer.gameObject.scene.GetRootGameObjects())
                foreach(var station in root.GetComponentsInChildren<AerospaceForestStation>(true))
                    if(station.isActiveAndEnabled)foreach(var b in station.hiddenFoliage)
                        if(b.renderer==renderer)return b.hiddenMaterials;
            return source;
        }
        private void OnEnable()
        {
            clock = 0; ApplyDisplay(); ApplyVisuals(0, cycle != null ? cycle.startPhase : 0);
        }
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            // Scenery pauses with gameplay and therefore does not run behind a paused inspection screen.
            clock += Time.deltaTime;
            ApplyVisuals(clock, cycle != null ? cycle.CurrentPhase : 0);
            UpdateAmbience();
        }
        void UpdateAmbience()
        {
            if (machineryAmbience == null || machineryAmbience.clip == null) return;
            float target = 0;
            var registry = Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance;
            if (Time.timeScale > 0 && registry != null && registry.TryGetFocusedHandle(out var agent) && agent.IsAlive && agent.CachedTransform != null)
            {
                var delta = agent.CachedTransform.position - machineryAmbience.transform.position;
                delta.y = 0;
                target = .10f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(9, 38, delta.magnitude)));
            }
            machineryAmbience.volume = Mathf.MoveTowards(machineryAmbience.volume, target, Time.unscaledDeltaTime * .25f);
            if (target > 0 && !machineryAmbience.isPlaying) machineryAmbience.Play();
            if (machineryAmbience.volume <= .0001f && machineryAmbience.isPlaying) machineryAmbience.Stop();
        }
        public void ApplyVisuals(float seconds, float phase)
        {
            if (!isActiveAndEnabled) return;
            foreach (var m in motions)
            {
                if (m == null || m.target == null) continue;
                float wave = Mathf.Sin(seconds * Mathf.PI * 2 / Mathf.Max(4, m.period) + m.phase);
                m.target.localRotation = m.originRotation * Quaternion.AngleAxis(wave * m.degrees, m.axis);
                m.target.localPosition = m.originPosition + m.axis.normalized * (wave * m.distance);
            }
            block ??= new MaterialPropertyBlock();
            foreach (var i in indicators)
            {
                if (i == null || i.renderer == null) continue;
                i.renderer.GetPropertyBlock(block);
                // A restrained change in intensity, never an on/off strobe.
                float pulse = .82f + .18f * Mathf.Sin(seconds * .7f + i.offset);
                block.SetColor(Emission, i.baseEmission * pulse);
                i.renderer.SetPropertyBlock(block); block.Clear();
            }
            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f,.35f,Mathf.Cos(phase*Mathf.PI*2)));
            foreach (var light in workLights) if (light != null) light.intensity = Mathf.Lerp(nightLightIntensity,.08f,day);
        }
        private void OnDisable()
        {
            if (machineryAmbience != null) { machineryAmbience.Stop(); machineryAmbience.volume = 0; }
            ReleaseTerrainDisplay();
            ReleaseFoliageDisplay();
            foreach (var m in motions) if (m != null && m.target != null) m.target.SetLocalPositionAndRotation(m.originPosition,m.originRotation);
            foreach (var i in indicators) if (i != null && i.renderer != null) i.renderer.SetPropertyBlock(null);
            foreach (var light in workLights) if (light != null) light.intensity = 0;
        }
        private void ReleaseFoliageDisplay()
        {
            foreach (var b in hiddenFoliage)
                if (b != null && b.renderer != null && b.originalMaterials != null &&
                    AerospaceLightingVegetationProfile.MaterialsEqual(b.renderer.sharedMaterials,b.hiddenMaterials))
                    b.renderer.sharedMaterials=b.originalMaterials;
        }
    }
}
