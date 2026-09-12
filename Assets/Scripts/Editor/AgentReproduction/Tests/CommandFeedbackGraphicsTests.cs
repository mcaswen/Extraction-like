using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Targets.Input;
using Gameplay.Targets.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AgentReproduction.Tests
{
    public sealed class CommandFeedbackGraphicsTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator FormalFeedbackRendersChineseAtTopAndFades()
        {
            Assert.That(TestRunContext.Load().graphics,Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"HUD graphics",Vector3.zero);
            var camera=World.Root("Evidence camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(0.055f,0.075f,0.10f);
            camera.transform.position=new Vector3(0,18,-16);
            camera.transform.LookAt(Vector3.zero);
            camera.targetTexture=World.Own(new RenderTexture(1920,1080,24));
            camera.targetTexture.Create();
            try
            {
                World.Root("Light").AddComponent<Light>().type=LightType.Directional;
                AgentCommandFeedbackInstaller.EnsureInstalled();
                var presenter=Object.FindObjectOfType<AgentCommandFeedbackPresenter>();
                var canvas=presenter.GetComponent<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=1;
                var label=presenter.GetComponentInChildren<Text>();
                Assert.That(label.font,Is.Not.Null);
                foreach(char character in "指令下达成功失败：目标不可") Assert.That(label.font.HasCharacter(character),Is.True,character.ToString());
                Assert.That(label.rectTransform.anchorMin.y,Is.EqualTo(1));
                Assert.That(label.rectTransform.anchorMax.x,Is.EqualTo(0.5f));
                yield return null;
                // HasCharacter and different glyph indices cannot detect duplicated outlines.
                // Render both characters through the formal Text, keeping the same frame/background.
                var group=presenter.GetComponentInChildren<CanvasGroup>();
                group.alpha=1f;
                Color32[] directiveGlyph=RenderCharacter(camera,label,"指");
                Color32[] addressGlyph=RenderCharacter(camera,label,"址");
                int differentPixels=0;
                for(int i=0;i<directiveGlyph.Length;i++)
                    if(!directiveGlyph[i].Equals(addressGlyph[i])) differentPixels++;
                CaseArtifactWriter.Trace("font_glyph_difference","U+6307 vs U+5740: "+differentPixels+" pixels");
                Assert.That(differentPixels,Is.GreaterThan(10),"指 and 址 must not render as the same glyph");
                group.alpha=0f;
                AssertCachedTmpGlyph(camera,canvas,label);
                var exit=TargetFactory.Extraction(World,new Vector3(10,0,0));
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out _),Is.True);
                Time.timeScale=0;
                yield return RuntimeWait.Until(()=>presenter.Alpha>0.95f,"rendered success");
                CaseArtifactWriter.Capture(camera,"success");
                yield return RuntimeWait.Until(()=>presenter.Alpha<0.6f,"visible fade");
                CaseArtifactWriter.Capture(camera,"fading");
                yield return RuntimeWait.Until(()=>presenter.Alpha==0,"success hidden");
                var invalid=TargetFactory.Extraction(World,new Vector3(200,0,0));
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(invalid,agent.AgentIdValue,out _),Is.False);
                yield return RuntimeWait.Until(()=>presenter.Alpha>0.95f,"rendered failure");
                Assert.That(presenter.CurrentText,Is.EqualTo("指令下达失败：目标不可达"));
                CaseArtifactWriter.Capture(camera,"failure");
                yield return RuntimeWait.Until(()=>presenter.Alpha==0,"failure hidden");
                CaseArtifactWriter.Capture(camera,"hidden");
                ContractCompleted=true;
            }
            finally { camera.targetTexture=null; }
        }

        private static Color32[] RenderCharacter(Camera camera,Text label,string character)
        {
            label.text=character;
            return RenderLabelPixels(camera);
        }

        private static void AssertCachedTmpGlyph(Camera camera,Canvas canvas,Text formalLabel)
        {
            var root=new GameObject("TMP cache verification",typeof(RectTransform));
            try
            {
                root.transform.SetParent(canvas.transform,false);
                var label=root.AddComponent<TextMeshProUGUI>();
                label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/text-c SDF.asset");
                Assert.That(label.font,Is.Not.Null);
                label.fontSize=formalLabel.fontSize;
                label.alignment=TextAlignmentOptions.Center;
                label.color=formalLabel.color;
                var rect=label.rectTransform;
                rect.anchorMin=rect.anchorMax=new Vector2(0.5f,1f);
                rect.sizeDelta=formalLabel.rectTransform.sizeDelta;
                rect.anchoredPosition=formalLabel.rectTransform.anchoredPosition;
                label.text="指";
                Color32[] directiveGlyph=RenderLabelPixels(camera);
                label.text="址";
                Color32[] addressGlyph=RenderLabelPixels(camera);
                int differentPixels=0;
                for(int i=0;i<directiveGlyph.Length;i++)
                    if(!directiveGlyph[i].Equals(addressGlyph[i])) differentPixels++;
                CaseArtifactWriter.Trace("tmp_glyph_difference","U+6307 vs U+5740: "+differentPixels+" pixels");
                Assert.That(differentPixels,Is.GreaterThan(10),"TMP must use the corrected cached glyph");
                label.text="指令下达成功 / 指令下达失败：目标不可达";
                CaseArtifactWriter.Capture(camera,"tmp-corrected");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Color32[] RenderLabelPixels(Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous=RenderTexture.active;
            var pixels=new Texture2D(320,120,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=camera.targetTexture;
                pixels.ReadPixels(new Rect(800,935,320,120),0,0);
                pixels.Apply();
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active=previous;
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
