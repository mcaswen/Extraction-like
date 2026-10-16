using System;
using UnityEngine;

namespace ExtractionLike.Environment
{
    /// <summary>Visual-only emergency landing. Does not write terrain, navigation, saves or gameplay state.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(5200)]
    public sealed class AerospaceLandingSite : MonoBehaviour
    {
        [Serializable] public struct PreviousVisual { public GameObject target; public bool active; }
        public AerospaceDayNightCycle cycle;
        public Transform vehicle;
        public LODGroup vehicleLods;
        public ParticleSystem[] smoke = Array.Empty<ParticleSystem>();
        public Renderer[] smokeWisps = Array.Empty<Renderer>();
        public Light[] inspectionLights = Array.Empty<Light>();
        public PreviousVisual[] previousVisuals = Array.Empty<PreviousVisual>();
        public Vector3[] groundContactPoints = Array.Empty<Vector3>();
        public string sourceMasterSha256;
        MaterialPropertyBlock smokeBlock;

        void OnEnable() => ApplyPhase(cycle != null ? cycle.startPhase : 0);
        void LateUpdate()
        {
            if (cycle != null) ApplyPhase(Application.isPlaying ? cycle.CurrentPhase : cycle.startPhase);
        }
        void OnDisable()
        {
            foreach (var light in inspectionLights) if (light != null) light.intensity = 0;
        }
        public void ApplyPhase(float phase)
        {
            float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.35f, .35f, Mathf.Cos(phase * Mathf.PI * 2)));
            for (int i = 0; i < inspectionLights.Length; i++)
                if (inspectionLights[i] != null) inspectionLights[i].intensity = Mathf.Lerp(i == 0 ? 18f : 10f, 0f, daylight);
            foreach (var system in smoke)
            {
                if (system == null) continue;
                var main = system.main;
                Color tint = Color.Lerp(new Color(.53f,.55f,.56f,.48f), new Color(.36f,.37f,.37f,.60f), daylight);
                main.startColor = new ParticleSystem.MinMaxGradient(tint, new Color(tint.r+.12f,tint.g+.12f,tint.b+.12f,tint.a*.75f));
            }
            smokeBlock ??= new MaterialPropertyBlock();
            smokeBlock.SetColor("_BaseColor", Color.Lerp(new Color(.57f,.60f,.61f,.48f), new Color(.26f,.27f,.28f,.62f), daylight));
            foreach (var wisp in smokeWisps) if (wisp != null) wisp.SetPropertyBlock(smokeBlock);
        }
    }
}
