using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using ExtractionLike.Aerospace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace AgentReproduction.Tests
{
    public sealed class AerospaceSciencePresentationTests : ReproductionTestFixture
    {
        AerospaceScienceUI Create(bool unlocked)
        {
            PlayerPrefs.DeleteKey(AerospaceCollectionRuntime.ArchiveKey);
            PlayerPrefs.DeleteKey("AerospaceScience.ReducedMotion.v1");
            var runtime = World.Root("Science presentation fixture").AddComponent<AerospaceCollectionRuntime>();
            runtime.ReserveOnStart = false;
            if (unlocked) PlayerPrefs.SetInt(AerospaceCollectionRuntime.ArchiveKey, 31);
            return runtime.GetComponent<AerospaceScienceUI>();
        }
        static T Named<T>(AerospaceScienceUI ui, string name) where T : Component =>
            ui.GetComponentsInChildren<T>(true).Single(c => c.name == name && c.gameObject.activeInHierarchy);
        static void Click(AerospaceScienceUI ui, string name)
        {
            var b = Named<Button>(ui, name);
            Assert.That(b.gameObject.activeInHierarchy && b.IsInteractable(), Is.True, name);
            b.onClick.Invoke();
        }
        static IEnumerator Ready(AerospaceScienceUI ui)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 25;
            while (ui.IsTransitioning && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(ui.IsTransitioning, Is.False); Assert.That(ui.Stage.Ready, Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator DossierHasPaperSidebarFiveTicketsAndHotspotToggle()
        {
            var ui = Create(true); yield return null; ui.Open("R01", false); yield return Ready(ui);
            var panel = Named<RectTransform>(ui, "Science_Reading_Panel");
            var view = Named<RectTransform>(ui, "Model_Viewport");
            Assert.That(panel.anchoredPosition.x, Is.Zero); Assert.That(panel.rect.width, Is.EqualTo(448));
            Assert.That(view.anchoredPosition.x, Is.EqualTo(448)); Assert.That(view.rect.width / view.rect.height, Is.EqualTo(ui.Stage.DisplayCamera.aspect).Within(.001f));
            var paper = panel.GetComponent<Image>().color; Assert.That(paper.r, Is.GreaterThan(.8f)); Assert.That(paper.b, Is.LessThan(paper.r));
            Assert.That(Named<TMP_Text>(ui, "Specimen_Title").text, Is.EqualTo("再生冷却\n喷管组件"));
            var rail = Enumerable.Range(1, 5).Select(i => Named<RectTransform>(ui, "Archive_R0" + i)).ToArray();
            foreach (var item in rail) Assert.That(item.anchoredPosition.y, Is.EqualTo(-12));
            for (int i = 1; i < rail.Length; i++) Assert.That(rail[i].anchoredPosition.x, Is.GreaterThan(rail[i - 1].anchoredPosition.x + rail[i - 1].rect.width));
            Assert.That(ui.HotspotsVisible, Is.True); Click(ui, "Toggle_Hotspots"); yield return null;
            Assert.That(ui.HotspotsVisible, Is.False);
            Assert.That(ui.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Hotspot_")), Is.False);
            Click(ui, "Reading_Tab_2"); Click(ui, "Detail_Selector_1"); yield return null;
            Assert.That(ui.SelectedHotspot, Is.EqualTo(1));
            Click(ui, "Toggle_Hotspots"); yield return null; Assert.That(ui.HotspotsVisible, Is.True);
            Click(ui, "Archive_R05"); yield return Ready(ui); Assert.That(ui.CurrentCode, Is.EqualTo("R05"));
            Assert.That(Named<TMP_Text>(ui, "Specimen_Title").text, Is.EqualTo("载荷分离\n机构弧段"));
            ui.Close(); Assert.That(Time.timeScale, Is.EqualTo(1)); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FiveDossiersContainScienceWithoutDisclaimersAcrossEveryReadingState()
        {
            var ui = Create(true); yield return null;
            foreach (var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code, false); yield return Ready(ui); AssertScienceCopy(ui);
                for (int tab = 0; tab < 3; tab++) { ui.SetReadingTab(tab); yield return null; AssertScienceCopy(ui); }
                for (int index = 0; index < 4; index++) { ui.ShowDetail(index); yield return null; AssertScienceCopy(ui); }
                Click(ui, "Science_Sources"); yield return null; AssertScienceCopy(ui);
                foreach (string mode in new[] { "assembled", "cutaway", "exploded" }) { Click(ui, "Mode_" + mode); yield return null; AssertScienceCopy(ui); }
                ui.StartDemonstration();
                for (int step = 0; step < 3; step++) { ui.Stage.SetDemonstrationStep(step); yield return null; AssertScienceCopy(ui); }
                ui.EndDemonstration();
                if (part.code == "R03" || part.code == "R04" || part.code == "R05")
                    for (int state = 0; state < 3; state++) { ui.ModelSpecialAction(); yield return null; AssertScienceCopy(ui); }
                ui.Close(); yield return null;
            }
            ContractCompleted = true;
        }
        static void AssertScienceCopy(AerospaceScienceUI ui)
        {
            string copy = string.Join("\n", ui.GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            foreach (string phrase in new[] { "不是", "并非", "而非", "非制造", "非实物", "非食物", "教学示意", "不代表", "不模拟", "颜色不", "不对应", "未完整复原", "没有复原", "游戏化", "仅用于观察", "仅供观察", "免责" })
                Assert.That(copy, Does.Not.Contain(phrase), ui.CurrentCode + " / " + ui.ActiveReadingTab);
        }
        [UnityTest]
        public IEnumerator DemonstrationHasAVisibleActionButtonAndSettingsHasOnlyAGear()
        {
            var ui = Create(true); yield return null; ui.Open("R04", false); yield return Ready(ui);
            var demo = Named<Button>(ui, "Start_Demonstration");
            Assert.That(demo.GetComponent<Image>().color.r, Is.GreaterThan(.8f));
            Assert.That(demo.GetComponentInChildren<AerospaceTerminalGraphic>().glyph, Is.EqualTo("play"));
            Assert.That(demo.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("原理演示"));
            var settings = Named<Button>(ui, "Display_Settings");
            Assert.That(settings.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(44, 44)));
            Assert.That(settings.GetComponentInChildren<AerospaceTerminalGraphic>().glyph, Is.EqualTo("gear"));
            Assert.That(settings.GetComponentsInChildren<TMP_Text>(), Is.Empty, "The closed settings entrance is an icon, not a text label.");
            var pointer = settings.GetComponent<AerospaceHotspotPointer>();
            pointer.OnPointerEnter(new PointerEventData(EventSystem.current)); yield return null;
            Assert.That(Named<RectTransform>(ui, "Settings_Tooltip").gameObject.activeSelf, Is.True);
            pointer.OnPointerExit(new PointerEventData(EventSystem.current));
            Click(ui, "Display_Settings"); Assert.That(ui.IsSettingsOpen, Is.True);
            Assert.That(ui.GetComponentsInChildren<RectTransform>().Any(t => t.name == "Settings_Tooltip"), Is.False);
            Assert.That(Named<Button>(ui, "Toggle_Quality").IsInteractable(), Is.True);
            Assert.That(Named<Button>(ui, "Toggle_Surface").IsInteractable(), Is.True);
            Assert.That(Named<Button>(ui, "Toggle_Reduced_Motion").IsInteractable(), Is.True);
            if (TestRunContext.Load().graphics) yield return Capture(ui, "R04_Gear_Settings", 1920, 1080);
            Click(ui, "Display_Settings_Overlay"); Assert.That(ui.IsSettingsOpen, Is.False);
            Click(ui, "Start_Demonstration"); yield return null; Assert.That(ui.Stage.DemoActive, Is.True);
            Click(ui, "Demo_Stop"); Assert.That(ui.Stage.DemoActive, Is.False);
            ui.Close(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator NativeSdfTextAndSupersampledModelStaySharpThrough4K()
        {
            var ui = Create(true); yield return null; ui.Open("R04", false); yield return Ready(ui);
            var title = Named<TMP_Text>(ui, "Specimen_Title");
            Assert.That(title, Is.TypeOf<TextMeshProUGUI>());
            Assert.That(title.enableAutoSizing, Is.False);
            Assert.That(title.font.faceInfo.familyName, Is.EqualTo("Noto Sans CJK SC"));
            Assert.That(title.GetComponentInParent<RawImage>(), Is.Null, "Text must render on the Canvas, not be baked into the model texture.");
            Assert.That(ui.Stage.Texture.antiAliasing, Is.EqualTo(4));
            Assert.That(ui.Stage.Texture.useDynamicScale, Is.False);
            Assert.That(ui.Stage.DisplayCamera.allowDynamicResolution, Is.False);
            Assert.That(AerospaceInspectionStage.RenderSizeFor(new Vector2(1152, 680)), Is.EqualTo(new Vector2Int(2016, 1190)));
            Assert.That(AerospaceInspectionStage.RenderSizeFor(new Vector2(2764.8f, 1632)), Is.EqualTo(new Vector2Int(3456, 2040)));
            if (TestRunContext.Load().graphics)
            {
                yield return Capture(ui, "R04_Dossier_1920x1080", 1920, 1080);
                yield return Capture(ui, "R04_Dossier_3840x2160", 3840, 2160);
                yield return Capture(ui, "R04_Dossier_Return_To_1600", 1600, 900);
            }
            ui.Close(); Assert.That(ui.Stage.Texture, Is.Null); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator EmptyArchiveRemainsReadableAndCloseRestoresTime()
        {
            var ui = Create(false); yield return null; Time.timeScale = 2;
            ui.Open(null, false); yield return null;
            Assert.That(ui.IsOpen, Is.True); Assert.That(ui.Stage.Ready, Is.False);
            Assert.That(ui.ActiveReadingTab, Is.EqualTo(4)); Assert.That(ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("尚无归档样本")), Is.True);
            for (int i = 0; i < 3; i++) Assert.That(Named<Button>(ui, "Reading_Tab_" + i).interactable, Is.False);
            if (TestRunContext.Load().graphics) yield return Capture(ui, "Empty_Archive", 1600, 900);
            Click(ui, "Close_Inspection"); Assert.That(Time.timeScale, Is.EqualTo(2)); Assert.That(ui.Stage.Texture, Is.Null);
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FiveSamplesHaveLayeredReadingAndWorkingHotspots()
        {
            var ui = Create(true); yield return null;
            var fonts = Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation");
            Assert.That(fonts.headingFont, Is.Not.Null); Assert.That(fonts.bodyFont, Is.Not.Null); Assert.That(fonts.monoFont, Is.Not.Null);
            Assert.That(fonts.inspectionHeading, Is.Not.Null); Assert.That(fonts.inspectionBody, Is.Not.Null); Assert.That(fonts.inspectionMono, Is.Not.Null);
            Assert.That(fonts.headingFont, Is.Not.EqualTo(fonts.font), "Display headings must not use the old stencil face.");
            foreach (var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code, false); yield return Ready(ui);
                Assert.That(ui.ActiveReadingTab, Is.Zero);
                Assert.That(Named<TMP_Text>(ui, "Overview_Summary").text.Length, Is.LessThan(150));
                Assert.That(ui.Stage.DisplayCamera.aspect, Is.EqualTo(AerospaceInspectionStage.ViewWidth / AerospaceInspectionStage.ViewHeight).Within(.002f));
                Assert.That(ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("保底投放")), Is.False);
                Click(ui, "Reading_Tab_1"); Assert.That(ui.ActiveReadingTab, Is.EqualTo(1));
                Click(ui, "Reading_Tab_2"); Assert.That(ui.ActiveReadingTab, Is.EqualTo(2));
                for (int i = 0; i < 4; i++)
                {
                    Click(ui, "Detail_Selector_" + i); yield return null;
                    Assert.That(ui.SelectedHotspot, Is.EqualTo(i));
                    Assert.That(Named<TMP_Text>(ui, "Hotspot_Explanation").text, Is.EqualTo(part.hotspots[i].text));
                    Assert.That(ui.Stage.Mode, Is.EqualTo(part.hotspots[i].view));
                }
                ui.ShowDetail(99); Assert.That(ui.SelectedHotspot, Is.EqualTo(3));
                Click(ui, "Back_To_Overview"); Assert.That(ui.ActiveReadingTab, Is.Zero);
                foreach (string mode in new[] { "cutaway", "exploded", "assembled" })
                {
                    Click(ui, "Mode_" + mode); Assert.That(ui.Stage.Mode, Is.EqualTo(mode)); yield return null;
                }
                Click(ui, "Science_Sources"); Assert.That(ui.ActiveReadingTab, Is.EqualTo(3));
                Assert.That(ui.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains(part.sources[0].label)), Is.True);
                Click(ui, "Reading_Tab_0"); yield return null;
                Assert.That(ui.Stage.CurrentModel.GetComponentsInChildren<Collider>(true), Is.Empty);
                ui.Close(); yield return null; Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator DisplaySettingsQualityOrbitAndSourcesDoNotLoseTheSample()
        {
            var ui = Create(true); yield return null; ui.Open("R01", true); yield return Ready(ui);
            Click(ui, "Display_Settings"); Assert.That(ui.IsSettingsOpen, Is.True);
            Click(ui, "Toggle_Surface"); Assert.That(ui.Stage.Recovered, Is.True);
            Click(ui, "Toggle_Quality"); Assert.That(ui.IsSettingsOpen, Is.False);
            yield return Ready(ui); Assert.That(ui.CurrentCode, Is.EqualTo("R01"));
            Click(ui, "Display_Settings"); Assert.That(Named<Button>(ui, "Toggle_Quality").GetComponentInChildren<TMP_Text>().text, Does.Contain("4K"));
            ui.SetSettings(false);
            var orbit = ui.GetComponentInChildren<AerospaceOrbitInput>();
            var before = ui.Stage.DisplayCamera.transform.position;
            orbit.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up }); yield return null;
            Assert.That(ui.Stage.DisplayCamera.transform.position, Is.Not.EqualTo(before));
            orbit.OnDrag(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, delta = new Vector2(45, 12) }); yield return null;
            Click(ui, "Reset_View"); yield return null;
            if (TestRunContext.Load().graphics) yield return Capture(ui, "R01_Overview_4K", 1600, 900);
            ui.ShowDetail(1); yield return new WaitForSecondsRealtime(.4f);
            if (TestRunContext.Load().graphics) yield return Capture(ui, "R01_Cooling_Hotspot", 1600, 900);
            Click(ui, "Reading_Tab_1"); yield return null;
            if (TestRunContext.Load().graphics) yield return Capture(ui, "R01_Principle", 1600, 900);
            Click(ui, "Close_Inspection"); yield return null;
            Assert.That(ui.Stage.Texture, Is.Null); Assert.That(ui.IsOpen, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator ReadingAndSampleControlsFitSmallFourByThreeAndUltrawideWindows()
        {
            var ui = Create(true); yield return null; ui.Open("R04", false); yield return Ready(ui);
            if (TestRunContext.Load().graphics)
            {
                yield return Capture(ui, "R04_1366x768", 1366, 768);
                yield return Capture(ui, "R04_1280x1024", 1280, 1024);
                yield return Capture(ui, "R04_2560x1080", 2560, 1080);
            }
            ui.Close(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FivePrincipleDemonstrationsPauseStepExitAndKeepTheirModel()
        {
            var ui = Create(true); yield return null;
            foreach (var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code, false); yield return Ready(ui);
                yield return new WaitForSecondsRealtime(.25f);
                if (TestRunContext.Load().graphics) yield return Capture(ui, part.code + "_Terminal_Overview", 1600, 900);
                Click(ui, "Start_Demonstration"); yield return new WaitForSecondsRealtime(.4f);
                Assert.That(ui.Stage.DemoActive && ui.Stage.DemoPlaying, Is.True);
                Assert.That(ui.Stage.TeachingPathCount, Is.GreaterThan(0));
                foreach (var vector in ui.GetComponentsInChildren<LineRenderer>())
                {
                    Assert.That(vector.widthMultiplier * vector.widthCurve.keys.Max(k => k.value), Is.LessThan(.02f), "Teaching vectors must not obscure geometry.");
                    var points = new Vector3[vector.positionCount]; vector.GetPositions(points);
                    foreach (var point in points) Assert.That(point.magnitude, Is.LessThan(2), "Teaching path must share the normalized sample scale.");
                }
                Assert.That(ui.ActiveReadingTab, Is.EqualTo(6));
                Assert.That(ui.Stage.Study, Is.EqualTo(part.code == "R03"));
                Click(ui, "Demo_Play_Pause"); float time = ui.Stage.DemoTime;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(ui.Stage.DemoTime, Is.EqualTo(time).Within(.001f));
                for (int step = 0; step < 3; step++)
                {
                    Click(ui, "Demo_Step_" + step); yield return new WaitForSecondsRealtime(.65f);
                    Assert.That(ui.Stage.DemoStep, Is.EqualTo(step)); Assert.That(ui.Stage.DemoPlaying, Is.False);
                    if (part.code == "R05") Assert.That(ui.Stage.RelationPhase, Is.EqualTo(step));
                    if (TestRunContext.Load().graphics && (step == 1 || part.code == "R05")) yield return Capture(ui, part.code + "_Demonstration_" + step, 1600, 900);
                }
                Click(ui, "Demo_Stop"); yield return null;
                Assert.That(ui.Stage.DemoActive, Is.False); Assert.That(ui.Stage.TeachingPathCount, Is.Zero);
                Assert.That(ui.Stage.Study, Is.False); Assert.That(ui.Stage.RelationPhase, Is.EqualTo(-1));
                Assert.That(ui.CurrentCode, Is.EqualTo(part.code));
                ui.StartDemonstration(); Click(ui, "Reading_Tab_0"); yield return null;
                Assert.That(ui.Stage.DemoActive, Is.False, "Reading must stop the teaching animation.");
                ui.StartDemonstration(); ui.Close(); yield return null;
                Assert.That(ui.Stage.Texture, Is.Null); Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator FocusGhostSectionAndReducedMotionAreIndependentOfGameplayTime()
        {
            var ui = Create(true); yield return null; ui.Open("R01", false); yield return Ready(ui);
            Vector3 original = ui.Stage.DisplayCamera.transform.position;
            ui.ShowDetail(1); yield return new WaitForSecondsRealtime(.8f);
            Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(1));
            Assert.That(Vector3.Distance(original, ui.Stage.DisplayCamera.transform.position), Is.GreaterThan(.02f));
            var ghosts = ui.Stage.CurrentModel.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Section_Silhouette").ToArray();
            Assert.That(ghosts.Length, Is.GreaterThan(0)); Assert.That(ghosts.Any(g => g.enabled), Is.True);
            if (TestRunContext.Load().graphics) yield return Capture(ui, "Terminal_Focused_Cooling", 1600, 900);
            ui.ShowDetail(-1); yield return new WaitForSecondsRealtime(.8f);
            Assert.That(Vector3.Distance(original, ui.Stage.DisplayCamera.transform.position), Is.LessThan(.01f));
            ui.SetSettings(true); Click(ui, "Toggle_Reduced_Motion"); Assert.That(ui.ReducedMotion, Is.True);
            ui.SetSettings(false); ui.StartDemonstration(); yield return null;
            Assert.That(ui.Stage.DemoPlaying, Is.False); Assert.That(Time.timeScale, Is.Zero);
            Click(ui, "Demo_Step_2"); yield return null; Assert.That(ui.Stage.DemoStep, Is.EqualTo(2));
            ui.Close(); ui.Open("R03", true); yield return Ready(ui);
            Assert.That(ui.Stage.ScanProgress, Is.LessThan(0)); Assert.That(ui.ReducedMotion, Is.True);
            ui.Close(); ui.ToggleReducedMotion(); PlayerPrefs.DeleteKey("AerospaceScience.ReducedMotion.v1");
            ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator DemonstrationRestoresSpecialViewFocusAndResetReallyResetsZoom()
        {
            var ui = Create(true); yield return null;
            foreach (string code in new[] { "R01", "R03", "R04", "R05" })
            {
                ui.Open(code, false); yield return Ready(ui);
                if (code == "R03" || code == "R04") ui.ModelSpecialAction();
                if (code == "R05") { ui.ModelSpecialAction(); ui.ModelSpecialAction(); ui.ModelSpecialAction(); }
                ui.Stage.Orbit(new Vector2(79, 32)); ui.Stage.Zoom(2);
                if (code == "R01") ui.ShowDetail(1);
                yield return new WaitForSecondsRealtime(.85f);
                var before = ui.Stage.DisplayCamera.transform.position;
                var rotation = ui.Stage.CurrentModel.transform.parent.localRotation;
                int tab = ui.ActiveReadingTab, hotspot = ui.SelectedHotspot, phase = ui.Stage.RelationPhase;
                string mode = ui.Stage.Mode; bool study = ui.Stage.Study, folded = ui.Stage.Folded;
                ui.StartDemonstration(); yield return new WaitForSecondsRealtime(.1f);
                ui.Stage.Orbit(new Vector2(61, 20)); ui.Stage.Zoom(-2);
                ui.EndDemonstration(); yield return new WaitForSecondsRealtime(.9f);
                Assert.That(ui.Stage.DemoActive, Is.False);
                Assert.That(ui.Stage.TeachingPathCount, Is.Zero);
                Assert.That(ui.Stage.Study, Is.EqualTo(study), code);
                Assert.That(ui.Stage.RelationPhase, Is.EqualTo(phase), code);
                Assert.That(ui.Stage.Folded, Is.EqualTo(folded), code);
                Assert.That(ui.Stage.Mode, Is.EqualTo(mode), code);
                Assert.That(ui.ActiveReadingTab, Is.EqualTo(tab), code);
                Assert.That(ui.SelectedHotspot, Is.EqualTo(hotspot), code);
                Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(hotspot), code);
                Assert.That(Vector3.Distance(before, ui.Stage.DisplayCamera.transform.position), Is.LessThan(.012f), code);
                Assert.That(Quaternion.Angle(rotation, ui.Stage.CurrentModel.transform.parent.localRotation), Is.LessThan(.01f), code);
                ui.Close();
            }
            ui.Open("R01", false); yield return Ready(ui);
            var defaultCamera = ui.Stage.DisplayCamera.transform.position;
            ui.Stage.Zoom(4); ui.ShowDetail(1); yield return new WaitForSecondsRealtime(.8f);
            Click(ui, "Reset_View"); yield return null;
            Assert.That(Vector3.Distance(defaultCamera, ui.Stage.DisplayCamera.transform.position), Is.LessThan(.01f));
            Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(-1));
            Assert.That(ui.SelectedHotspot, Is.EqualTo(-1));
            ui.Close(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator EnlargedTeachingUnitClearsOldAssemblyFocusAndRecenters()
        {
            var ui = Create(true); yield return null; ui.Open("R03", false); yield return Ready(ui);
            ui.ModelSpecialAction(); yield return new WaitForSecondsRealtime(.85f);
            var centered = ui.Stage.DisplayCamera.transform.position;
            ui.ModelSpecialAction(); ui.ShowDetail(1); yield return new WaitForSecondsRealtime(.8f);
            ui.ModelSpecialAction(); yield return new WaitForSecondsRealtime(.85f);
            Assert.That(ui.Stage.Study, Is.True);
            Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(-1));
            Assert.That(ui.SelectedHotspot, Is.EqualTo(-1));
            Assert.That(Vector3.Distance(centered, ui.Stage.DisplayCamera.transform.position), Is.LessThan(.012f));
            ui.Close(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator LeavingDemonstrationForReadingKeepsModelAndLabelSelectionInSync()
        {
            var ui = Create(true); yield return null; ui.Open("R01", false); yield return Ready(ui);
            ui.ShowDetail(1); ui.StartDemonstration();
            Click(ui, "Reading_Tab_2"); yield return null;
            Assert.That(ui.Stage.DemoActive, Is.False);
            Assert.That(ui.SelectedHotspot, Is.EqualTo(1));
            Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(1));
            Assert.That(Named<TMP_Text>(ui, "Hotspot_Explanation").text, Is.EqualTo(AerospaceCatalog.Load().Find("R01").hotspots[1].text));
            ui.StartDemonstration(); Click(ui, "Reading_Tab_0"); yield return null;
            Assert.That(ui.SelectedHotspot, Is.EqualTo(-1)); Assert.That(ui.Stage.FocusedHotspot, Is.EqualTo(-1));
            Assert.That(ui.Stage.TeachingPathCount, Is.Zero);
            ui.Close(); ContractCompleted = true;
        }
        [UnityTest]
        public IEnumerator DiscoveryCanBeSkippedAndRapidReplacementDoesNotLeaveVectorsOrPause()
        {
            var ui = Create(true); yield return null; Time.timeScale = 3;
            ui.Open("R01", true); ui.SkipReveal(); yield return Ready(ui);
            Assert.That(ui.Stage.ScanProgress, Is.LessThan(0));
            ui.StartDemonstration(); ui.Open("R05", false); ui.Open("R02", false); yield return Ready(ui);
            Assert.That(ui.CurrentCode, Is.EqualTo("R02")); Assert.That(ui.Stage.DemoActive, Is.False);
            Assert.That(ui.Stage.TeachingPathCount, Is.Zero);
            Assert.That(ui.Stage.CurrentModel.GetComponentsInChildren<Collider>(true), Is.Empty);
            ui.StartDemonstration(); ui.enabled = false; yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(3)); Assert.That(ui.Stage.Texture, Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsOfType<AerospaceTeachingFlow>(), Is.Empty);
            ContractCompleted = true;
        }
        static IEnumerator Capture(AerospaceScienceUI ui, string name, int width, int height)
        {
            // The reading panel has a .23 s unscaled reveal. Capture its settled state,
            // not the first almost-transparent frame after an otherwise-ready model.
            yield return new WaitForSecondsRealtime(.3f);
            var reading = ui.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Reading_Content");
            Assert.That(reading.alpha, Is.GreaterThanOrEqualTo(.99f));
            var canvas = ui.GetComponentInChildren<Canvas>();
            var transforms = canvas.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            var go = new GameObject("Science UI evidence camera"); var camera = go.AddComponent<Camera>(); go.AddComponent<AerospaceInspectionCamera>();
            // Layer isolation is sufficient; extreme coordinates lose sub-pixel UI precision.
            camera.transform.position = new Vector3(0, -200, 0); camera.cullingMask = 1 << 30; camera.depth = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .055f, .060f); camera.nearClipPlane = .01f; camera.farClipPlane = 5;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.volumeLayerMask = 0;
            var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
            foreach (var t in transforms) t.gameObject.layer = 30;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); yield return null; yield return null; yield return null;
            var frame = Named<RectTransform>(ui, "Responsive_Frame");
            var corners = new Vector3[4];
            foreach (string element in new[] { "Model_Viewport", "Science_Reading_Panel", "Close_Inspection", "Archive_R01", "Archive_R05" })
            {
                Named<RectTransform>(ui, element).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 local = frame.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(frame.rect.xMin - 1, frame.rect.xMax + 1), element);
                    Assert.That(local.y, Is.InRange(frame.rect.yMin - 1, frame.rect.yMax + 1), element);
                    Vector3 screen = camera.WorldToViewportPoint(corner);
                    Assert.That(screen.x, Is.InRange(-.001f, 1.001f), element);
                    Assert.That(screen.y, Is.InRange(-.001f, 1.001f), element);
                }
            }
            if (ui.Stage.Ready)
            {
                var size = AerospaceInspectionStage.RenderSizeFor(new Vector2(AerospaceInspectionStage.ViewWidth, AerospaceInspectionStage.ViewHeight) * Mathf.Min(width / 1600f, height / 900f));
                Assert.That(ui.Stage.Texture.width, Is.EqualTo(size.x), "Model render width must follow the display resolution.");
                Assert.That(ui.Stage.Texture.height, Is.EqualTo(size.y));
                Assert.That(ui.Stage.DisplayCamera.targetTexture, Is.SameAs(ui.Stage.Texture));
                Assert.That(ui.GetComponentsInChildren<RawImage>().Single(i => i.name == "Model_Viewport").texture, Is.SameAs(ui.Stage.Texture));
            }
            var old = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); RenderTexture.active = old;
            string folder = Path.Combine(TestRunContext.Load().outputPath, "screenshots"); Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
            camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
