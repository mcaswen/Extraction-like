using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using Gameplay.MapGraph.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandVisualTests : ReproductionTestFixture
    {
        private const string ScenePath="Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        [UnityTest] public IEnumerator FormalSceneRendersTheRealCompactAndExpandedCommandHud()
        {
            Assert.That(TestRunContext.Load().graphics,Is.True,"必须启用真实图形设备");
            MapGraphUguiPrefabFactory.EnsureCurrentAssets();
            EditorWindow view=null;int rendered=0;
            void Rendered(ScriptableRenderContext context,Camera[] cameras){rendered++;}
            RenderPipelineManager.endFrameRendering+=Rendered;
            try
            {
                var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                view=(EditorWindow)ScriptableObject.CreateInstance(type);view.position=new Rect(80,80,1280,800);view.ShowUtility();view.Focus();
                var loading=EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
                yield return RuntimeWait.Until(()=>loading.isDone,"正式视觉场景载入",60);Time.timeScale=4;
                yield return RuntimeWait.Until(()=>rendered>=5&&Object.FindObjectOfType<MapGraphPresenter>()!=null,"正式场景和 SRP 实际渲染",45);
                var map=Object.FindObjectOfType<MapGraphPresenter>();
                Assert.That(Object.FindObjectsOfType<RaidMinimapController>().Any(x=>x.enabled),Is.False);
                Assert.That(map.Overlay.NodeViews.Count,Is.EqualTo(28));
                double next=Time.realtimeSinceStartupAsDouble+1;
                yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=next,"等待紧凑 HUD 帧",5);
                string output=Path.Combine(TestRunContext.Load().outputPath,"visual");
                Capture(view,map,output,"01-compact-hud",rendered);
                map.Viewport.SetExpanded(true);next=Time.realtimeSinceStartupAsDouble+1;
                yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=next,"等待 M 地图帧",5);
                Capture(view,map,output,"02-expanded-map",rendered);
                ContractCompleted=true;
            }
            finally
            {
                RenderPipelineManager.endFrameRendering-=Rendered;
                if(view!=null)view.Close();
            }
        }
        [UnityTearDown]public IEnumerator UnloadFormalSceneBeforeNavigationCleanup()
        {
            var loaded=SceneManager.GetSceneByPath(ScenePath);
            if(!loaded.IsValid()||!loaded.isLoaded)yield break;
            var empty=SceneManager.CreateScene("Map visual cleanup");SceneManager.SetActiveScene(empty);
            var unload=SceneManager.UnloadSceneAsync(loaded);
            yield return RuntimeWait.Until(()=>unload.isDone,"正式视觉场景卸载",30);
        }
        private static void Capture(EditorWindow view,MapGraphPresenter map,string output,string name,int frames)
        {
            Canvas.ForceUpdateCanvases();map.TickPresentation();
            var pixels=UnityEditorViewCapture.Capture(view,Path.Combine(output,name+".png"));
            var evidence=new Evidence{scene=ScenePath,width=pixels.x,height=pixels.y,renderedFrames=frames,expanded=map.Viewport.IsExpanded,
                zones=map.Overlay.ZoneViews.Count,nodes=map.Overlay.NodeViews.Count,edges=map.Overlay.EdgeViews.Count,
                graphics=SystemInfo.graphicsDeviceName,timeScale=Time.timeScale,
                agents=map.Projection.AgentStates.Select(s=>new AgentEvidence{id=s.AgentId,root=s.RootRequestId,step=s.StepIndex,phase=s.DisplayMode.ToString(),
                    target=s.CurrentTargetNodeId,current=s.CurrentStepNodeId,edge=s.CurrentEdgeId,progress=s.CurrentEdgeProgress01,distance=s.RemainingDistance,valid=s.HasValidDistance}).ToArray()};
            File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(evidence,true));
        }
        [Serializable]private sealed class Evidence{public string scene,graphics;public int width,height,renderedFrames,zones,nodes,edges;public bool expanded;public float timeScale;public AgentEvidence[] agents;}
        [Serializable]private sealed class AgentEvidence{public string id,root,phase,target,current,edge;public int step;public float progress,distance;public bool valid;}
    }
}
