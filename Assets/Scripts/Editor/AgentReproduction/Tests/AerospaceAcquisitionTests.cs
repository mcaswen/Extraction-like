using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using ExtractionLike.Aerospace;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AgentReproduction.Tests
{
    public sealed class AerospaceAcquisitionTests : ReproductionTestFixture
    {
        AerospaceCollectionRuntime Create(int archive = 0)
        {
            PlayerPrefs.SetInt(AerospaceCollectionRuntime.ArchiveKey, archive);
            PlayerPrefs.DeleteKey("AerospaceScience.ReducedMotion.v1");
            var runtime = World.Root("Sample acquisition fixture").AddComponent<AerospaceCollectionRuntime>();
            runtime.ReserveOnStart = false;
            return runtime;
        }
        static T Named<T>(AerospaceScienceUI ui, string name) where T : Component =>
            ui.GetComponentsInChildren<T>(true).Single(v => v.name == name);
        static InventoryItemData Part(string code) => Resources.Load<InventoryItemData>("Aerospace/Items/" + code);
        static IEnumerator Until(Func<bool> ready)
        {
            double end = Time.realtimeSinceStartupAsDouble + 25;
            while (!ready() && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(ready(), Is.True, "Bounded acquisition wait timed out.");
        }
        static IEnumerator Ready(AerospaceScienceUI ui)
        {
            yield return Until(() => ui.IsOpen && !ui.IsTransitioning);
            Assert.That(ui.Stage.Ready, Is.True);
        }
        static void CleanCopy(AerospaceScienceUI ui)
        {
            foreach (string text in ui.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)
                .Concat(ui.GetComponentsInChildren<Text>(true).Select(t => t.text)))
            {
                Assert.That(text.IndexOf("ASTRA", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
                Assert.That(text, Does.Not.Contain("人工智能"));
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text, @"\bAI\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase), Is.False);
            }
        }
        static void Settled(AerospaceScienceUI ui)
        {
            Assert.That(Vector2.Distance(Named<RectTransform>(ui,"Model_Viewport").anchoredPosition,new Vector2(448,-108)), Is.LessThan(.001f));
            Assert.That(Named<RectTransform>(ui,"Model_Viewport").localScale, Is.EqualTo(Vector3.one));
            Assert.That(Named<Image>(ui,"Science_Modal").color, Is.EqualTo(new Color(.025f,.047f,.073f)));
            Assert.That(Named<Transform>(ui,"Sample_Receipt").gameObject.activeSelf, Is.False);
            Assert.That(Named<AerospaceAcquisitionGraphic>(ui,"Sample_Acquisition_Frame").gameObject.activeSelf, Is.False);
            Assert.That(Named<AerospaceAcquisitionGraphic>(ui,"Sample_Acquisition_Atmosphere").gameObject.activeSelf, Is.False);
            Assert.That(ui.Stage.ArrivalEffectsVisible, Is.False);
            Assert.That(ui.Stage.ScanProgress, Is.LessThan(0));
            Assert.That(ui.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Discovery_Hero_Icon"), Is.False);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in ui.Stage.CurrentModel.GetComponent<AerospaceModelAsset>().meshes)
            {
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Acquisition"), Is.Zero, renderer.name);
                Assert.That(block.GetFloat("_AcquisitionAge"), Is.Zero, renderer.name);
            }
            CleanCopy(ui);
        }

        [UnityTest]
        public IEnumerator FiveFirstUnlocksUseTheirOwnReceiptAndTheSameLiveModelThroughDocking()
        {
            var runtime = Create(); var ui = runtime.GetComponent<AerospaceScienceUI>();
            yield return null;
            foreach (var part in AerospaceCatalog.Load().models)
            {
                var source = new Rect(.15f,.27f,.055f,.08f);
                runtime.RecordSuccessfulPickup(Part(part.code), "1", source);
                yield return Until(() => ui.IsOpen);
                Assert.That(ui.CurrentCode, Is.EqualTo(part.code));
                Assert.That(ui.IsDiscoveryPresentation, Is.True);
                Assert.That(Named<Image>(ui,"Science_Modal").color.a, Is.LessThan(.05f), "No opaque first-frame takeover.");
                Assert.That(Named<Image>(ui,"Receipt_Part_Icon").sprite, Is.SameAs(Resources.Load<Sprite>("Aerospace/Icons/"+part.code)));
                string expectedName = new[] { "再生冷却喷管", "涡轮泵转子", "燃烧室喷注器", "可折叠栅格舵", "载荷分离机构" }[int.Parse(part.code.Substring(1))-1];
                Assert.That(Named<Transform>(ui,"Sample_Receipt").GetComponentsInChildren<TMP_Text>().Any(t => t.text == expectedName), Is.True);
                Assert.That(ui.EntryOrigin.center.x, Is.LessThan(800));
                yield return Until(() => ui.Stage.Ready && ui.EntryElapsed >= .7f);
                Assert.That(ui.IsTransitioning, Is.True);
                var model = ui.Stage.CurrentModel;
                Assert.That(model.name, Does.StartWith(part.code));
                Assert.That(Named<RawImage>(ui,"Model_Viewport").texture, Is.SameAs(ui.Stage.Texture));
                Assert.That(ui.Stage.ScanProgress, Is.InRange(0f,1f));
                Assert.That(Named<AerospaceAcquisitionGraphic>(ui,"Sample_Acquisition_Frame").canvasRenderer, Is.Not.Null);
                Assert.That(Named<Transform>(ui,"Model_Controls").gameObject.activeSelf, Is.True);
                Assert.That(Named<Button>(ui,"Mode_assembled").IsInteractable(), Is.False, "Controls fade in without accepting premature input.");
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(AerospaceUiInputGate.BlocksGameplayInput, Is.True);
                yield return Ready(ui); yield return null;
                Assert.That(ui.Stage.CurrentModel, Is.SameAs(model), "Do not swap a hero image or another model at handoff.");
                Assert.That(ui.EntryElapsed, Is.EqualTo(AerospaceScienceUI.DiscoveryDuration));
                Settled(ui); ui.Close(); yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            Assert.That(runtime.CollectedCount, Is.EqualTo(5));
            Assert.That(PlayerPrefs.GetInt(AerospaceCollectionRuntime.ArchiveKey), Is.EqualTo(31));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator HistoricalUnlocksStillCollectButOnlyANewPartAutoOpens()
        {
            var runtime = Create(15); var ui = runtime.GetComponent<AerospaceScienceUI>();
            yield return null;
            foreach (var code in new[] { "R01","R02","R03","R04" })
            {
                runtime.RecordSuccessfulPickup(Part(code),"1"); yield return null;
                Assert.That(ui.IsOpen, Is.False, code + " is already in the persistent archive.");
            }
            Assert.That(runtime.CollectedCount, Is.EqualTo(4));
            runtime.RecordSuccessfulPickup(Part("R05"),"1"); yield return Until(() => ui.IsOpen);
            Assert.That(ui.IsDiscoveryPresentation, Is.True); Assert.That(ui.CurrentCode, Is.EqualTo("R05"));
            ui.SkipReveal(); yield return Ready(ui); ui.Close(); yield return null;
            runtime.RecordSuccessfulPickup(Part("R05"),"1"); yield return null;
            Assert.That(ui.IsOpen, Is.False); Assert.That(runtime.CollectedCount, Is.EqualTo(5));
            UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            var next = World.Root("Next round").AddComponent<AerospaceCollectionRuntime>(); next.ReserveOnStart = false;
            next.RecordSuccessfulPickup(Part("R05"),"2"); yield return null;
            Assert.That(next.GetComponent<AerospaceScienceUI>().IsOpen, Is.False);
            Assert.That(next.CollectedCount, Is.EqualTo(1)); ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ArchiveReopenIsQuarterSecondWithoutReceiptScanOrPickupAudio()
        {
            var runtime = Create(31); var ui = runtime.GetComponent<AerospaceScienceUI>(); yield return null;
            foreach (var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code,false); yield return Ready(ui); ui.Close(); yield return null;
                ui.Open(part.code,false);
                Assert.That(ui.IsDiscoveryPresentation, Is.False);
                Assert.That(Named<Transform>(ui,"Sample_Receipt").gameObject.activeSelf, Is.False);
                yield return Ready(ui); yield return null;
                Assert.That(ui.EntryElapsed, Is.EqualTo(.25f));
                Assert.That(runtime.GetComponent<AudioSource>().isPlaying, Is.False);
                Assert.That(Named<AudioSource>(ui,"Sample_Transition_Audio").isPlaying, Is.False);
                Settled(ui); ui.Close(); yield return null;
            }
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ScanSpansActualBoundsForEachDistinctPart()
        {
            var runtime=Create(31); var ui=runtime.GetComponent<AerospaceScienceUI>(); yield return null;
            var block=new MaterialPropertyBlock();
            foreach(var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code,false); yield return Ready(ui); yield return null;
                var meshes=ui.Stage.CurrentModel.GetComponent<AerospaceModelAsset>().meshes;
                Bounds bounds=meshes[0].bounds; foreach(var renderer in meshes) bounds.Encapsulate(renderer.bounds);
                ui.Stage.SetArrivalPresentation(1,0,1); yield return null; yield return null;
                meshes[0].GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ScanHeight"), Is.GreaterThan(bounds.max.y),part.code);
                Assert.That(block.GetFloat("_Acquisition"), Is.EqualTo(1));
                ui.Stage.SetArrivalPresentation(1,1,1); yield return null; yield return null;
                meshes[0].GetPropertyBlock(block);
                Assert.That(block.GetFloat("_ScanHeight"), Is.LessThan(bounds.min.y),part.code);
                ui.Stage.EndArrivalPresentation(); yield return null; Settled(ui); ui.Close(); yield return null;
            }
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator SkipReducedMotionRapidReplaceAndEarlyCloseRestoreInput()
        {
            var runtime=Create(31); var ui=runtime.GetComponent<AerospaceScienceUI>(); yield return null; Time.timeScale=3;
            ui.Open("R01",true); ui.Close(); yield return null;
            Assert.That(ui.Stage.Texture, Is.Null); Assert.That(Time.timeScale,Is.EqualTo(3));
            ui.Open("R04",true); ui.SkipReveal(); yield return Ready(ui); yield return null;
            Settled(ui); ui.Close();
            ui.Open("R01",true); yield return Until(()=>ui.Stage.Ready && ui.EntryElapsed>.45f);
            ui.Open("R05",false); ui.Open("R02",false); yield return Ready(ui); yield return null;
            Assert.That(ui.CurrentCode,Is.EqualTo("R02")); Assert.That(ui.Stage.CurrentModel.name,Does.StartWith("R02"));
            Settled(ui); ui.Close();
            ui.ToggleReducedMotion(); ui.Open("R03",true); yield return Ready(ui); yield return null;
            Assert.That(ui.EntryElapsed,Is.Zero); Settled(ui); ui.Close(); yield return null; yield return null;
            Assert.That(AerospaceUiInputGate.BlocksGameplayInput,Is.False); Assert.That(Time.timeScale,Is.EqualTo(3));
            ui.ToggleReducedMotion(); ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator MissingResourceRemainsClosableAndNextOpenRecovers()
        {
            var runtime=Create(31); var ui=runtime.GetComponent<AerospaceScienceUI>(); yield return null; Time.timeScale=2;
            var quality=typeof(AerospaceScienceUI).GetField("quality",BindingFlags.Instance|BindingFlags.NonPublic);
            quality.SetValue(ui,"MissingFixture"); ui.Open("R02",true);
            yield return Until(()=>!ui.IsTransitioning);
            Assert.That(ui.Stage.Ready,Is.False);
            Assert.That(ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("模型资源缺失")),Is.True);
            Assert.That(Named<Image>(ui,"Science_Modal").color.a,Is.EqualTo(1));
            Assert.That(Named<Transform>(ui,"Sample_Receipt").gameObject.activeSelf,Is.False);
            Named<Button>(ui,"Close_Inspection").onClick.Invoke(); yield return null;
            Assert.That(Time.timeScale,Is.EqualTo(2));
            quality.SetValue(ui,"2K"); ui.Open("R02",false); yield return Ready(ui); ui.Close();
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RealBackpackReceiptUsesTransferredSlotAndLeavesTheAgentStill()
        {
            TestNavMeshBuilder.Flat(World); var agent=AgentFactory.Create(World,"1",Vector3.zero);
            var screen=InventoryFactory.Create(World); var runtime=Create(); var ui=runtime.GetComponent<AerospaceScienceUI>();
            yield return null; Time.timeScale=4;
            screen.OpenInventorySession(new InventoryScreenSessionContext{ DisplayName="样本回收",ExternalColumns=6,ExternalRows=2,
                ExternalItems=new System.Collections.Generic.List<ContainerItemSaveData>{new ContainerItemSaveData{
                    ItemData=Part("R04"),Amount=1,X=0,Y=0,RuntimeItemId=Guid.NewGuid().ToString("N"),RequiresSearch=false,IsSearched=true}}});
            yield return null;
            var item=screen.ActiveExternalGrid.ItemContainer.GetComponentsInChildren<DraggableItemUI>().Single(i=>AerospaceCatalog.CodeFor(i.ItemData)=="R04");
            Assert.That(item.TryQuickTransfer(out _),Is.True); Canvas.ForceUpdateCanvases();
            var normalized=AerospaceCollectionRuntime.NormalizedPickupRect(item.transform as RectTransform);
            Assert.That(normalized.HasValue,Is.True);
            Assert.That(normalized.Value.xMin,Is.InRange(0f,1f)); Assert.That(normalized.Value.yMax,Is.InRange(0f,1f));
            Vector3 position=agent.transform.position;
            yield return Until(()=>ui.IsOpen); Assert.That(ui.CurrentCode,Is.EqualTo("R04"));
            yield return Ready(ui);
            Assert.That(agent.transform.position,Is.EqualTo(position));
            Assert.That(screen.BackpackGrid.ExtractSaveData().Count(i=>AerospaceCatalog.CodeFor(i.ItemData)=="R04"),Is.EqualTo(1));
            ui.Close(); Assert.That(Time.timeScale,Is.Zero); Assert.That(screen.IsInventoryOpen,Is.True);
            screen.CloseInventory(); Assert.That(Time.timeScale,Is.EqualTo(4));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ScanOpticsStayPrivateBoundedAndClearWhenMotionIsReducedMidEntry()
        {
            var runtime=Create(31);var ui=runtime.GetComponent<AerospaceScienceUI>();yield return null;
            var block=new MaterialPropertyBlock();
            foreach(var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code,true);yield return Until(()=>ui.Stage.Ready && ui.EntryElapsed>.78f);
                Assert.That(ui.Stage.ArrivalEffectsVisible,Is.True,part.code);
                Assert.That(ui.Stage.ArrivalVertexCount,Is.InRange(100,4096));
                var volume=runtime.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Sample_Scan_Volume");
                Assert.That(volume.gameObject.layer,Is.EqualTo(31));
                Assert.That(volume.GetComponents<Collider>(),Is.Empty);
                Assert.That(volume.sharedMaterial.shader.isSupported,Is.True);
                Assert.That(volume.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(volume.transform.IsChildOf(ui.Stage.CurrentModel.transform),Is.False,"Scanning geometry must not rotate with the specimen.");
                var atmosphere=Named<AerospaceAcquisitionGraphic>(ui,"Sample_Acquisition_Atmosphere");
                Assert.That(atmosphere.BehindModel,Is.True);
                Assert.That(atmosphere.raycastTarget,Is.False);
                Assert.That(atmosphere.transform.GetSiblingIndex(),Is.LessThan(Named<Transform>(ui,"Model_Viewport").GetSiblingIndex()));
                ui.Stage.CurrentModel.GetComponent<AerospaceModelAsset>().meshes[0].GetPropertyBlock(block);
                Assert.That(block.GetFloat("_AcquisitionAge"),Is.GreaterThan(.65f));
                Assert.That(block.GetVector("_AcquisitionOrigin").y,Is.EqualTo(-10000));
                ui.ToggleReducedMotion();yield return Ready(ui);yield return null;Settled(ui);
                ui.Close();yield return null;
                Assert.That(ui.Stage.ArrivalVertexCount,Is.Zero);
                Assert.That(runtime.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name=="Sample_Scan_Volume"),Is.False);
                ui.ToggleReducedMotion();
            }
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator FiveEntryTimelinesHaveRenderableEvidence()
        {
            if(!TestRunContext.Load().graphics){ContractCompleted=true;yield break;}
            var runtime=Create(31); var ui=runtime.GetComponent<AerospaceScienceUI>(); yield return null;
            var present=typeof(AerospaceScienceUI).GetMethod("PresentDiscovery",BindingFlags.Instance|BindingFlags.NonPublic);
            var restore=typeof(AerospaceScienceUI).GetMethod("RestoreEntryPresentation",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var part in AerospaceCatalog.Load().models)
            {
                ui.Open(part.code,true,new Rect(.19f,.29f,.04f,.07f));
                yield return Until(()=>ui.Stage.Ready && ui.EntryElapsed>.35f);
                ui.StopAllCoroutines();
                // Sample the real Unity presentation at fixed times; behavioural tests above run it live.
                foreach(float age in new[]{.1f,.48f,.92f,1.55f,1.9f})
                {
                    present.Invoke(ui,new object[]{age});
                    yield return Capture(ui,part.code+"_Entry_"+Mathf.RoundToInt(age*1000));
                }
                restore.Invoke(ui,null); ui.Close(); yield return null;
            }
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator AcquisitionMotionRecordsUnityFramesForBothTallAndWideParts()
        {
            if(!TestRunContext.Load().graphics){ContractCompleted=true;yield break;}
            var runtime=Create(31);var ui=runtime.GetComponent<AerospaceScienceUI>();yield return null;
            var present=typeof(AerospaceScienceUI).GetMethod("PresentDiscovery",BindingFlags.Instance|BindingFlags.NonPublic);
            var restore=typeof(AerospaceScienceUI).GetMethod("RestoreEntryPresentation",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(string code in new[]{"R01","R05"})
            {
                ui.Open(code,true,new Rect(.19f,.29f,.04f,.07f));
                yield return Until(()=>ui.Stage.Ready && ui.EntryElapsed>.35f);ui.StopAllCoroutines();
                var canvas=ui.GetComponentInChildren<Canvas>();var transforms=canvas.GetComponentsInChildren<Transform>(true);
                var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
                var go=new GameObject("Acquisition sequence evidence camera");var camera=go.AddComponent<Camera>();go.AddComponent<AerospaceInspectionCamera>();
                camera.transform.position=new Vector3(0,-200,0);camera.cullingMask=1<<30;camera.depth=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.028f,.04f,.052f);
                camera.nearClipPlane=.01f;camera.farClipPlane=5;
                var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.volumeLayerMask=0;
                var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
                var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
                string folder=Path.Combine(TestRunContext.Load().outputPath,"acquisition-motion",code);Directory.CreateDirectory(folder);
                try
                {
                    foreach(var t in transforms)t.gameObject.layer=30;
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    // This is a deterministic 30 Hz visual timeline, not a performance benchmark or a loading-speed claim.
                    for(int frame=0;frame<79;frame++)
                    {
                        float age=frame/30f;
                        if(age<AerospaceScienceUI.DiscoveryDuration)present.Invoke(ui,new object[]{age});
                        else if(frame==57)
                        {
                            restore.Invoke(ui,null);ui.SkipReveal();Named<Transform>(ui,"Model_Controls").gameObject.SetActive(true);
                            typeof(AerospaceScienceUI).GetField("transitioning",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,false);
                            Named<CanvasGroup>(ui,"Responsive_Frame").interactable=true;
                            Named<CanvasGroup>(ui,"Reading_Content").interactable=true;
                            ui.Stage.Select(-1);
                            typeof(AerospaceScienceUI).GetMethod("RefreshButtons",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
                        }
                        Canvas.ForceUpdateCanvases();yield return null;yield return null;
                        var previous=RenderTexture.active;RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;
                        File.WriteAllBytes(Path.Combine(folder,frame.ToString("D3")+".png"),pixels.EncodeToPNG());
                    }
                }
                finally
                {
                    canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                    for(int i=0;i<transforms.Length;i++)if(transforms[i]!=null)transforms[i].gameObject.layer=layers[i];
                    camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
                    restore.Invoke(ui,null);ui.Close();
                }
                yield return null;
            }
            ContractCompleted=true;
        }

        static IEnumerator Capture(AerospaceScienceUI ui,string name)
        {
            var canvas=ui.GetComponentInChildren<Canvas>(); var transforms=canvas.GetComponentsInChildren<Transform>(true);
            var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
            var go=new GameObject("Entry evidence camera"); var camera=go.AddComponent<Camera>(); go.AddComponent<AerospaceInspectionCamera>();
            camera.transform.position=new Vector3(0,-200,0); camera.cullingMask=1<<30; camera.depth=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.028f,.04f,.052f);
            camera.nearClipPlane=.01f; camera.farClipPlane=5;
            var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing=false; data.volumeLayerMask=0;
            var target=new RenderTexture(1600,900,24); target.Create(); camera.targetTexture=target;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in transforms)t.gameObject.layer=30;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();yield return null;yield return null;yield return null;
                var previous=RenderTexture.active; RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();RenderTexture.active=previous;
                string folder=Path.Combine(TestRunContext.Load().outputPath,"screenshots");Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
                for(int i=0;i<transforms.Length;i++)if(transforms[i]!=null)transforms[i].gameObject.layer=layers[i];
                camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
