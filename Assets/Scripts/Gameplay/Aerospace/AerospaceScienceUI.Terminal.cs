using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceScienceUI
    {
        private const string MotionPreference = "AerospaceScience.ReducedMotion.v1";
        private Button demoButton, skipButton;
        private TMP_Text orientation, terminalState, motionText, demoPlayLabel, demoStepLabel, demoBody, demoTitle;
        private Image demoProgress;
        private CanvasGroup readingReveal;
        private AerospaceTerminalGraphic terminalGraphic;
        private GameObject legend;
        private TMP_Text legendPrimary, legendSecondary;
        private bool skipReveal;
        private float readingAge;
        private readonly float[] pinEmphasis = new float[4];
        private int renderedDemoStep = -1;
        private int demoReturnReadingTab, demoReturnHotspot;
        public bool ReducedMotion => stage.ReducedMotion;

        private void BuildTerminal(Transform frame)
        {
            var go = new GameObject("Inspection_Datum", typeof(RectTransform), typeof(AerospaceTerminalGraphic));
            go.transform.SetParent(frame, false); Rect(go.GetComponent<RectTransform>(), 48, 145, 1024, 580);
            go.transform.SetSiblingIndex(viewport.GetSiblingIndex());
            terminalGraphic = go.GetComponent<AerospaceTerminalGraphic>(); terminalGraphic.raycastTarget = false;
            legend = Panel(frame, "Analysis_Vector_Legend", Color.clear, 75, 248, 230, 120).gameObject;
            MonoLabel(legend.transform, "ANALYSIS OVERLAY", 0, 0, 228, 25, 11, Muted);
            Panel(legend.transform, "Path_A", Teal, 0, 35, 17, 2);
            legendPrimary = Label(legend.transform, "", 27, 22, 203, 32, 15, Teal);
            legendSecondary = Label(legend.transform, "", 27, 59, 203, 32, 15, Amber);
            Label(legend.transform, "方向示意 · 非实测数据", 0, 96, 230, 25, 12, Muted);
            legend.SetActive(false);
            terminalState = Label(frame, "样本待载入", 75, 654, 260, 26, 14, Teal);
            orientation = MonoLabel(frame, "VIEW 000 / FREE", 818, 659, 240, 24, 12, Muted);
            skipButton = Button(frame, "跳过入场", 905, 605, 150, 38, SkipReveal); skipButton.name = "Skip_Reveal";
            skipButton.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            skipButton.gameObject.SetActive(false);
        }

        public void SkipReveal()
        {
            skipReveal = true; stage.SkipScan(); transitionRect.gameObject.SetActive(false); skipButton.gameObject.SetActive(false);
        }
        public void ToggleReducedMotion()
        {
            stage.SetReducedMotion(!stage.ReducedMotion);
            PlayerPrefs.SetInt(MotionPreference, stage.ReducedMotion ? 1 : 0); PlayerPrefs.Save();
            if (stage.ReducedMotion) SkipReveal();
            RefreshButtons();
        }
        public void StartDemonstration()
        {
            if (!stage.Ready || transitioning || stage.DemoActive) return;
            demoReturnReadingTab = readingTab; demoReturnHotspot = selectedHotspot;
            selectedHotspot = hoveredPin = -1; stage.Hover(-1); stage.BeginDemonstration();
            readingTab = 6; RenderReading(); RefreshButtons();
        }
        public void EndDemonstration()
        {
            if (!stage.DemoActive) return;
            RestoreDemonstrationReading();
            RenderReading(); RefreshButtons();
        }
        private void RestoreDemonstrationReading()
        {
            if (!stage.DemoActive) return;
            stage.StopDemonstration(); readingTab = demoReturnReadingTab;
            selectedHotspot = demoReturnHotspot; hoveredPin = -1;
        }
        private void UpdateTerminal()
        {
            readingAge += Time.unscaledDeltaTime;
            if (readingReveal != null) readingReveal.alpha = stage.ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, readingAge / .23f);
            terminalGraphic.gameObject.SetActive(stage.Ready);
            legend.SetActive(stage.DemoActive);
            if (stage.DemoActive)
            {
                legendPrimary.text = current.code == "R01" ? "冷却通道路径" : current.code == "R02" ? "入口与泵端流向" : current.code == "R03" ? "通路 A / 中心通道" : current.code == "R04" ? "大气来流方向" : "相对分离方向";
                legendSecondary.text = current.code == "R03" ? "通路 B / 外围环隙" : current.code == "R02" ? "出口方向" : "";
            }
            stage.Hover(hoveredPin);
            orientation.text = "VIEW " + Mathf.RoundToInt(stage.DisplayYaw).ToString("000") + " / " + (stage.Study ? "SECTION" : stage.Mode == "exploded" ? "EXPLODED" : stage.Mode == "cutaway" ? "CUTAWAY" : "FREE");
            terminalState.text = transitioning ? "载入真实模型…" : stage.ScanProgress >= 0 ? "样本检视 · 建立结构标记" : stage.DemoActive ? "原理示意 / " + (stage.DemoPlaying ? "播放中" : "已暂停") : current != null && selectedHotspot >= 0 ? "局部检视 / " + ShortHotspot(current.code, selectedHotspot) : stage.Ready ? "交互就绪 / 拖动观察" : "等待回收样本";
            if (!transitioning && skipButton.gameObject.activeSelf && stage.ScanProgress < 0) skipButton.gameObject.SetActive(false);
            if (readingTab != 6 || !stage.DemoActive || demoStepLabel == null) return;
            if (renderedDemoStep != stage.DemoStep)
            {
                var copy = AerospaceScienceCopy.For(current.code);
                renderedDemoStep = stage.DemoStep;
                demoStepLabel.text = "0" + (renderedDemoStep + 1) + " / 03";
                demoTitle.text = copy.stepTitles[renderedDemoStep]; demoBody.text = copy.steps[renderedDemoStep];
            }
            demoPlayLabel.text = stage.DemoPlaying ? "暂停演示" : stage.DemoEnded ? "重新演示" : "播放演示";
            demoProgress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 383 * Mathf.Clamp01(stage.DemoTime / 7.2f));
        }
        private void RenderDemonstration(ref float y)
        {
            ReadText("原理演示 / SCHEMATIC", ref y, 13, Teal, 10);
            demoStepLabel = MonoLabel(detailContent, "01 / 03", 0, y, 383, 34, 23, Teal); y += 38;
            demoTitle = Label(detailContent, "", 0, y, 383, 42, 27, Ink); demoTitle.font = font; y += 44;
            demoBody = Label(detailContent, "", 0, y, 383, 110, 20, Ink); y += 116;
            Panel(detailContent, "Demo_Track", Rule, 0, y, 383, 2);
            demoProgress = Panel(detailContent, "Demo_Progress", Teal, 0, y, 1, 2).GetComponent<Image>(); y += 15;
            var play = Button(detailContent, "播放演示", 0, y, 181, 38, () => stage.ToggleDemonstration());
            play.name = "Demo_Play_Pause"; demoPlayLabel = play.GetComponentInChildren<TMP_Text>();
            var stop = Button(detailContent, "退出演示", 193, y, 190, 38, EndDemonstration); stop.name = "Demo_Stop"; y += 46;
            for (int i = 0; i < 3; i++)
            {
                int step = i;
                var button = Button(detailContent, "步骤 " + (i + 1), i * 130, y, 123, 34, () => stage.SetDemonstrationStep(step));
                button.name = "Demo_Step_" + i;
                button.GetComponentInChildren<TMP_Text>().fontSize = 14;
            }
            y += 43;
            ReadText(DemoCaveat(current.code), ref y, 15, Muted, 12);
            renderedDemoStep = -1;
        }
        private void AddLocationDiagram(ref float y)
        {
            var panel = Panel(detailContent, "Rocket_System_Locator", new Color(.09f, .16f, .21f, .85f), 0, y, 383, 132);
            Label(panel, "所在系统 / " + current.locationTitle, 14, 10, 358, 24, 15, Ink);
            var go = new GameObject("Rocket_Location_Diagram", typeof(RectTransform), typeof(AerospaceTerminalGraphic));
            go.transform.SetParent(panel, false); Rect(go.GetComponent<RectTransform>(), 14, 39, 355, 66);
            var diagram = go.GetComponent<AerospaceTerminalGraphic>(); diagram.locationCode = current.code; diagram.raycastTarget = false;
            Label(panel, "功能位置示意 · 非实物比例", 14, 108, 355, 18, 12, Muted); y += 149;
        }
        private static string DemoCaveat(string code)
        {
            switch (code)
            {
                case "R01": return "流动标记说明冷却通道中的路径概念，不是实测流场；颜色不代表温度。";
                case "R02": return "低速转动仅供观察叶轮；本件不含驱动涡轮，箭头不是计算流场或实际转速。";
                case "R03": return "两种颜色仅区分两路介质。当前是放大教学单元，不与整体样件保持尺寸比例。";
                case "R04": return "直线箭头仅表示来流；需要大气参与。折叠与气流分步展示，不模拟气动力或控制系统。";
                default: return "三步只说明约束关系。位移不是实际释放轨迹，不模拟内部装置、速度或储能。";
            }
        }
        private static string ShortHotspot(string code, int index)
        {
            string[] names;
            switch (code)
            {
                case "R01": names = new[] { "喷管热壁", "冷却通道", "外套与护罩", "集流与管路" }; break;
                case "R02": names = new[] { "螺旋诱导轮", "泵端叶轮", "轴与支撑", "壳体与出口" }; break;
                case "R03": names = new[] { "同轴单元", "面板与法兰", "分层供给", "背部接口" }; break;
                case "R04": names = new[] { "立体格栅", "边框连接", "折叠铰链", "安装座" }; break;
                default: names = new[] { "上下接口", "夹紧区域", "封闭模块", "推力元件" }; break;
            }
            return names[Mathf.Clamp(index, 0, 3)];
        }
    }
}
