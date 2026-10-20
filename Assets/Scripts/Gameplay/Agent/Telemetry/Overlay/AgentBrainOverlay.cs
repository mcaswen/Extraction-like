#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using ExtractionLike.Aerospace;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.MapGraph.Binding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.Overlay
{
    /// <summary>
    /// U 键三维叠加层：在游戏世界里画视线、接近点、导航路径、决策图路线和攻击距离，屏幕底部一行图例。
    /// U 在"关 → 焦点智能体 → 全部智能体 → 关"之间切换；科普档案打开时隐藏、按键失效。
    /// 只读遥测，用科普系统的叠加着色器，没有碰撞体；只在编辑器和开发构建中存在。
    /// </summary>
    [DefaultExecutionOrder(10011)]
    public sealed class AgentBrainOverlay : MonoBehaviour
    {
        public enum Mode { Off, Focused, All }

        public const KeyCode ToggleKey = KeyCode.U;
        public const int SortingOrder = 279;
        private const int MaxDrawers = 4;

        private static AgentBrainOverlay _instance;

        private readonly List<AgentBrainOverlayDrawer> _drawers = new List<AgentBrainOverlayDrawer>(MaxDrawers);
        private Material _material;
        private bool _throughWalls = true;
        private MapGraphBindingAuthoring _binding;
        private float _nextBindingSearch;
        private Canvas _legend;
        private TMP_Text _modeLabel;

        public static AgentBrainOverlay Instance => _instance;

        public Mode CurrentMode { get; private set; }

        public bool IsShown => CurrentMode != Mode.Off && !AerospaceUiInputGate.BlocksGameplayInput;

        public Canvas Legend => _legend;

        public IReadOnlyList<AgentBrainOverlayDrawer> Drawers => _drawers;

        /// <summary>线是否穿墙显示（默认是）；导演拍摄需要真实遮挡关系时可关闭</summary>
        public bool ThroughWalls
        {
            get => _throughWalls;
            set
            {
                _throughWalls = value;
                ApplyDepthTest();
            }
        }

        public static AgentBrainOverlay Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("[AgentBrainOverlay]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AgentBrainOverlay>();
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => Ensure();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public void SetMode(Mode mode)
        {
            CurrentMode = mode;
            if (mode != Mode.Off)
            {
                AgentBrainTelemetryHub.Ensure();
                if (_legend == null) BuildLegend();
                AgentBrainPanel.SetText(_modeLabel, mode == Mode.All ? "全部智能体" : "焦点智能体");
            }
            ApplyVisibility();
        }

        public void Cycle() => SetMode(CurrentMode == Mode.Off ? Mode.Focused : CurrentMode == Mode.Focused ? Mode.All : Mode.Off);

        private void Update()
        {
            if (!AerospaceUiInputGate.BlocksGameplayInput && Input.GetKeyDown(ToggleKey)) Cycle();
            ApplyVisibility();
        }

        // 在遥测入口（执行顺序 10000）读完本帧结果之后
        private void LateUpdate()
        {
            if (IsShown) TickFrame();
        }

        /// <summary>每帧的工作：选出要画的智能体，按遥测更新各自的线。不分配内存</summary>
        public void TickFrame()
        {
            int used = 0;
            AgentBrainTelemetryHub hub = AgentBrainTelemetryHub.Instance;
            if (hub != null && IsShown)
            {
                FindBinding();
                if (CurrentMode == Mode.All)
                {
                    IReadOnlyList<AgentBrainRecorder> recorders = hub.Recorders;
                    for (int index = 0; index < recorders.Count && used < MaxDrawers; index++)
                        DrawWith(used++, recorders[index]);
                }
                else
                {
                    AgentBrainRecorder focused = FocusedRecorder(hub);
                    if (focused != null) DrawWith(used++, focused);
                }
            }
            for (int index = used; index < _drawers.Count; index++) _drawers[index].Hide();
        }

        private void DrawWith(int slot, AgentBrainRecorder recorder)
        {
            if (slot == _drawers.Count)
            {
                var root = new GameObject("Overlay_Agent_" + slot).transform;
                root.SetParent(transform, false);
                _drawers.Add(new AgentBrainOverlayDrawer(new AgentBrainOverlayLines(root, LineMaterial())));
            }
            _drawers[slot].Draw(recorder, _binding, Time.unscaledTime);
        }

        // 只读选择：焦点智能体，没有焦点时取第一台
        private static AgentBrainRecorder FocusedRecorder(AgentBrainTelemetryHub hub)
        {
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null) return null;
            AgentPawnRoot pawn = null;
            AgentId focus = registry.FocusedAgentId;
            if (!focus.IsEmpty && registry.TryGetHandle(focus, out AgentRuntimeHandle handle) && handle.IsValid) pawn = handle.PawnRoot;
            IReadOnlyList<AgentRuntimeHandle> handles = registry.RegisteredAgents;
            for (int index = 0; index < handles.Count && pawn == null; index++)
                if (handles[index].IsValid) pawn = handles[index].PawnRoot;
            return pawn != null ? hub.Find(pawn) : null;
        }

        // 场景里的地图绑定只读，找不到时每秒再找一次
        private void FindBinding()
        {
            if (_binding != null || Time.unscaledTime < _nextBindingSearch) return;
            _nextBindingSearch = Time.unscaledTime + 1f;
            _binding = FindObjectOfType<MapGraphBindingAuthoring>();
        }

        private Material LineMaterial()
        {
            if (_material != null) return _material;
            Shader shader = Resources.Load<Shader>("Aerospace/AnalysisOverlay");
            _material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "Agent brain overlay lines" };
            ApplyDepthTest();
            return _material;
        }

        private void ApplyDepthTest()
        {
            if (_material != null && _material.HasProperty("_ZTest"))
                _material.SetFloat("_ZTest", (float)(_throughWalls ? UnityEngine.Rendering.CompareFunction.Always : UnityEngine.Rendering.CompareFunction.LessEqual));
        }

        private void ApplyVisibility()
        {
            bool show = IsShown;
            if (_legend != null && _legend.enabled != show) _legend.enabled = show;
            if (!show)
                for (int index = 0; index < _drawers.Count; index++)
                    if (_drawers[index].Lines.UsedCount > 0) _drawers[index].Hide();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_instance == this) _instance = null;
        }

        // 图例：一行色块加文字，贴屏幕底部，居中放在 B 面板左右两栏之间（参考分辨率下 x 564–1522）
        private const float LegendCenterX = 1043, LegendHeight = 34;

        private void BuildLegend()
        {
            var canvasObject = new GameObject("AgentOverlay_Legend", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _legend = canvasObject.GetComponent<Canvas>();
            _legend.renderMode = RenderMode.ScreenSpaceOverlay;
            _legend.sortingOrder = SortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            RectTransform bar = AgentBrainPanel.Panel(canvasObject.transform, "Legend_Bar", Vector2.zero, Vector2.zero, Vector2.zero);
            AgentBrainVectorGraphic vectors = AgentBrainPanel.Vectors(bar, "Legend_Vectors", 1000, LegendHeight);
            _modeLabel = AgentBrainPanel.Label(bar, string.Empty, 14, 0, 80, LegendHeight, Style.SmallSize, Style.Muted);
            _modeLabel.name = "Legend_Mode";
            float x = 98;
            x = LineEntry(bar, vectors, x, AgentBrainOverlayDrawer.SightClear, "视线可见");
            x = LineEntry(bar, vectors, x, AgentBrainOverlayDrawer.SightBlocked, "视线被挡");
            x = RingEntry(bar, vectors, x, AgentBrainOverlayDrawer.ProbeChosen, "选中接近点");
            x = RingEntry(bar, vectors, x, AgentBrainOverlayDrawer.ProbeCannotFire, "无法开火");
            x = RingEntry(bar, vectors, x, AgentBrainOverlayDrawer.ProbeUnreachable, "走不到");
            x = LineEntry(bar, vectors, x, AgentBrainOverlayDrawer.PathColor, "导航路径");
            x = LineEntry(bar, vectors, x, AgentBrainOverlayDrawer.RouteColor, "决策路线");
            x = RingEntry(bar, vectors, x, AgentBrainOverlayDrawer.RiskColor, "计入风险的敌人");
            float width = RingEntry(bar, vectors, x, Style.Muted, "攻击距离") - 4;
            vectors.Outline(0, 0, width, LegendHeight, Style.Rule);
            vectors.Commit();
            bar.pivot = new Vector2(.5f, 0);
            bar.anchoredPosition = new Vector2(LegendCenterX, 16);
            bar.sizeDelta = new Vector2(width, LegendHeight);
            _legend.enabled = false;
        }

        private static float LineEntry(Transform bar, AgentBrainVectorGraphic vectors, float x, Color color, string text)
        {
            vectors.Fill(x, 16, 16, 3, color);
            return Entry(bar, x + 22, text);
        }

        private static float RingEntry(Transform bar, AgentBrainVectorGraphic vectors, float x, Color color, string text)
        {
            vectors.Ring(new Vector2(x + 8, 17), 5.5f, color, 2f);
            return Entry(bar, x + 22, text);
        }

        private static float Entry(Transform bar, float x, string text)
        {
            TMP_Text label = AgentBrainPanel.Label(bar, text, x, 0, 120, LegendHeight, Style.SmallSize, Style.Ink);
            label.name = "Legend_" + text;
            return x + label.GetPreferredValues(text).x + 16;
        }
    }
}
#endif
