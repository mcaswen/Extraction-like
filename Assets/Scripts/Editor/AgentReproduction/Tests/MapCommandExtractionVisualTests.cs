using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandExtractionVisualTests : ReproductionTestFixture
    {
        private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        private const string ExitNode = "cluster_b232001ecd90d331fc0d2a4844eb5545";
        [UnityTest]
        public IEnumerator FormalExtractionCountdownRendersOffsetMarkerAndSettles()
        {
            Assert.That(TestRunContext.Load().graphics, Is.True);
            EditorWindow view = null; int rendered = 0;
            void Rendered(ScriptableRenderContext context, Camera[] cameras) { rendered++; }
            RenderPipelineManager.endFrameRendering += Rendered;
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                view = (EditorWindow)ScriptableObject.CreateInstance(type);
                view.position = new Rect(80, 80, 1280, 800); view.ShowUtility(); view.Focus();
                var loading = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return RuntimeWait.Until(() => loading.isDone, "正式撤离视觉场景载入", 60);
                yield return RuntimeWait.Until(() => rendered >= 5 && Object.FindObjectOfType<MapGraphPresenter>() != null,
                    "正式 HUD 实际渲染", 45);
                var map = Object.FindObjectOfType<MapGraphPresenter>();
                var pawn = Object.FindObjectsOfType<AgentPawnRoot>().Single(x => x.AgentIdValue == "2");
                var binding = map.Binding.TargetBindings.Single(x => x.NodeId == ExitNode);
                Assert.That(binding.TryGetNavigationAnchor(out var anchor), Is.True);
                Assert.That(NavMesh.SamplePosition(anchor + Vector3.right * 3, out var hit, 3, pawn.NavMeshAgent.areaMask), Is.True);
                var original = pawn.Position;
                Assert.That(pawn.NavMeshAgent.Warp(hit.position), Is.True, "仅布置确定性用例起点");
                CaseArtifactWriter.Trace("fixture-start", "original=" + original + "; configured=" + hit.position + "; goal=" + anchor);
                AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2");
                var results = new List<AgentRouteResult>(); pawn.RouteResultPublished += results.Add;
                Time.timeScale = 4; map.Viewport.SetExpanded(true); map.SubmitNode(ExitNode);
                string root = map.LastSubmittedResult.Request.RequestId;
                yield return RuntimeWait.Until(() => pawn != null &&
                    pawn.RouteSnapshot.CurrentStep.Phase == AgentClusterStepPhase.Extracting &&
                    map.Projection.AgentStates.Any(s => s.AgentId == "2" && s.DisplayMode.ToString() == "Extracting"),
                    "真实计时和正式地图撤离状态", 25);
                int before = rendered;
                yield return RuntimeWait.Until(() => rendered > before, "撤离状态实际绘制", 5);
                string output = Path.Combine(TestRunContext.Load().outputPath, "extraction-visual");
                Directory.CreateDirectory(output);
                var pixels = UnityEditorViewCapture.Capture(view, Path.Combine(output, "05-extracting.png"));
                var displays = SceneRaidRouteEvidence.CaptureDisplay(map);
                var actor = displays.Single(x => x.agent == "2");
                Assert.That(actor.mode, Is.EqualTo("Extracting"));
                Assert.That(actor.onEdge, Is.False);
                File.WriteAllText(Path.Combine(output, "05-extracting.json"), JsonUtility.ToJson(new Evidence {
                    fixture = "正式场景，仅把 2 号初始位置布置在雨林撤离群附近，后续由正式 Handler、导航和 Raid 执行",
                    configuredStart = hit.position, width = pixels.x, height = pixels.y, renderedFrames = rendered,
                    root = SceneRaidRouteEvidence.Capture(pawn), displays = displays
                }, true));
                yield return RuntimeWait.Until(() => results.Any(x => x.Request.RequestId == root && x.Stage == AgentRouteStage.Extracted),
                    "同一根正式撤离结算", 20);
                Assert.That(results.Any(x => x.Request.RequestId == root && x.Stage == AgentRouteStage.Failed), Is.False);
                ContractCompleted = true;
            }
            finally
            {
                RenderPipelineManager.endFrameRendering -= Rendered;
                if (view != null) view.Close();
            }
        }
        [UnityTearDown]
        public IEnumerator UnloadFormalScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var empty = SceneManager.CreateScene("Extraction visual cleanup"); SceneManager.SetActiveScene(empty);
            var unload = SceneManager.UnloadSceneAsync(scene);
            yield return RuntimeWait.Until(() => unload.isDone, "正式场景清理", 30);
        }
        [Serializable] private sealed class Evidence
        {
            public string fixture; public Vector3 configuredStart; public int width, height, renderedFrames;
            public SceneRaidRouteEvidence.RootRecord root; public SceneRaidRouteEvidence.DisplayRecord[] displays;
        }
    }
}
