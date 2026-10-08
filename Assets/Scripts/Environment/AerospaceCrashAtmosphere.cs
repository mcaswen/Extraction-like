using System;
using System.Linq;
using UnityEngine;

namespace ExtractionLike.Environment
{
    /// <summary>
    /// Animates visual-only wreck atmosphere, distant aerospace landmarks and reversible wreck grading.
    /// It never owns colliders, navigation, gameplay targets or shared-material state.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(5150)]
    public sealed class AerospaceCrashAtmosphere : MonoBehaviour
    {
        [Serializable]
        public struct WreckMaterialBinding
        {
            public Renderer renderer;
            public int materialIndex;
            public Color dayColor;
            public Color nightColor;
            public Color dayEmission;
            public Color nightEmission;
        }

        public AerospaceDayNightCycle cycle;
        [SerializeField] private Transform scanPivot;
        [SerializeField] private Renderer scanBeam;
        [SerializeField] private Renderer[] beaconHeads = Array.Empty<Renderer>();
        [SerializeField] private Light[] beaconLights = Array.Empty<Light>();
        [SerializeField] private ParticleSystem[] steamSystems = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem sparkSystem;
        [SerializeField] private Transform[] vaporPuffs = Array.Empty<Transform>();
        [SerializeField] private Renderer[] vaporRenderers = Array.Empty<Renderer>();
        [SerializeField] private Vector3[] vaporBasePositions = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] vaporBaseScales = Array.Empty<Vector3>();
        [SerializeField] private Transform[] radarPivots = Array.Empty<Transform>();
        [SerializeField] private Vector3[] radarBaseEuler = Array.Empty<Vector3>();
        [SerializeField] private Renderer[] skylineBeaconHeads = Array.Empty<Renderer>();
        [SerializeField] private Light fractureRimLight;
        [SerializeField] private WreckMaterialBinding[] wreckMaterialBindings = Array.Empty<WreckMaterialBinding>();
        [SerializeField] private Vector3 scanBaseEuler;
        [SerializeField, Range(0f, 45f)] private float scanArc = 22f;
        [SerializeField, Min(.05f)] private float scanCyclesPerSecond = .11f;
        [SerializeField, Min(1f)] private float radarDegreesPerSecond = 7f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;

        public Transform ScanPivot => scanPivot;
        public Renderer ScanBeam => scanBeam;
        public Renderer[] BeaconHeads => (Renderer[])beaconHeads.Clone();
        public Light[] BeaconLights => (Light[])beaconLights.Clone();
        public ParticleSystem[] SteamSystems => (ParticleSystem[])steamSystems.Clone();
        public ParticleSystem SparkSystem => sparkSystem;
        public Transform[] VaporPuffs => (Transform[])vaporPuffs.Clone();
        public Renderer[] VaporRenderers => (Renderer[])vaporRenderers.Clone();
        public Transform[] RadarPivots => (Transform[])radarPivots.Clone();
        public Renderer[] SkylineBeaconHeads => (Renderer[])skylineBeaconHeads.Clone();
        public Light FractureRimLight => fractureRimLight;
        public WreckMaterialBinding[] WreckMaterialBindings => (WreckMaterialBinding[])wreckMaterialBindings.Clone();

        public void Configure(AerospaceDayNightCycle sourceCycle, Transform pivot, Renderer beam, Renderer[] heads,
            Light[] lights, ParticleSystem[] steam, ParticleSystem sparks, Transform[] puffs,
            Transform[] dishes, Renderer[] skylineHeads, Light fractureLight)
        {
            cycle = sourceCycle;
            scanPivot = pivot;
            scanBeam = beam;
            beaconHeads = heads ?? Array.Empty<Renderer>();
            beaconLights = lights ?? Array.Empty<Light>();
            steamSystems = steam ?? Array.Empty<ParticleSystem>();
            sparkSystem = sparks;
            vaporPuffs = puffs ?? Array.Empty<Transform>();
            vaporRenderers = vaporPuffs.Select(transform => transform != null ? transform.GetComponent<Renderer>() : null).ToArray();
            vaporBasePositions = vaporPuffs.Select(transform => transform != null ? transform.localPosition : Vector3.zero).ToArray();
            vaporBaseScales = vaporPuffs.Select(transform => transform != null ? transform.localScale : Vector3.one).ToArray();
            radarPivots = dishes ?? Array.Empty<Transform>();
            radarBaseEuler = radarPivots.Select(transform => transform != null ? transform.localEulerAngles : Vector3.zero).ToArray();
            skylineBeaconHeads = skylineHeads ?? Array.Empty<Renderer>();
            fractureRimLight = fractureLight;
            if (scanPivot != null) scanBaseEuler = scanPivot.localEulerAngles;
            ApplyPreview(cycle != null ? cycle.startPhase : 0f, 0f);
        }

        public void BindScene(AerospaceDayNightCycle sourceCycle, WreckMaterialBinding[] bindings)
        {
            cycle = sourceCycle;
            wreckMaterialBindings = bindings ?? Array.Empty<WreckMaterialBinding>();
            ApplyPreview(cycle != null ? cycle.startPhase : 0f, 0f);
        }

        private void OnEnable()
        {
            ApplyPreview(cycle != null ? (Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase) : 0f, 0f);
            if (!Application.isPlaying) return;
            foreach (ParticleSystem system in steamSystems)
                if (system != null) system.Play(true);
            if (sparkSystem != null) sparkSystem.Play(true);
        }

        private void LateUpdate()
        {
            float phase = cycle != null ? (Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase) : 0f;
            ApplyPreview(phase, Application.isPlaying ? Time.unscaledTime : 0f);
        }

        private void OnDisable()
        {
            if (scanBeam != null) scanBeam.SetPropertyBlock(null);
            foreach (Renderer renderer in vaporRenderers)
                if (renderer != null) renderer.SetPropertyBlock(null);
            foreach (Renderer head in beaconHeads.Concat(skylineBeaconHeads))
                if (head != null) head.SetPropertyBlock(null);
            foreach (Light light in beaconLights)
                if (light != null) light.intensity = 0f;
            if (fractureRimLight != null) fractureRimLight.intensity = 0f;
            foreach (WreckMaterialBinding binding in wreckMaterialBindings)
                if (binding.renderer != null && binding.materialIndex >= 0 && binding.materialIndex < binding.renderer.sharedMaterials.Length)
                    binding.renderer.SetPropertyBlock(null, binding.materialIndex);
        }

        public void ApplyPreview(float phase, float seconds)
        {
            float elevation = Mathf.Cos(Mathf.Repeat(phase, 1f) * Mathf.PI * 2f);
            float dayWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.35f, .35f, elevation));
            float nightWeight = 1f - dayWeight;
            float sweep = Mathf.Sin(seconds * scanCyclesPerSecond * Mathf.PI * 2f) * scanArc;
            if (scanPivot != null) scanPivot.localRotation = Quaternion.Euler(scanBaseEuler + Vector3.up * sweep);

            block ??= new MaterialPropertyBlock();
            if (scanBeam != null)
            {
                float scanPulse = .65f + .35f * Mathf.Sin(seconds * 1.35f);
                Color baseTint = Color.Lerp(new Color(.015f, .18f, .2f, .035f), new Color(.025f, .64f, .82f, .12f), nightWeight) * scanPulse;
                Color emission = Color.Lerp(new Color(.004f, .025f, .03f), new Color(.1f, 2.2f, 3.2f), nightWeight) * scanPulse;
                block.SetColor(BaseColor, baseTint);
                block.SetColor(EmissionColor, emission);
                scanBeam.SetPropertyBlock(block);
                block.Clear();
            }

            for (int i = 0; i < beaconHeads.Length; i++)
            {
                float wave = Mathf.Sin(seconds * 2.25f + i * 2.1f);
                float pulse = .16f + .84f * Mathf.Pow(Mathf.Max(0f, wave), 5f);
                float strength = Mathf.Lerp(.3f, 6.5f, nightWeight) * pulse;
                ApplyBeacon(beaconHeads[i], pulse, strength, nightWeight);
                if (i < beaconLights.Length && beaconLights[i] != null)
                    beaconLights[i].intensity = Mathf.Lerp(.1f, 9f, nightWeight) * pulse;
            }

            for (int i = 0; i < vaporPuffs.Length; i++)
            {
                Transform puff = vaporPuffs[i];
                if (puff == null || i >= vaporBasePositions.Length || i >= vaporBaseScales.Length) continue;
                float life = Mathf.Repeat(seconds * (.045f + i * .004f) + i * .23f, 1f);
                float lateral = Mathf.Sin(seconds * (.33f + i * .025f) + i * 1.7f);
                puff.localPosition = vaporBasePositions[i] + new Vector3(lateral * .42f, life * 2.35f, Mathf.Cos(seconds * .27f + i) * .25f);
                puff.localScale = Vector3.Scale(vaporBaseScales[i], new Vector3(.88f + life * .35f, .82f + life * .5f, .88f + life * .35f));
                if (i < vaporRenderers.Length && vaporRenderers[i] != null)
                {
                    float fade = Mathf.Sin(life * Mathf.PI);
                    Color tint = Color.Lerp(new Color(.18f, .24f, .27f, .65f), new Color(.42f, .55f, .62f, .62f), nightWeight);
                    tint.a *= Mathf.Lerp(.42f, 1f, fade);
                    block.SetColor(BaseColor, tint);
                    block.SetColor(EmissionColor, Color.Lerp(new Color(.012f, .018f, .02f), new Color(.05f, .15f, .19f), nightWeight) * fade);
                    vaporRenderers[i].SetPropertyBlock(block);
                    block.Clear();
                }
            }

            for (int i = 0; i < radarPivots.Length; i++)
                if (radarPivots[i] != null && i < radarBaseEuler.Length)
                    radarPivots[i].localRotation = Quaternion.Euler(radarBaseEuler[i] + Vector3.up * (seconds * radarDegreesPerSecond * (i % 2 == 0 ? 1f : -.78f)));

            for (int i = 0; i < skylineBeaconHeads.Length; i++)
            {
                float wave = Mathf.Sin(seconds * 1.45f + i * 2.75f);
                float pulse = .08f + .92f * Mathf.Pow(Mathf.Max(0f, wave), 8f);
                ApplyBeacon(skylineBeaconHeads[i], pulse, Mathf.Lerp(.2f, 8f, nightWeight) * pulse, nightWeight);
            }

            if (fractureRimLight != null)
                fractureRimLight.intensity = Mathf.Lerp(.18f, 6.2f, nightWeight) * (.88f + .12f * Mathf.Sin(seconds * .72f));

            foreach (WreckMaterialBinding binding in wreckMaterialBindings)
            {
                if (binding.renderer == null || binding.materialIndex < 0 || binding.materialIndex >= binding.renderer.sharedMaterials.Length) continue;
                block.SetColor(BaseColor, Color.Lerp(binding.dayColor, binding.nightColor, nightWeight));
                block.SetColor(EmissionColor, Color.Lerp(binding.dayEmission, binding.nightEmission, nightWeight));
                binding.renderer.SetPropertyBlock(block, binding.materialIndex);
                block.Clear();
            }
        }

        private void ApplyBeacon(Renderer renderer, float pulse, float strength, float nightWeight)
        {
            if (renderer == null) return;
            block.SetColor(BaseColor, Color.Lerp(new Color(.38f, .065f, .008f), new Color(1f, .2f, .012f), nightWeight * pulse));
            block.SetColor(EmissionColor, new Color(1f, .085f, .003f) * strength);
            renderer.SetPropertyBlock(block);
            block.Clear();
        }
    }
}
