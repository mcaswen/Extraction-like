using System;
using UnityEngine;

namespace ExtractionLike.Environment
{
    /// <summary>A reversible, local-only lighting adjustment; never controls the sun or sky.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AerospaceResearchLightingOverride : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public Light light;
            public float originalIntensity;
            public float surveyIntensity;
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        public Entry[] Entries => (Entry[])entries.Clone();

        public void Configure(Light[] lights, float[] intensities)
        {
            if (lights.Length != intensities.Length) throw new ArgumentException("One intensity is required per light.");
            Restore();
            entries = new Entry[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                entries[i] = new Entry { light = lights[i], originalIntensity = lights[i].intensity, surveyIntensity = intensities[i] };
            if (isActiveAndEnabled) Apply();
        }

        public void Apply()
        {
            foreach (Entry entry in entries)
                if (entry.light != null) SetIntensity(entry.light, entry.surveyIntensity);
        }

        public void Restore()
        {
            // Do not overwrite an artist's later manual change to an existing light.
            foreach (Entry entry in entries)
                if (entry.light != null && Mathf.Approximately(entry.light.intensity, entry.surveyIntensity))
                    SetIntensity(entry.light, entry.originalIntensity);
        }

        private static void SetIntensity(Light light, float intensity)
        {
            light.intensity = intensity;
#if UNITY_EDITOR
            // Existing station lights are prefab instances. Keep their saved override in sync
            // when an artist hides the details and saves/reopens the scene.
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                UnityEditor.EditorUtility.SetDirty(light);
            }
#endif
        }

        private void OnEnable() => Apply();
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
