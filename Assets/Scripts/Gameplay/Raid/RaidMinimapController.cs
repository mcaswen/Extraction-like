using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime-generated minimap + fullscreen map for the raid whitebox MVP.
/// </summary>
public class RaidMinimapController : MonoBehaviour
{
    private enum MarkerType
    {
        Enemy,
        LootBox,
        Teleport,
        Extraction
    }

    private sealed class MarkerVisual
    {
        public Transform Target;
        public MarkerType Type;
        public RectTransform MiniIcon;
        public RectTransform FullIcon;
    }

    public KeyCode ToggleFullMapKey = KeyCode.M;
    public Vector2 SmallMapSize = new Vector2(220f, 220f);
    public Vector2 FullMapSize = new Vector2(760f, 760f);
    public Vector2 SmallMapWorldSpan = new Vector2(180f, 180f);
    public float MarkerRefreshInterval = 0.75f;
    public bool ShowEnemyMarkers = true;
    public bool ShowLootMarkers = true;

    private Canvas _canvas;
    private RectTransform _smallMapRoot;
    private RectTransform _smallMapContent;
    private RectTransform _fullMapOverlayRoot;
    private RectTransform _fullMapContent;
    private RectTransform _miniPlayerIcon;
    private RectTransform _fullPlayerIcon;
    private Text _titleText;
    private Bounds _mapBounds;
    private readonly Dictionary<int, MarkerVisual> _markers = new Dictionary<int, MarkerVisual>();
    private Transform _playerTransform;
    private float _refreshTimer;
    private bool _isFullMapVisible;

    private static Sprite _whiteSprite;
    private static Font _defaultFont;

    private void Awake()
    {
        EnsureUi();
        RecalculateMapBounds();
        RefreshMarkers();
        SetFullMapVisible(false);
    }

    private void Update()
    {
        if (IsTogglePressed())
        {
            SetFullMapVisible(!_isFullMapVisible);
        }

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = MarkerRefreshInterval;
            ResolvePlayerTransform();
            RefreshMarkers();
            RecalculateMapBounds();
        }

        UpdateMarkerPositions();
    }

    private void EnsureUi()
    {
        EnsureSharedResources();

        GameObject canvasObject = new GameObject("RaidMinimapCanvas");
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        AdaptiveCanvasScaler.Configure(scaler, new Vector2(1920f, 1080f));
        canvasObject.AddComponent<GraphicRaycaster>();

        _smallMapRoot = CreatePanel(canvasObject.transform, "SmallMapRoot", SmallMapSize, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Color(0.06f, 0.08f, 0.12f, 0.78f));
        _smallMapContent = CreateContentRoot(_smallMapRoot, "SmallMapContent");
        _miniPlayerIcon = CreateMarkerIcon(_smallMapContent, "MiniPlayer", new Color(1f, 1f, 1f, 1f), new Vector2(12f, 12f));

        _titleText = CreateLabel(_smallMapRoot, "SmallMapTitle", "Mini Map", new Vector2(8f, -8f), TextAnchor.UpperLeft);

        _fullMapOverlayRoot = CreateStretchPanel(canvasObject.transform, "FullMapOverlay", new Color(0.02f, 0.03f, 0.05f, 0.82f));
        RectTransform fullMapPanel = CreatePanel(_fullMapOverlayRoot, "FullMapPanel", FullMapSize, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.08f, 0.1f, 0.14f, 0.92f));
        _fullMapContent = CreateContentRoot(fullMapPanel, "FullMapContent");
        _fullPlayerIcon = CreateMarkerIcon(_fullMapContent, "FullPlayer", new Color(1f, 1f, 1f, 1f), new Vector2(18f, 18f));
        CreateLabel(fullMapPanel, "FullMapTitle", "Map (M)", new Vector2(12f, -10f), TextAnchor.UpperLeft);
        CreateLegend(fullMapPanel);
    }

    private void RefreshMarkers()
    {
        HashSet<int> aliveIds = new HashSet<int>();

        if (ShowEnemyMarkers)
        {
            EnemyHealthController[] enemies = FindObjectsOfType<EnemyHealthController>(true);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealthController enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                RegisterMarker(enemy.transform, MarkerType.Enemy, aliveIds);
            }
        }

        if (ShowLootMarkers)
        {
            LootBoxEntity[] lootBoxes = FindObjectsOfType<LootBoxEntity>(true);
            for (int i = 0; i < lootBoxes.Length; i++)
            {
                LootBoxEntity lootBox = lootBoxes[i];
                if (lootBox == null || !lootBox.gameObject.activeInHierarchy)
                {
                    continue;
                }

                RegisterMarker(lootBox.transform, MarkerType.LootBox, aliveIds);
            }
        }

        TeleportPadController[] teleportPads = FindObjectsOfType<TeleportPadController>(true);
        for (int i = 0; i < teleportPads.Length; i++)
        {
            TeleportPadController pad = teleportPads[i];
            if (pad == null || !pad.gameObject.activeInHierarchy)
            {
                continue;
            }

            RegisterMarker(pad.transform, MarkerType.Teleport, aliveIds);
        }

        ExtractionPointController[] extractionPoints = FindObjectsOfType<ExtractionPointController>(true);
        for (int i = 0; i < extractionPoints.Length; i++)
        {
            ExtractionPointController extractionPoint = extractionPoints[i];
            if (extractionPoint == null || !extractionPoint.gameObject.activeInHierarchy)
            {
                continue;
            }

            RegisterMarker(extractionPoint.transform, MarkerType.Extraction, aliveIds);
        }

        List<int> toRemove = new List<int>();
        foreach (KeyValuePair<int, MarkerVisual> pair in _markers)
        {
            if (!aliveIds.Contains(pair.Key))
            {
                if (pair.Value.MiniIcon != null)
                {
                    Destroy(pair.Value.MiniIcon.gameObject);
                }

                if (pair.Value.FullIcon != null)
                {
                    Destroy(pair.Value.FullIcon.gameObject);
                }

                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _markers.Remove(toRemove[i]);
        }
    }

    private void RegisterMarker(Transform target, MarkerType type, HashSet<int> aliveIds)
    {
        if (target == null)
        {
            return;
        }

        int id = target.GetInstanceID();
        aliveIds.Add(id);
        if (_markers.ContainsKey(id))
        {
            return;
        }

        Color markerColor = GetMarkerColor(type);
        Vector2 miniSize = type == MarkerType.Enemy ? new Vector2(8f, 8f) : new Vector2(10f, 10f);
        Vector2 fullSize = type == MarkerType.Enemy ? new Vector2(12f, 12f) : new Vector2(16f, 16f);

        MarkerVisual marker = new MarkerVisual
        {
            Target = target,
            Type = type,
            MiniIcon = CreateMarkerIcon(_smallMapContent, $"{type}_Mini_{id}", markerColor, miniSize),
            FullIcon = CreateMarkerIcon(_fullMapContent, $"{type}_Full_{id}", markerColor, fullSize)
        };

        _markers.Add(id, marker);
    }

    private void UpdateMarkerPositions()
    {
        ResolvePlayerTransform();
        if (_playerTransform == null)
        {
            if (_miniPlayerIcon != null)
            {
                _miniPlayerIcon.gameObject.SetActive(false);
            }

            if (_fullPlayerIcon != null)
            {
                _fullPlayerIcon.gameObject.SetActive(false);
            }

            return;
        }

        if (_miniPlayerIcon != null)
        {
            _miniPlayerIcon.gameObject.SetActive(true);
            _miniPlayerIcon.anchoredPosition = Vector2.zero;
        }

        if (_fullPlayerIcon != null)
        {
            _fullPlayerIcon.gameObject.SetActive(true);
            _fullPlayerIcon.anchoredPosition = GetFullMapPosition(_playerTransform.position);
        }

        foreach (KeyValuePair<int, MarkerVisual> pair in _markers)
        {
            MarkerVisual marker = pair.Value;
            if (marker == null || marker.Target == null)
            {
                continue;
            }

            Vector3 worldPosition = marker.Target.position;

            if (marker.MiniIcon != null)
            {
                Vector2 delta = new Vector2(worldPosition.x - _playerTransform.position.x, worldPosition.z - _playerTransform.position.z);
                Vector2 halfSpan = SmallMapWorldSpan * 0.5f;
                bool insideMini = Mathf.Abs(delta.x) <= halfSpan.x && Mathf.Abs(delta.y) <= halfSpan.y;
                marker.MiniIcon.gameObject.SetActive(insideMini);
                if (insideMini)
                {
                    float x = (delta.x / halfSpan.x) * (_smallMapContent.rect.width * 0.5f);
                    float y = (delta.y / halfSpan.y) * (_smallMapContent.rect.height * 0.5f);
                    marker.MiniIcon.anchoredPosition = new Vector2(x, y);
                }
            }

            if (marker.FullIcon != null)
            {
                marker.FullIcon.gameObject.SetActive(true);
                marker.FullIcon.anchoredPosition = GetFullMapPosition(worldPosition);
            }
        }
    }

    private Vector2 GetFullMapPosition(Vector3 worldPosition)
    {
        float xNorm = Mathf.InverseLerp(_mapBounds.min.x, _mapBounds.max.x, worldPosition.x);
        float yNorm = Mathf.InverseLerp(_mapBounds.min.z, _mapBounds.max.z, worldPosition.z);
        float x = (xNorm - 0.5f) * _fullMapContent.rect.width;
        float y = (yNorm - 0.5f) * _fullMapContent.rect.height;
        return new Vector2(x, y);
    }

    private void ResolvePlayerTransform()
    {
        if (_playerTransform != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _playerTransform = playerObject.transform;
        }
    }

    private void RecalculateMapBounds()
    {
        RaidRegionMarker[] markers = FindObjectsOfType<RaidRegionMarker>(true);
        if (markers.Length > 0)
        {
            Bounds bounds = markers[0].GetWorldBounds();
            for (int i = 1; i < markers.Length; i++)
            {
                bounds.Encapsulate(markers[i].GetWorldBounds());
            }

            _mapBounds = bounds;
            return;
        }

        Renderer[] renderers = FindObjectsOfType<Renderer>(true);
        bool initialized = false;
        Bounds fallbackBounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];
            if (rendererComponent == null || rendererComponent is ParticleSystemRenderer || rendererComponent is TrailRenderer || rendererComponent is LineRenderer)
            {
                continue;
            }

            GameObject targetObject = rendererComponent.gameObject;
            if (targetObject.layer == LayerMask.NameToLayer("UI"))
            {
                continue;
            }

            if (!initialized)
            {
                fallbackBounds = rendererComponent.bounds;
                initialized = true;
            }
            else
            {
                fallbackBounds.Encapsulate(rendererComponent.bounds);
            }
        }

        _mapBounds = fallbackBounds;
    }

    private void SetFullMapVisible(bool isVisible)
    {
        _isFullMapVisible = isVisible;
        if (_fullMapOverlayRoot != null)
        {
            _fullMapOverlayRoot.gameObject.SetActive(isVisible);
        }
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchorMax, Vector2 anchoredPosition, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMax;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = anchorMax;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        Image image = panelObject.GetComponent<Image>();
        image.sprite = _whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return rectTransform;
    }

    private static RectTransform CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.sprite = _whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return rectTransform;
    }

    private static RectTransform CreateContentRoot(Transform parent, string name)
    {
        GameObject contentObject = new GameObject(name, typeof(RectTransform));
        contentObject.transform.SetParent(parent, false);
        RectTransform rectTransform = contentObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.08f, 0.08f);
        rectTransform.anchorMax = new Vector2(0.92f, 0.92f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return rectTransform;
    }

    private static RectTransform CreateMarkerIcon(Transform parent, string name, Color color, Vector2 size)
    {
        GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(parent, false);
        RectTransform rectTransform = iconObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = _whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return rectTransform;
    }

    private static Text CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, TextAnchor anchor)
    {
        EnsureSharedResources();

        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(-16f, 24f);

        Text label = textObject.GetComponent<Text>();
        label.font = _defaultFont;
        label.fontSize = 16;
        label.color = Color.white;
        label.alignment = anchor;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static void CreateLegend(RectTransform fullMapPanel)
    {
        CreateLegendEntry(fullMapPanel, "LegendEnemy", "Enemy", new Color(1f, 0.28f, 0.22f, 1f), new Vector2(14f, -40f));
        CreateLegendEntry(fullMapPanel, "LegendLoot", "Loot", new Color(1f, 0.84f, 0.22f, 1f), new Vector2(14f, -64f));
        CreateLegendEntry(fullMapPanel, "LegendTeleport", "Teleport", new Color(0.28f, 0.92f, 1f, 1f), new Vector2(14f, -88f));
        CreateLegendEntry(fullMapPanel, "LegendExtract", "Extract", new Color(0.28f, 1f, 0.56f, 1f), new Vector2(14f, -112f));
    }

    private static void CreateLegendEntry(RectTransform parent, string name, string labelText, Color color, Vector2 anchoredPosition)
    {
        RectTransform icon = CreateMarkerIcon(parent, $"{name}_Icon", color, new Vector2(12f, 12f));
        icon.anchorMin = new Vector2(0f, 1f);
        icon.anchorMax = new Vector2(0f, 1f);
        icon.pivot = new Vector2(0f, 1f);
        icon.anchoredPosition = anchoredPosition;

        Text label = CreateLabel(parent, $"{name}_Label", labelText, anchoredPosition + new Vector2(18f, 2f), TextAnchor.UpperLeft);
        label.fontSize = 14;
    }

    private static Color GetMarkerColor(MarkerType type)
    {
        return type switch
        {
            MarkerType.Enemy => new Color(1f, 0.28f, 0.22f, 1f),
            MarkerType.LootBox => new Color(1f, 0.84f, 0.22f, 1f),
            MarkerType.Teleport => new Color(0.28f, 0.92f, 1f, 1f),
            MarkerType.Extraction => new Color(0.28f, 1f, 0.56f, 1f),
            _ => Color.white
        };
    }

    private bool IsTogglePressed()
    {
        if (Input.GetKeyDown(ToggleFullMapKey))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

        return false;
    }

    private static void EnsureSharedResources()
    {
        if (_whiteSprite == null)
        {
            Texture2D whiteTexture = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                whiteTexture,
                new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        if (_defaultFont == null)
        {
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
