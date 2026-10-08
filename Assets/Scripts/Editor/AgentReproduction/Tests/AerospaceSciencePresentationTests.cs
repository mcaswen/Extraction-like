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
                Assert.That(ui.Stage.DisplayCamera.aspect, Is.EqualTo(1024f / 580).Within(.002f));
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
            var old = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); RenderTexture.active = old;
            string folder = Path.Combine(TestRunContext.Load().outputPath, "screenshots"); Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
            camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
