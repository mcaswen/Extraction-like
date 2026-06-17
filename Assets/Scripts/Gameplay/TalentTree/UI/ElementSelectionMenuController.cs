using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public sealed class ElementSelectionMenuController : MonoBehaviour
    {
        public const string SelectedElementsPlayerPrefsKey = "ElementSelectionMenu.SelectedElements";

        [SerializeField] private string gameplaySceneName = "Scene_lyl_test2 1";
        [SerializeField, Min(1)] private int requiredSelectionCount = 2;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1280f, 720f);
        [SerializeField] private RectTransform highlightRoot;
        [SerializeField]
        private HotspotSpec[] elementHotspots =
        {
            new HotspotSpec("Fire", new Vector2(475f, 276f), new Vector2(330f, 96f)),
            new HotspotSpec("Ice", new Vector2(475f, 131f), new Vector2(330f, 96f)),
            new HotspotSpec("Earth", new Vector2(475f, -18f), new Vector2(330f, 98f)),
            new HotspotSpec("Water", new Vector2(475f, -154f), new Vector2(330f, 98f)),
            new HotspotSpec("Metal", new Vector2(475f, -288f), new Vector2(330f, 96f))
        };
        [SerializeField] private HotspotSpec startHotspot = new HotspotSpec("Start", new Vector2(124f, -5f), new Vector2(270f, 112f));
        [SerializeField] private Sprite selectedButtonSprite;
        [SerializeField] private Sprite startReadySprite;
        [SerializeField] private Vector2 buttonSpriteReferenceSize = new Vector2(1280f, 720f);
        [SerializeField] private Vector2 buttonSpriteSourceCenter = new Vector2(460.3333f, 281.3333f);
        [SerializeField] private Color selectedFillColor = new Color(0.38f, 0.94f, 1f, 0.3f);
        [SerializeField] private Color selectedOutlineColor = new Color(0.7f, 1f, 1f, 0.28f);
        [SerializeField] private Color startReadyFillColor = new Color(1f, 1f, 1f, 0.26f);
        [SerializeField] private Color startReadyOutlineColor = new Color(0.62f, 0.95f, 1f, 0.28f);
        [SerializeField] private Color hoverFillColor = new Color(0.86f, 0.98f, 1f, 0.28f);
        [SerializeField] private Color hoverShiftColor = new Color(0.22f, 0.9f, 1f, 0.38f);
        [SerializeField] private Color hoverGlowColor = new Color(0.54f, 0.95f, 1f, 0.26f);
        [SerializeField, Min(0f)] private float hoverPulseSpeed = 4.6f;
        [SerializeField, Range(0f, 0.04f)] private float hoverPulseScale = 0.018f;
        [SerializeField, Range(0f, 0.35f)] private float hoverPulseAlpha = 0.24f;
        [SerializeField, Min(0f)] private float selectionPulseSpeed = 3.6f;
        [SerializeField, Range(0f, 0.08f)] private float selectionPulseScale = 0.028f;
        [SerializeField, Range(0f, 0.5f)] private float selectionPulseAlpha = 0.24f;

        private readonly List<int> selectedIndices = new List<int>();
        private SelectionHighlightVisual[] hoverHighlights;
        private SelectionHighlightVisual[] selectionHighlights;
        private SelectionHighlightVisual startReadyHighlight;

        private void Awake()
        {
            CreateHighlightObjects();
            RefreshVisuals();
        }

        private void Update()
        {
            RefreshHoverVisual();
            AnimateSelectionEffects();

            if (!Input.GetMouseButtonDown(0))
                return;

            Vector2 point = ToReferencePoint(Input.mousePosition);
            if (startHotspot.Contains(point))
            {
                StartGame();
                return;
            }

            if (elementHotspots == null)
                return;

            for (int i = 0; i < elementHotspots.Length; i++)
            {
                if (!elementHotspots[i].Contains(point))
                    continue;

                ToggleElement(i);
                return;
            }
        }

        public void ToggleElement(int index)
        {
            if (elementHotspots == null || index < 0 || index >= elementHotspots.Length)
                return;

            if (selectedIndices.Contains(index))
            {
                selectedIndices.Remove(index);
            }
            else
            {
                int selectionLimit = Mathf.Max(1, requiredSelectionCount);
                if (selectedIndices.Count >= selectionLimit)
                    selectedIndices.RemoveAt(0);

                selectedIndices.Add(index);
            }

            RefreshVisuals();
            RefreshHoverVisual();
        }

        public void StartGame()
        {
            if (selectedIndices.Count != Mathf.Max(1, requiredSelectionCount))
            {
                Debug.LogWarning("Select the required number of elements before starting.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError("Element selection gameplay scene name is empty.", this);
                return;
            }

            SaveSelection();
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void CreateHighlightObjects()
        {
            RectTransform root = highlightRoot != null ? highlightRoot : GetComponent<RectTransform>();
            if (root == null)
            {
                Debug.LogError("Element selection highlight root is missing.", this);
                return;
            }

            if (elementHotspots != null)
            {
                hoverHighlights = new SelectionHighlightVisual[elementHotspots.Length];
                selectionHighlights = new SelectionHighlightVisual[elementHotspots.Length];
                for (int i = 0; i < elementHotspots.Length; i++)
                {
                    hoverHighlights[i] = CreateHoverHighlight(root, elementHotspots[i]);
                    selectionHighlights[i] = CreateElementHighlight(root, elementHotspots[i]);
                }
            }

            startReadyHighlight = CreateStartReadyHighlight(root, startHotspot);
            MoveHighlightsBehindForeground(root);
        }

        private void RefreshVisuals()
        {
            if (selectionHighlights != null)
            {
                for (int i = 0; i < selectionHighlights.Length; i++)
                {
                    SelectionHighlightVisual highlight = selectionHighlights[i];
                    if (highlight != null)
                        highlight.SetActive(selectedIndices.Contains(i));
                }
            }

            bool canStart = selectedIndices.Count == Mathf.Max(1, requiredSelectionCount);
            if (startReadyHighlight != null)
                startReadyHighlight.SetActive(canStart);
        }

        private void RefreshHoverVisual()
        {
            int nextHoveredIndex = ResolveHoveredElementIndex();
            if (nextHoveredIndex >= 0 && selectedIndices.Contains(nextHoveredIndex))
                nextHoveredIndex = -1;

            if (hoverHighlights != null)
            {
                for (int i = 0; i < hoverHighlights.Length; i++)
                    hoverHighlights[i]?.SetActive(i == nextHoveredIndex);
            }

        }

        private void AnimateSelectionEffects()
        {
            if (hoverHighlights != null)
            {
                for (int i = 0; i < hoverHighlights.Length; i++)
                    hoverHighlights[i]?.TickColorCycle(Time.unscaledTime, hoverPulseSpeed, hoverPulseScale, hoverPulseAlpha, hoverShiftColor);
            }

            if (selectionHighlights != null)
            {
                for (int i = 0; i < selectionHighlights.Length; i++)
                    selectionHighlights[i]?.Tick(Time.unscaledTime, selectionPulseSpeed, selectionPulseScale, selectionPulseAlpha);
            }

            startReadyHighlight?.Tick(Time.unscaledTime, selectionPulseSpeed, selectionPulseScale, selectionPulseAlpha);
        }

        private void SaveSelection()
        {
            string[] selectedElements = new string[selectedIndices.Count];
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int selectedIndex = selectedIndices[i];
                selectedElements[i] = ResolveElementKey(selectedIndex);
            }

            PlayerPrefs.SetString(SelectedElementsPlayerPrefsKey, string.Join(",", selectedElements));
            PlayerPrefs.Save();
        }

        private string ResolveElementKey(int index)
        {
            if (elementHotspots != null &&
                index >= 0 &&
                index < elementHotspots.Length &&
                !string.IsNullOrWhiteSpace(elementHotspots[index].ElementKey))
            {
                return elementHotspots[index].ElementKey;
            }

            return index.ToString();
        }

        private int ResolveHoveredElementIndex()
        {
            if (elementHotspots == null)
                return -1;

            Vector2 point = ToReferencePoint(Input.mousePosition);
            for (int i = 0; i < elementHotspots.Length; i++)
            {
                if (elementHotspots[i].Contains(point))
                    return i;
            }

            return -1;
        }

        private Vector2 ToReferencePoint(Vector3 screenPosition)
        {
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            return new Vector2(
                (screenPosition.x / width * referenceResolution.x) - (referenceResolution.x * 0.5f),
                (screenPosition.y / height * referenceResolution.y) - (referenceResolution.y * 0.5f));
        }

        private SelectionHighlightVisual CreateElementHighlight(RectTransform root, HotspotSpec hotspot)
        {
            if (selectedButtonSprite != null)
            {
                Vector2 center = hotspot.Center - buttonSpriteSourceCenter;
                return CreateSpriteHighlight(
                    root,
                    hotspot.ElementKey + " Selection Glow",
                    selectedButtonSprite,
                    center,
                    buttonSpriteReferenceSize,
                    selectedFillColor,
                    selectedOutlineColor,
                    false);
            }

            return CreateBoxHighlight(root, hotspot, selectedFillColor, selectedOutlineColor);
        }

        private SelectionHighlightVisual CreateHoverHighlight(RectTransform root, HotspotSpec hotspot)
        {
            if (selectedButtonSprite != null)
            {
                Vector2 center = hotspot.Center - buttonSpriteSourceCenter;
                return CreateSpriteTintHighlight(
                    root,
                    hotspot.ElementKey + " Hover Tint",
                    selectedButtonSprite,
                    center,
                    buttonSpriteReferenceSize,
                    hoverFillColor,
                    hoverGlowColor,
                    false);
            }

            return CreateBoxHighlight(root, hotspot, hoverFillColor, hoverGlowColor);
        }

        private SelectionHighlightVisual CreateStartReadyHighlight(RectTransform root, HotspotSpec hotspot)
        {
            if (startReadySprite != null)
            {
                Vector2 size = hotspot.Size + new Vector2(28f, 10f);
                return CreateSpriteHighlight(
                    root,
                    "Start Ready Glow",
                    startReadySprite,
                    hotspot.Center,
                    size,
                    startReadyFillColor,
                    startReadyOutlineColor,
                    false);
            }

            return CreateBoxHighlight(root, hotspot, startReadyFillColor, startReadyOutlineColor);
        }

        private SelectionHighlightVisual CreateSpriteHighlight(
            RectTransform root,
            string name,
            Sprite sprite,
            Vector2 center,
            Vector2 size,
            Color coreColor,
            Color glowColor,
            bool preserveAspect)
        {
            GameObject highlightObject = new GameObject(name, typeof(RectTransform));
            highlightObject.transform.SetParent(root, false);

            RectTransform rect = highlightObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;

            List<Graphic> pulseGraphics = new List<Graphic>(17);
            Vector2[] wideGlowOffsets =
            {
                new Vector2(0f, 9f),
                new Vector2(0f, -9f),
                new Vector2(9f, 0f),
                new Vector2(-9f, 0f),
                new Vector2(7f, 7f),
                new Vector2(-7f, 7f),
                new Vector2(7f, -7f),
                new Vector2(-7f, -7f)
            };

            Vector2[] tightGlowOffsets =
            {
                new Vector2(0f, 6f),
                new Vector2(0f, -6f),
                new Vector2(6f, 0f),
                new Vector2(-6f, 0f),
                new Vector2(5f, 5f),
                new Vector2(-5f, 5f),
                new Vector2(5f, -5f),
                new Vector2(-5f, -5f)
            };

            Color wideGlow = glowColor;
            wideGlow.a *= 0.55f;
            Vector2 wideSize = size + new Vector2(16f, 10f);
            for (int i = 0; i < wideGlowOffsets.Length; i++)
                pulseGraphics.Add(CreateHighlightImage(rect, "WideGlow", sprite, wideGlow, wideGlowOffsets[i], wideSize, preserveAspect));

            Color tightGlow = glowColor;
            tightGlow.a *= 0.74f;
            for (int i = 0; i < tightGlowOffsets.Length; i++)
                pulseGraphics.Add(CreateHighlightImage(rect, "TightGlow", sprite, tightGlow, tightGlowOffsets[i], size, preserveAspect));

            pulseGraphics.Add(CreateHighlightImage(rect, "CoreGlow", sprite, coreColor, Vector2.zero, size, preserveAspect));

            highlightObject.SetActive(false);
            return new SelectionHighlightVisual(highlightObject, rect, pulseGraphics);
        }

        private SelectionHighlightVisual CreateSpriteTintHighlight(
            RectTransform root,
            string name,
            Sprite sprite,
            Vector2 center,
            Vector2 size,
            Color coreColor,
            Color glowColor,
            bool preserveAspect)
        {
            GameObject highlightObject = new GameObject(name, typeof(RectTransform));
            highlightObject.transform.SetParent(root, false);

            RectTransform rect = highlightObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;

            List<Graphic> pulseGraphics = new List<Graphic>(6);
            Vector2 glowSize = size + new Vector2(14f, 10f);
            Color edgeGlow = glowColor;
            edgeGlow.a *= 0.72f;
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverEdgeGlow", sprite, edgeGlow, new Vector2(0f, 5f), glowSize, preserveAspect));
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverEdgeGlow", sprite, edgeGlow, new Vector2(0f, -5f), glowSize, preserveAspect));
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverEdgeGlow", sprite, edgeGlow, new Vector2(5f, 0f), glowSize, preserveAspect));
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverEdgeGlow", sprite, edgeGlow, new Vector2(-5f, 0f), glowSize, preserveAspect));
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverGlow", sprite, glowColor, Vector2.zero, glowSize, preserveAspect));
            pulseGraphics.Add(CreateHighlightImage(rect, "HoverTint", sprite, coreColor, Vector2.zero, size, preserveAspect));

            highlightObject.SetActive(false);
            return new SelectionHighlightVisual(highlightObject, rect, pulseGraphics);
        }

        private SelectionHighlightVisual CreateBoxHighlight(RectTransform root, HotspotSpec hotspot, Color fillColor, Color outlineColor)
        {
            GameObject highlightObject = new GameObject(hotspot.ElementKey + " Selection Glow", typeof(RectTransform));
            highlightObject.transform.SetParent(root, false);

            RectTransform rect = highlightObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = hotspot.Center;
            rect.sizeDelta = hotspot.Size;

            List<Graphic> pulseGraphics = new List<Graphic>(2)
            {
                CreateHighlightImage(rect, "SoftGlow", null, outlineColor, Vector2.zero, hotspot.Size + new Vector2(18f, 12f), false),
                CreateHighlightImage(rect, "CoreGlow", null, fillColor, Vector2.zero, hotspot.Size, false)
            };

            highlightObject.SetActive(false);
            return new SelectionHighlightVisual(highlightObject, rect, pulseGraphics);
        }

        private Image CreateHighlightImage(
            RectTransform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 offset,
            Vector2 size,
            bool preserveAspect)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = preserveAspect;

            return image;
        }

        private void MoveHighlightsBehindForeground(RectTransform root)
        {
            int insertIndex = root.childCount;
            for (int i = 0; i < root.childCount; i++)
            {
                string childName = root.GetChild(i).name;
                if (childName.EndsWith("Icon") ||
                    childName.EndsWith("Label") ||
                    childName == "StartButtonBackground" ||
                    childName == "StartButtonText")
                {
                    insertIndex = i;
                    break;
                }
            }

            if (selectionHighlights != null)
            {
                for (int i = 0; i < selectionHighlights.Length; i++)
                    hoverHighlights[i]?.RootTransform.SetSiblingIndex(insertIndex + i);

                for (int i = 0; i < selectionHighlights.Length; i++)
                    selectionHighlights[i]?.RootTransform.SetSiblingIndex(insertIndex + hoverHighlights.Length + i);
            }

            int highlightCount = (hoverHighlights?.Length ?? 0) + (selectionHighlights?.Length ?? 0);
            startReadyHighlight?.RootTransform.SetSiblingIndex(insertIndex + highlightCount);
        }

        [System.Serializable]
        private struct HotspotSpec
        {
            public string ElementKey;
            public Vector2 Center;
            public Vector2 Size;

            public HotspotSpec(string elementKey, Vector2 center, Vector2 size)
            {
                ElementKey = elementKey;
                Center = center;
                Size = size;
            }

            public bool Contains(Vector2 point)
            {
                Vector2 halfSize = Size * 0.5f;
                return point.x >= Center.x - halfSize.x &&
                       point.x <= Center.x + halfSize.x &&
                       point.y >= Center.y - halfSize.y &&
                       point.y <= Center.y + halfSize.y;
            }
        }

        private sealed class SelectionHighlightVisual
        {
            private readonly RectTransform rectTransform;
            private readonly Graphic[] pulseGraphics;
            private readonly Color[] baseColors;

            public SelectionHighlightVisual(GameObject rootObject, RectTransform rectTransform, List<Graphic> pulseGraphics)
            {
                RootObject = rootObject;
                RootTransform = rootObject.transform;
                this.rectTransform = rectTransform;
                this.pulseGraphics = pulseGraphics.ToArray();
                baseColors = new Color[this.pulseGraphics.Length];
                for (int i = 0; i < this.pulseGraphics.Length; i++)
                    baseColors[i] = this.pulseGraphics[i].color;
            }

            public GameObject RootObject { get; }

            public Transform RootTransform { get; }

            public void SetActive(bool active)
            {
                if (RootObject.activeSelf != active)
                    RootObject.SetActive(active);
            }

            public void Tick(float time, float speed, float scaleAmount, float alphaAmount)
            {
                if (!RootObject.activeSelf)
                    return;

                float wave = (Mathf.Sin(time * speed) + 1f) * 0.5f;
                float scale = 1f + ((wave * 2f) - 1f) * scaleAmount;
                rectTransform.localScale = new Vector3(scale, scale, 1f);

                float alphaScale = Mathf.Lerp(1f - alphaAmount, 1f, wave);
                for (int i = 0; i < pulseGraphics.Length; i++)
                {
                    Color color = baseColors[i];
                    color.a *= alphaScale;
                    pulseGraphics[i].color = color;
                }
            }

            public void TickColorCycle(float time, float speed, float scaleAmount, float alphaAmount, Color shiftColor)
            {
                if (!RootObject.activeSelf)
                    return;

                float wave = (Mathf.Sin(time * speed) + 1f) * 0.5f;
                float scale = 1f + ((wave * 2f) - 1f) * scaleAmount;
                rectTransform.localScale = new Vector3(scale, scale, 1f);

                float alphaScale = Mathf.Lerp(1f - alphaAmount, 1f, wave);
                for (int i = 0; i < pulseGraphics.Length; i++)
                {
                    Color color = Color.Lerp(baseColors[i], shiftColor, wave * 0.55f);
                    color.a = baseColors[i].a * alphaScale;
                    pulseGraphics[i].color = color;
                }
            }
        }
    }
}
