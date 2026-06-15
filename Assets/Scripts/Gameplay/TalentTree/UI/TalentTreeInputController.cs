using Gameplay.Agent.Runtime;
using Gameplay.Agent.Talent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameplay.TalentTree.UI
{
    [DisallowMultipleComponent]
    public sealed class TalentTreeInputController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.P;
        [SerializeField] private KeyCode _closeKey = KeyCode.Escape;
        [SerializeField] private bool _ignoreInputWhileInventoryOpen = true;

        [Header("Panel")]
        [SerializeField] private TalentTreePanelController _panelPrefab;
        [SerializeField] private TalentTreePanelController _panelInstance;
        [SerializeField] private Canvas _targetCanvas;
        [SerializeField] private int _runtimeCanvasSortingOrder = 700;

        private Canvas _runtimeCanvas;

        public bool IsOpen => _panelInstance != null && _panelInstance.gameObject.activeSelf;

        private void Start()
        {
            if (_panelInstance != null)
                _panelInstance.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_ignoreInputWhileInventoryOpen &&
                global::InventoryScreenController.Instance != null &&
                global::InventoryScreenController.Instance.IsInventoryOpen)
            {
                return;
            }

            if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
            {
                TogglePanel();
                return;
            }

            if (IsOpen && _closeKey != KeyCode.None && Input.GetKeyDown(_closeKey))
            {
                ClosePanel();
            }
        }

        public void TogglePanel()
        {
            if (IsOpen)
                ClosePanel();
            else
                OpenPanel();
        }

        public void OpenPanel()
        {
            TalentTreePanelController panel = EnsurePanelInstance();
            if (panel == null)
                return;

            if (!TryResolveFocusedTalentRuntime(out AgentTalentRuntimeController talentRuntime))
            {
                Debug.LogWarning("[TalentTree] Cannot open panel: focused agent has no AgentTalentRuntimeController.", this);
                return;
            }

            panel.SetTargetTalentRuntime(talentRuntime);
            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            panel.RefreshAll();
        }

        public void ClosePanel()
        {
            if (_panelInstance != null)
                _panelInstance.gameObject.SetActive(false);
        }

        private TalentTreePanelController EnsurePanelInstance()
        {
            if (_panelInstance != null)
                return _panelInstance;

            if (_panelPrefab == null)
            {
                Debug.LogError("[TalentTree] Missing talent tree panel prefab.", this);
                return null;
            }

            Transform parent = ResolveCanvasTransform();
            _panelInstance = parent != null
                ? Instantiate(_panelPrefab, parent, false)
                : Instantiate(_panelPrefab);
            _panelInstance.name = _panelPrefab.name;
            _panelInstance.gameObject.SetActive(false);
            return _panelInstance;
        }

        private Transform ResolveCanvasTransform()
        {
            Canvas canvas = _targetCanvas != null ? _targetCanvas : EnsureRuntimeCanvas();
            if (canvas == null)
                return null;

            EnsureGraphicRaycaster(canvas);
            return canvas.transform;
        }

        private Canvas EnsureRuntimeCanvas()
        {
            if (_runtimeCanvas != null)
                return _runtimeCanvas;

            GameObject canvasObject = new GameObject(
                "TalentTreeRuntimeCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            RectTransform canvasRect = canvasObject.transform as RectTransform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            _runtimeCanvas = canvasObject.GetComponent<Canvas>();
            _runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _runtimeCanvas.overrideSorting = true;
            _runtimeCanvas.sortingOrder = _runtimeCanvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystemExists();
            return _runtimeCanvas;
        }

        private static void EnsureGraphicRaycaster(Canvas canvas)
        {
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private static void EnsureEventSystemExists()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private static bool TryResolveFocusedTalentRuntime(out AgentTalentRuntimeController talentRuntime)
        {
            talentRuntime = null;

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null || !registry.TryGetFocusedHandle(out AgentRuntimeHandle focusedHandle))
                return false;

            if (focusedHandle.PawnRoot == null)
                return false;

            talentRuntime = focusedHandle.PawnRoot.GetComponent<AgentTalentRuntimeController>();
            if (talentRuntime == null)
                return false;

            talentRuntime.EnsureInitialUnlocksApplied();
            return true;
        }
    }
}
