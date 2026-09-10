using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Targets.Input;
using Gameplay.Targets.Presentation;
using NUnit.Framework;
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
            camera.targetTexture=null;
            ContractCompleted=true;
        }
    }
}
