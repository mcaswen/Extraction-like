#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.MapGraph.View;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Demo
{
    /// <summary>
    /// 导演模拟指挥员在地图上下令：展开地图（同 M 键），画面上一个指针移到目标群上悬停、点击，再收起地图。
    /// 悬停和点击直接调用地图群图标的指针事件，走的就是玩家点击的那条路；指针只是画面上的示意。
    /// </summary>
    public sealed class AgentDemoMapPointer : MonoBehaviour
    {
        public const int SortingOrder = 360;
        public const float OpenSeconds = .5f, MoveSeconds = .8f, HoverSeconds = .6f, ClickSeconds = .3f, HoldSeconds = .9f;
        private static readonly Color Body = new Color(.96f, .98f, 1f), Edge = new Color(.02f, .05f, .08f), Ripple = AgentBrainTerminalStyle.Accent;

        private static AgentDemoMapPointer _instance;
        private Canvas _canvas;
        private RectTransform _pointer;
        private AgentBrainVectorGraphic _graphic;

        public static AgentDemoMapPointer Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("[AgentDemoMapPointer]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AgentDemoMapPointer>();
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public static AgentDemoMapPointer Instance => _instance;

        public bool IsShown => _canvas != null && _canvas.enabled;

        /// <summary>展开地图，指针移到群上悬停、点击，停一会儿看到路线后收起地图；地图原本是展开的就保持展开</summary>
        public IEnumerator ClickNode(MapGraphPresenter presenter, string nodeId, AgentDemoLog log)
        {
            MapGraphViewport viewport = presenter.Viewport;
            bool wasExpanded = viewport.IsExpanded;
            if (!viewport.IsVisible) viewport.SetVisible(true);
            if (!wasExpanded) viewport.SetExpanded(true);
            log.Begin("map-open").Field("node", nodeId).End();
            yield return Wait(OpenSeconds);

            if (!presenter.Overlay.NodeViews.TryGetValue(nodeId, out MapGraphNodeView view))
            {
                presenter.SubmitNode(nodeId);
                log.Begin("map-click").Field("node", nodeId).Field("pointer", false).End();
            }
            else
            {
                Show();
                Vector2 target = ScreenPoint(view.RectTransform);
                Vector2 from = target + new Vector2(180f, -120f);
                float started = Time.unscaledTime;
                while (Time.unscaledTime - started < MoveSeconds)
                {
                    target = ScreenPoint(view.RectTransform);
                    float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - started) / MoveSeconds);
                    Place(Vector2.Lerp(from, target, t), 0f);
                    yield return null;
                }

                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = target };
                Place(target, 0f);
                view.OnPointerEnter(data);
                yield return Wait(HoverSeconds);

                view.OnPointerClick(data);
                log.Begin("map-click").Field("node", nodeId).Field("pointer", true).End();
                started = Time.unscaledTime;
                while (Time.unscaledTime - started < ClickSeconds)
                {
                    Place(ScreenPoint(view.RectTransform), (Time.unscaledTime - started) / ClickSeconds);
                    yield return null;
                }
                Place(ScreenPoint(view.RectTransform), 0f);
                yield return Wait(HoldSeconds);
                view.OnPointerExit(data);
                Hide();
            }

            if (!wasExpanded) viewport.SetExpanded(false);
            log.Begin("map-close").Field("node", nodeId).End();
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Show()
        {
            if (_canvas == null) Build();
            _canvas.enabled = true;
        }

        // 指针尖端放在屏幕坐标上；ripple 从 0 到 1 时在尖端画一圈扩散的点击波纹
        private void Place(Vector2 screen, float ripple)
        {
            _pointer.anchoredPosition = screen;
            _graphic.Clear();
            if (ripple > 0f)
            {
                var color = new Color(Ripple.r, Ripple.g, Ripple.b, 1f - ripple);
                _graphic.Ring(Vector2.zero, 6f + ripple * 18f, color, 2f);
            }
            _graphic.Triangle(new Vector2(-1.5f, -2.5f), new Vector2(-1.5f, 25f), new Vector2(19f, 18f), Edge);
            _graphic.Triangle(new Vector2(5f, 17f), new Vector2(10f, 29f), new Vector2(15f, 26f), Edge);
            _graphic.Triangle(Vector2.zero, new Vector2(0f, 21f), new Vector2(15f, 16f), Body);
            _graphic.Triangle(new Vector2(6f, 17f), new Vector2(10.5f, 27f), new Vector2(13f, 25.5f), Body);
            _graphic.Commit();
        }

        private void Build()
        {
            var canvasObject = new GameObject("AgentDemo_Pointer", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var go = new GameObject("Pointer", typeof(RectTransform), typeof(AgentBrainVectorGraphic));
            go.transform.SetParent(canvasObject.transform, false);
            _pointer = (RectTransform)go.transform;
            _pointer.anchorMin = _pointer.anchorMax = Vector2.zero;
            _pointer.pivot = new Vector2(0f, 1f);
            _pointer.sizeDelta = new Vector2(32f, 32f);
            _graphic = go.GetComponent<AgentBrainVectorGraphic>();
            _graphic.raycastTarget = false;
        }

        private static Vector2 ScreenPoint(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera camera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.position);
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }
    }
}
#endif
