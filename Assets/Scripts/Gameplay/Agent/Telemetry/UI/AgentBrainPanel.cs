#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using ExtractionLike.Aerospace;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// B 键大脑面板（录制布局）：左栏状态机和行为树，右栏指令槽、目标评分和时间线。
    /// 只读遥测，非模态，不碰输入闸门；科普档案打开时隐藏、按键失效。
    /// 交互会话一启动就创建遥测入口：自主评分只在路线开始时发生，开局就挂上才能在打开面板时看到这条路线是怎么选出来的。
    /// 批处理（自动化测试、时长批跑）不自动挂，保持被测对象不变，需要时由测试自己创建。
    /// 默认跟随焦点智能体；点击切换按钮看另一台，焦点变化后重新跟随。只在编辑器和开发构建中存在。
    /// </summary>
    [DefaultExecutionOrder(10010)]
    public sealed partial class AgentBrainPanel : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.B;
        public const int SortingOrder = 280;
        private const int MaxAgentChips = 3;

        private static AgentBrainPanel _instance;

        private readonly AgentBrainTargetNames _names = new AgentBrainTargetNames();
        private readonly Button[] _chipButtons = new Button[MaxAgentChips];
        private readonly TMP_Text[] _chipLabels = new TMP_Text[MaxAgentChips];
        private readonly AgentPawnRoot[] _chipPawns = new AgentPawnRoot[MaxAgentChips];
        private Canvas _canvas;
        private RectTransform _left, _right;
        private AgentBrainVectorGraphic _leftVectors, _rightVectors;
        private AgentBrainRecorder _recorder;
        private AgentPawnRoot _pinnedPawn;
        private AgentId _followedFocus;
        private float _nextRefreshTime;

        public static AgentBrainPanel Instance => _instance;

        /// <summary>按 B 打开的状态；科普档案打开时仍为打开，但不显示</summary>
        public bool IsOpen { get; private set; }

        public bool IsShown => _canvas != null && _canvas.enabled;
        public Canvas Canvas => _canvas;
        public AgentBrainRecorder DisplayedRecorder => _recorder;
        public int RefreshCount { get; private set; }

        public static AgentBrainPanel Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("[AgentBrainPanel]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AgentBrainPanel>();
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Ensure();
            if (!Application.isBatchMode) AgentBrainTelemetryHub.Ensure();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public void SetOpen(bool open)
        {
            if (open && _canvas == null) Build();
            IsOpen = open;
            if (open)
            {
                AgentBrainTelemetryHub.Ensure();
                _nextRefreshTime = 0f;
            }
            ApplyVisibility();
        }

        /// <summary>立即按当前遥测刷新一次，供测试和截图使用</summary>
        public void RefreshNow()
        {
            SelectRecorder();
            Refresh();
        }

        private void Update()
        {
            if (!AerospaceUiInputGate.BlocksGameplayInput && Input.GetKeyDown(ToggleKey)) SetOpen(!IsOpen);
            ApplyVisibility();
        }

        // 在遥测入口（执行顺序 10000）读完本帧结果之后
        private void LateUpdate()
        {
            if (!IsShown) return;
            TickFrame();
            if (Time.unscaledTime < _nextRefreshTime) return;
            _nextRefreshTime = Time.unscaledTime + Style.RefreshInterval;
            Refresh();
        }

        /// <summary>每帧的工作：选择显示的智能体、移动光点。不重建网格、不分配内存</summary>
        public void TickFrame()
        {
            SelectRecorder();
            AnimatePulse();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void ApplyVisibility()
        {
            if (_canvas == null) return;
            bool show = IsOpen && !AerospaceUiInputGate.BlocksGameplayInput;
            if (_canvas.enabled != show) _canvas.enabled = show;
        }

        // 只读选择：不调用会改焦点的注册表方法
        private void SelectRecorder()
        {
            AgentPawnRoot pawn = null;
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry != null)
            {
                AgentId focus = registry.FocusedAgentId;
                if (focus != _followedFocus)
                {
                    _followedFocus = focus;
                    _pinnedPawn = null;
                }

                if (_pinnedPawn != null) pawn = _pinnedPawn;
                else if (!focus.IsEmpty && registry.TryGetHandle(focus, out AgentRuntimeHandle handle) && handle.IsValid) pawn = handle.PawnRoot;
                else
                {
                    IReadOnlyList<AgentRuntimeHandle> handles = registry.RegisteredAgents;
                    for (int index = 0; index < handles.Count && pawn == null; index++)
                        if (handles[index].IsValid) pawn = handles[index].PawnRoot;
                }
            }

            AgentBrainTelemetryHub hub = AgentBrainTelemetryHub.Instance;
            _recorder = hub != null && pawn != null ? hub.Find(pawn) : null;
        }

        private void Refresh()
        {
            RefreshCount++;
            _leftVectors.Clear();
            _rightVectors.Clear();
            DrawFrames();
            RefreshChips();
            RefreshGraph();
            RefreshTree();
            RefreshSlots();
            RefreshTimeline(RefreshScores());
            _leftVectors.Commit();
            _rightVectors.Commit();
        }

        private void Build()
        {
            var canvasObject = new GameObject("AgentBrain_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            // 左栏贴左上角，右栏贴右上角（紧凑地图下方），换分辨率时各自跟随
            _left = Panel(canvasObject.transform, "Brain_Left", new Vector2(0, 1), new Vector2(24, -222), new Vector2(540, 834));
            _right = Panel(canvasObject.transform, "Brain_Right", new Vector2(1, 1), new Vector2(-22, -386), new Vector2(376, 670));
            _leftVectors = Vectors(_left, "Left_Vectors", 540, 834);
            _rightVectors = Vectors(_right, "Right_Vectors", 376, 670);

            Label(_left, "智能体大脑", 46, 20, 200, 30, Style.TitleSize, Style.Ink, Style.HeadingFont).name = "Left_Title";
            for (int index = 0; index < MaxAgentChips; index++) BuildChip(index);
            Label(_right, "决策与指令", 40, 16, 200, 22, Style.SectionTitleSize, Style.Ink, Style.HeadingFont).name = "Right_Title";

            BuildGraph();
            BuildTree();
            BuildSlots();
            BuildScores();
            BuildTimeline();
            _canvas.enabled = false;
        }

        private void DrawFrames()
        {
            _leftVectors.Outline(0, 0, 540, 834, Style.Rule);
            _leftVectors.Fill(20, 34, 16, 2, Style.Amber);
            _rightVectors.Outline(0, 0, 376, 670, Style.Rule);
            _rightVectors.Fill(16, 26, 14, 2, Style.Amber);
        }

        // 切换按钮从右往左排；按钮本身透明，外观画在矢量层
        private const float ChipWidth = 76, ChipHeight = 28, ChipTop = 21;

        private static float ChipX(int index) => 520 - ChipWidth - (MaxAgentChips - 1 - index) * (ChipWidth + 6);

        private void BuildChip(int index)
        {
            var go = new GameObject("Agent_Chip_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_left, false);
            Rect((RectTransform)go.transform, ChipX(index), ChipTop, ChipWidth, ChipHeight);
            var image = go.GetComponent<Image>();
            image.color = new Color(1, 1, 1, 0);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            int chip = index;
            button.onClick.AddListener(() => PinChip(chip));
            _chipButtons[index] = button;
            _chipLabels[index] = Label(go.transform, string.Empty, 0, 0, ChipWidth, ChipHeight, 13, Style.Muted, null, TextAlignmentOptions.Center);
        }

        private void PinChip(int index)
        {
            if (_chipPawns[index] == null) return;
            _pinnedPawn = _chipPawns[index];
            RefreshNow();
        }

        private void RefreshChips()
        {
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            IReadOnlyList<AgentRuntimeHandle> handles = registry != null ? registry.RegisteredAgents : null;
            int count = handles != null ? Mathf.Min(handles.Count, MaxAgentChips) : 0;
            // 只有一台智能体时不显示切换按钮
            if (count < 2) count = 0;
            for (int index = 0; index < MaxAgentChips; index++)
            {
                int handleIndex = index - (MaxAgentChips - count);
                bool visible = handleIndex >= 0;
                _chipPawns[index] = visible ? handles[handleIndex].PawnRoot : null;
                _chipButtons[index].gameObject.SetActive(visible);
                if (!visible) continue;
                bool selected = _recorder != null && _chipPawns[index] == _recorder.Pawn;
                // 正式场景的智能体编号就是 1、2；其他编号按注册顺序编
                string id = _chipPawns[index].AgentIdValue;
                SetText(_chipLabels[index], "智能体 " + (id.Length > 0 && id.Length <= 2 && char.IsDigit(id[0]) ? id : (handleIndex + 1).ToString()));
                _chipLabels[index].color = selected ? Style.Ink : Style.Muted;
                float x = ChipX(index);
                _leftVectors.Fill(x, ChipTop, ChipWidth, ChipHeight, selected ? Style.SelectedFill : Style.ButtonFill);
                _leftVectors.Outline(x, ChipTop, ChipWidth, ChipHeight, selected ? Style.Teal : Style.Rule);
            }
        }

        internal static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = Style.PanelFill;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        internal static AgentBrainVectorGraphic Vectors(Transform parent, string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(AgentBrainVectorGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<AgentBrainVectorGraphic>();
            graphic.raycastTarget = false;
            Rect(graphic.rectTransform, 0, 0, w, h);
            return graphic;
        }

        internal static TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color,
            TMP_FontAsset font = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font != null ? font : Style.BodyFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.enableAutoSizing = false;
            // 不截断：中文字体的行高约 1.45 倍字号，截断模式会把比框高的整行丢掉
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = false;
            label.extraPadding = true;
            Rect(label.rectTransform, x, y, w, h);
            return label;
        }

        internal static void SetText(TMP_Text label, string text)
        {
            if (label.text != text) label.text = text;
        }

        // 父节点左上角为原点、向下为正
        private static void Rect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
#endif
