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
            go.transform.SetParent(frame, false); Rect(go.GetComponent<RectTransform>(), 448, 108, AerospaceInspectionStage.ViewWidth, AerospaceInspectionStage.ViewHeight);
            go.transform.SetSiblingIndex(viewport.GetSiblingIndex());
            terminalGraphic = go.GetComponent<AerospaceTerminalGraphic>(); terminalGraphic.raycastTarget = false;
            legend = Panel(frame, "Analysis_Vector_Legend", Color.clear, 478, 190, 230, 95).gameObject;
            Label(legend.transform, "流向与运动", 0, 0, 228, 25, 13, Muted);
            Panel(legend.transform, "Path_A", Teal, 0, 35, 17, 2);
            legendPrimary = Label(legend.transform, "", 27, 22, 203, 32, 15, Teal);
            legendSecondary = Label(legend.transform, "", 27, 59, 203, 32, 15, new Color(1, .71f, .38f));
            legend.SetActive(false);
            terminalState = Label(frame, "样本待载入", 478, 757, 530, 26, 14, Muted);
            orientation = MonoLabel(frame, "VIEW 000 / FREE", 1324, 759, 248, 24, 11, Muted);
            skipButton = Button(frame, "跳过入场", 1414, 706, 150, 38, SkipReveal); skipButton.name = "Skip_Reveal";
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
            if (stage.ReducedMotion) { SkipReveal(); ReleaseSwitchSnapshot(); ClearHotspotConfirmation(); }
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
            terminalState.text = transitioning ? "正在载入样本…" : stage.ScanProgress >= 0 ? "结构识别中" : stage.DemoActive ? "原理演示 / " + (stage.DemoPlaying ? "播放中" : "已暂停") : current != null && selectedHotspot >= 0 ? "局部结构 / " + ShortHotspot(current.code, selectedHotspot) : stage.Ready ? (stage.Study ? "同轴喷注单元" : stage.Mode == "exploded" ? "分解观察" : stage.Mode == "cutaway" ? "结构剖面" : "完整结构") : "等待回收样本";
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
            ReadText("原理演示", ref y, 12, PaperMuted, 10);
            demoStepLabel = MonoLabel(detailContent, "01 / 03", 0, y, 383, 29, 20, PaperMuted); y += 34;
            demoTitle = Label(detailContent, "", 0, y, 383, 36, 24, PaperInk); demoTitle.font = font; y += 42;
            demoBody = Label(detailContent, "", 0, y, 383, 105, 18, PaperInk); y += 115;
            Panel(detailContent, "Demo_Track", Rule, 0, y, 383, 2);
            demoProgress = Panel(detailContent, "Demo_Progress", PaperMuted, 0, y, 1, 2).GetComponent<Image>(); y += 15;
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
            renderedDemoStep = -1;
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
