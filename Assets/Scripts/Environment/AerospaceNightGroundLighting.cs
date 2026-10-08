using System;
using UnityEngine;

namespace ExtractionLike.Environment
{
    /// <summary>
    /// Drives visual-only ground strips and a few shadowless survey lamps from the
    /// existing day/night phase. MaterialPropertyBlocks keep shared assets immutable.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(5100)]
    public sealed class AerospaceNightGroundLighting : MonoBehaviour
    {
        [Serializable]
        public sealed class EmissionBinding
        {
            public Renderer renderer;
            [Min(0)] public int materialIndex;
            [ColorUsage(true, true)] public Color dayEmission;
            [ColorUsage(true, true)] public Color nightEmission;
        }

        [Serializable]
        public sealed class LightBinding
        {
            public Light light;
            [Min(0f)] public float dayIntensity;
            [Min(0f)] public float nightIntensity;
        }

        public AerospaceDayNightCycle cycle;
        [SerializeField] private EmissionBinding[] emissionBindings = Array.Empty<EmissionBinding>();
        [SerializeField] private LightBinding[] lightBindings = Array.Empty<LightBinding>();

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock propertyBlock;

        public EmissionBinding[] Emissions => (EmissionBinding[])emissionBindings.Clone();
        public LightBinding[] Lights => (LightBinding[])lightBindings.Clone();

        public void Configure(AerospaceDayNightCycle sourceCycle, EmissionBinding[] emissions, LightBinding[] lights)
        {
            cycle = sourceCycle;
            emissionBindings = emissions ?? Array.Empty<EmissionBinding>();
            lightBindings = lights ?? Array.Empty<LightBinding>();
            ApplyPhase(cycle != null ? (Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase) : 0f);
        }

        private void OnEnable()
            => ApplyPhase(cycle != null ? (Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase) : 0f);

        private void LateUpdate()
        {
            if (cycle != null) ApplyPhase(Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase);
        }

        private void OnDisable()
        {
            foreach (EmissionBinding binding in emissionBindings)
                if (binding != null && binding.renderer != null)
                    binding.renderer.SetPropertyBlock(null, Mathf.Max(0, binding.materialIndex));
            foreach (LightBinding binding in lightBindings)
                if (binding != null && binding.light != null) binding.light.intensity = 0f;
        }

        public void ApplyPhase(float phase)
        {
            float elevation = Mathf.Cos(Mathf.Repeat(phase, 1f) * Mathf.PI * 2f);
            float dayWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.35f, elevation));
            float nightWeight = 1f - dayWeight;
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (EmissionBinding binding in emissionBindings)
            {
                if (binding == null || binding.renderer == null) continue;
                int index = Mathf.Clamp(binding.materialIndex, 0, Mathf.Max(0, binding.renderer.sharedMaterials.Length - 1));
                binding.renderer.GetPropertyBlock(propertyBlock, index);
                propertyBlock.SetColor(EmissionColor, Color.Lerp(binding.dayEmission, binding.nightEmission, nightWeight));
                Material material = binding.renderer.sharedMaterials[index];
                Color dayBase = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.black;
                bool amber = material != null && material.name.IndexOf("Amber", StringComparison.OrdinalIgnoreCase) >= 0;
                Color nightBase = amber ? new Color(1f, .24f, .025f, 1f) : new Color(.035f, .72f, 1f, 1f);
                propertyBlock.SetColor(BaseColor, Color.Lerp(dayBase, nightBase, nightWeight));
                binding.renderer.SetPropertyBlock(propertyBlock, index);
                propertyBlock.Clear();
            }
            foreach (LightBinding binding in lightBindings)
                if (binding != null && binding.light != null)
                    binding.light.intensity = Mathf.Lerp(binding.dayIntensity, binding.nightIntensity, nightWeight);
        }
    }
}
