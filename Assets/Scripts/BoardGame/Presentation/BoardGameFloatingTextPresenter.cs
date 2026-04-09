using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// Reusable floating-text animation helper for both HUD and world-space views.
    /// </summary>
    internal sealed class BoardGameFloatingTextPresenter
    {
        private readonly List<FloatingTextEntry> _entries = new List<FloatingTextEntry>();
        private readonly float _lifetimeSeconds;
        private readonly float _riseDistance;

        public BoardGameFloatingTextPresenter(float lifetimeSeconds, float riseDistance)
        {
            _lifetimeSeconds = Mathf.Max(0.01f, lifetimeSeconds);
            _riseDistance = riseDistance;
        }

        public void Add(TMP_Text label, Vector3 startLocalPosition, Color baseColor)
        {
            if (label == null)
            {
                return;
            }

            SetLocalPosition(label, startLocalPosition);
            _entries.Add(new FloatingTextEntry(label, startLocalPosition, baseColor));
        }

        public void Tick(float deltaTime)
        {
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                FloatingTextEntry entry = _entries[index];

                if (entry == null || entry.Label == null)
                {
                    _entries.RemoveAt(index);
                    continue;
                }

                entry.ElapsedSeconds += deltaTime;
                float progress01 = Mathf.Clamp01(entry.ElapsedSeconds / _lifetimeSeconds);
                SetLocalPosition(entry.Label, entry.StartLocalPosition + Vector3.up * (_riseDistance * progress01));

                Color color = entry.BaseColor;
                color.a *= 1f - progress01;
                entry.Label.color = color;

                if (entry.ElapsedSeconds < _lifetimeSeconds)
                {
                    continue;
                }

                Object.Destroy(entry.Label.gameObject);
                _entries.RemoveAt(index);
            }
        }

        private static void SetLocalPosition(TMP_Text label, Vector3 localPosition)
        {
            if (label == null)
            {
                return;
            }

            if (label is TextMeshProUGUI textUi && textUi.rectTransform != null)
            {
                textUi.rectTransform.anchoredPosition3D = localPosition;
                return;
            }

            label.transform.localPosition = localPosition;
        }

        private sealed class FloatingTextEntry
        {
            public FloatingTextEntry(TMP_Text label, Vector3 startLocalPosition, Color baseColor)
            {
                Label = label;
                StartLocalPosition = startLocalPosition;
                BaseColor = baseColor;
            }

            public TMP_Text Label { get; }
            public Vector3 StartLocalPosition { get; }
            public Color BaseColor { get; }
            public float ElapsedSeconds { get; set; }
        }
    }
}
