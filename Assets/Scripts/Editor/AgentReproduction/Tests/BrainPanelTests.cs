using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using ExtractionLike.Aerospace;
using Gameplay.Agent.AI.Trees;
using Gameplay.Agent.Core;
using Gameplay.Agent.Telemetry;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace AgentReproduction.Tests
{
    /// <summary>
    /// B 面板：配色与科普界面一致；真实大脑"指挥员搜索 → 受击反击"时各区内容正确，并渲染一张 1920×1080 截图供目视；
    /// 正式场景里候选显示地图上的中文名，并截图；科普档案打开时隐藏；逐帧工作不分配内存；面板会显示的中文全部预烘焙进字体；行为树节点都有中文名。
    /// </summary>
    public sealed class BrainPanelTests : ReproductionTestFixture
    {
        private static readonly string[] SharedColors = { "Ink", "Muted", "Accent", "Teal", "Rule", "ButtonFill", "SelectedFill" };

        [UnityTest]
        public IEnumerator PanelStyleMatchesScienceTerminal()
        {
            foreach (string name in SharedColors)
            {
                var science = (Color)typeof(AerospaceScienceUI).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var panel = (Color)typeof(AgentBrainTerminalStyle).GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Assert.That(panel, Is.EqualTo(science), "面板颜色 " + name + " 应与科普界面相同");
            }

            var fonts = Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation");
            Assert.That(AgentBrainTerminalStyle.BodyFont, Is.SameAs(fonts.inspectionBody));
            Assert.That(AgentBrainTerminalStyle.HeadingFont, Is.SameAs(fonts.inspectionHeading));
            Assert.That(AgentBrainTerminalStyle.MonoFont, Is.SameAs(fonts.inspectionMono));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PanelShowsRetaliationOverSuspendedCommand()
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Panel", Vector3.zero, 8, true, false);
            AgentFactory.Create(World, "Panel2", new Vector3(-6, 0, -6), 0, false, false);
            TargetFactory.Resources(World, new Vector3(-18, 0, 14));
            var commanded = TargetFactory.Resources(World, new Vector3(45, 0, 0));
            TargetFactory.Extraction(World, new Vector3(0, 0, -70));
            var panel = AgentBrainPanel.Ensure();
            try
            {
                panel.SetOpen(true);
                yield return null;
                AgentBrainRecorder recorder = AgentBrainTelemetryHub.Instance.Find(agent);
                Assert.That(recorder, Is.Not.Null, "打开面板后应为智能体挂上记录器");
                yield return RuntimeWait.Until(() => recorder.RoundTotal > 0, "decision round recorded", 5);

                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(commanded, agent.AgentIdValue, out var order), Is.True);
                yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == order.CommandId &&
                                                     recorder.CurrentLeaf != null && recorder.CurrentLeaf.StateName == "SearchResource",
                    "commander search runs", 5);
                yield return Frames(.3f);

                var attacker = EnemyFactory.Passive(World, agent.Position + new Vector3(0, 0, 10));
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                yield return RuntimeWait.Until(() => recorder.CurrentLeaf != null && recorder.CurrentLeaf.StateName == "Combat", "retaliation enters combat", 5);
                yield return Frames(.2f);
                panel.RefreshNow();

                Assert.That(panel.IsShown, Is.True);
                Assert.That(panel.DisplayedRecorder, Is.SameAs(recorder), "面板应显示焦点智能体");
                Assert.That(Text(panel, "State_Combat").font, Is.SameAs(AgentBrainTerminalStyle.HeadingFont), "当前状态用粗体");
                Assert.That(Named(panel, "State_Previous_Tag").activeSelf, Is.True, "转移来源标为上一状态");
                Assert.That(Text(panel, "Edge_Fired_Label").text, Does.StartWith("受击打断"));
                Assert.That(Text(panel, "Slot_Active_What").text, Is.EqualTo("交战"));
                Assert.That(Text(panel, "Slot_Active_Target").text, Does.StartWith("反击"));
                Assert.That(Text(panel, "Slot_Held_What").text, Is.EqualTo("搜索"));
                Assert.That(Text(panel, "Slot_Held_Target").text, Does.StartWith("指挥员"));
                Assert.That(Text(panel, "Tree_Row_00").text, Is.EqualTo("交战记忆"));
                var rows = Enumerable.Range(0, AgentBrainPanel.MaxTreeRows).Select(i => Named(panel, "Tree_Row_" + i.ToString("00")))
                    .Where(row => row.activeSelf).Select(row => row.GetComponent<TMP_Text>().text).ToList();
                Assert.That(rows, Has.Some.EqualTo("交战"));
                Assert.That(rows.Any(row => Regex.IsMatch(row, "[A-Za-z]{3,}") && !row.Contains("<size")), Is.False, "行为树行不应出现英文：" + string.Join(" / ", rows));
                Assert.That(Text(panel, "Score_Row_0_Name").gameObject.activeSelf, Is.True, "应显示最近一轮评分");
                Assert.That(Text(panel, "Score_Formula").text, Does.StartWith("S = B - λ("));
                var timeline = Enumerable.Range(0, AgentBrainPanel.TimelineRows).Select(i => Named(panel, "Timeline_Row_" + i + "_Text"))
                    .Where(row => row.activeSelf).Select(row => row.GetComponent<TMP_Text>().text).ToList();
                Assert.That(timeline, Has.Some.Contains("搜索命令挂起"));
                Assert.That(timeline, Has.Some.Contains("搜索资源 → 战斗"));
                CaseArtifactWriter.Trace("brain-panel", "tree=" + string.Join(" / ", rows) + "; timeline=" + string.Join(" / ", timeline));

                yield return Capture(panel, agent, "brain-panel-retaliation");
                // 再等到开火间隙（等待冷却在运行）拍一张，与布局稿的时刻一致；等不到也照拍
                float giveUp = Time.time + 6f;
                while (Time.time < giveUp && !Named(panel, "Tree_Focus_Detail").activeSelf && recorder.CurrentLeaf?.StateName == "Combat") yield return null;
                yield return Capture(panel, agent, "brain-panel-cooldown");

                // 科普档案打开时隐藏，关上后恢复（闸门在释放后多拦一帧）
                AerospaceUiInputGate.Set(true);
                yield return null;
                Assert.That(panel.IsShown, Is.False, "科普档案打开时面板隐藏");
                Assert.That(panel.IsOpen, Is.True);
                AerospaceUiInputGate.Set(false);
                yield return null;
                yield return null;
                Assert.That(panel.IsShown, Is.True);

                // 面板关掉后单独量逐帧工作（选择智能体、移动光点），排除 10 Hz 的文字刷新
                panel.SetOpen(false);
                long allocated = 0;
                yield return ManagedAllocation.MeasureFrame(() =>
                {
                    for (int i = 0; i < 200; i++) panel.TickFrame();
                }, value => allocated = value);
                CaseArtifactWriter.Trace("brain-panel-allocation", "bytes=" + allocated + " for 200 frames of work");
                if (allocated != ManagedAllocation.Unavailable) Assert.That(allocated, Is.LessThan(1024), "面板逐帧工作不应分配内存");
                ContractCompleted = true;
            }
            finally
            {
                AerospaceUiInputGate.Set(false);
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
                if (AgentBrainTelemetryHub.Instance != null) Object.DestroyImmediate(AgentBrainTelemetryHub.Instance.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PanelOverFinalSceneShowsMapNamesAndRetaliation()
        {
            // 与交互会话一致：开局前就挂上遥测，才能拿到选路线时的那轮评分
            AgentBrainTelemetryHub.Ensure();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            var panel = AgentBrainPanel.Ensure();
            // 演示时面板和叠加层一起开，截图也一起拍
            var overlay = Gameplay.Agent.Telemetry.Overlay.AgentBrainOverlay.Ensure();
            try
            {
                panel.SetOpen(true);
                overlay.SetMode(Gameplay.Agent.Telemetry.Overlay.AgentBrainOverlay.Mode.Focused);
                overlay.OutputHeight = 1080;
                yield return null;
                AgentBrainRecorder recorder = null;
                float deadline = Time.realtimeSinceStartup + 60f;
                while (Time.realtimeSinceStartup < deadline &&
                       !((recorder = panel.DisplayedRecorder) != null && recorder.RoundTotal > 0 && recorder.GetRound(0).Count >= 3))
                    yield return null;
                if (recorder == null || recorder.RoundTotal == 0 || recorder.GetRound(0).Count < 3)
                {
                    var hub = AgentBrainTelemetryHub.Instance;
                    string diagnosis = "gameTime=" + Time.time + "; focus=" + Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance?.FocusedAgentId.Value +
                        "; recorders=" + (hub == null ? "none" : string.Join(" | ", hub.Recorders.Select(r => r.Pawn.AgentIdValue + " leaf=" + r.CurrentLeaf?.StateName +
                        " rounds=" + r.RoundTotal + " lastCount=" + (r.GetRound(0)?.Count ?? -1) + " skip=" + r.LastSkip + " transitions=" + r.TransitionTotal)));
                    CaseArtifactWriter.Trace("brain-panel-scene-wait", diagnosis);
                    Assert.Fail("焦点智能体 60 秒内没有完成至少 3 个候选的评分：" + diagnosis);
                }
                panel.RefreshNow();
                var names = Enumerable.Range(0, AgentBrainPanel.MaxScoreRows).Select(i => Text(panel, "Score_Row_" + i + "_Name"))
                    .Where(label => label.gameObject.activeSelf).Select(label => label.text).ToList();
                CaseArtifactWriter.Trace("brain-panel-scene-scores", string.Join(" / ", names));
                Assert.That(names.Count(name => name.Any(IsHan)), Is.GreaterThanOrEqualTo(names.Count - 1), "正式场景的候选应显示地图上的中文名：" + string.Join(" / ", names));
                AgentPawnRoot agent = recorder.Pawn;
                yield return Capture(panel, agent, "scene-autonomous", Camera.main, overlay.Legend);

                // 指挥员经地图给它下达一条去较远资源群的路线，走起来之后让最近的敌人打它一下，复现"遇袭挂起与反击"；
                // 只截图不判定，真实场景里反击能否接上取决于敌人位置
                var binding = Object.FindObjectOfType<MapGraphBindingAuthoring>();
                Assert.That(binding, Is.Not.Null);
                bool routed = false;
                foreach (var node in binding.TargetBindings.Where(b => b != null && b.DirectTarget is ResourceClusterAuthoring)
                             .OrderByDescending(b => Vector3.Distance(b.DirectTarget.CenterPosition, agent.Position)))
                {
                    var result = Gameplay.Agent.Runtime.AgentCommandRouter.GetOrCreate().TrySubmitRoute(
                        new Gameplay.Agent.Routes.AgentRouteRequest(node.NodeId, Gameplay.Agent.Routes.AgentRouteSource.Player, agent.AgentId));
                    CaseArtifactWriter.Trace("brain-panel-scene-route", node.NodeId + " " + result.Stage + " " + result.Reason);
                    if (!result.Accepted && result.Stage != Gameplay.Agent.Routes.AgentRouteStage.Planning) continue;
                    routed = true;
                    break;
                }
                Assert.That(routed, Is.True, "应能经地图给焦点智能体下达一条路线");
                yield return Frames(2f);
                var enemy = Object.FindObjectsOfType<EnemyHealthController>().Where(e => e.GetCurrentHealthRatio() > 0f)
                    .OrderBy(e => Vector3.Distance(e.transform.position, agent.Position)).FirstOrDefault();
                if (enemy != null)
                {
                    agent.TakeCombatDamage(10, agent.Position, (agent.Position - enemy.transform.position).normalized, enemy.gameObject);
                    float giveUp = Time.time + 5f;
                    while (Time.time < giveUp && recorder.CurrentLeaf?.StateName != "Combat") yield return null;
                    yield return Frames(.3f);
                    yield return Capture(panel, agent, "scene-retaliation", Camera.main, overlay.Legend);
                    giveUp = Time.time + 8f;
                    while (Time.time < giveUp && !Named(panel, "Tree_Focus_Detail").activeSelf && recorder.CurrentLeaf?.StateName == "Combat") yield return null;
                    yield return Capture(panel, agent, "scene-cooldown", Camera.main, overlay.Legend);
                }

                CaseArtifactWriter.Trace("brain-panel-scene", "leaf=" + recorder.CurrentLeaf?.StateName + "; held=" + Text(panel, "Slot_Held_What").text +
                    "; active=" + Text(panel, "Slot_Active_What").text + " " + Text(panel, "Slot_Active_Target").text + "; enemy=" + (enemy != null ? enemy.name : "none"));
                ContractCompleted = true;
            }
            finally
            {
                if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
                if (panel != null) Object.DestroyImmediate(panel.gameObject);
                if (AgentBrainTelemetryHub.Instance != null) Object.DestroyImmediate(AgentBrainTelemetryHub.Instance.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EveryTreeNodeHasAChineseName()
        {
            var english = new List<string>();
            foreach (BehaviorTreeType tree in Trees())
                foreach (var node in tree.Nodes)
                    if (!node.DisplayName.Any(IsHan)) english.Add(tree.Name + "/" + node.NodeName + "=" + node.DisplayName);
            Assert.That(english, Is.Empty, "面板只显示中文节点名");
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PanelCharactersArePrebakedInScienceFonts()
        {
            string required = RequiredCharacters();
            var report = new StringBuilder();
            foreach (string path in new[] { "Assets/Resources/Aerospace/Fonts/ScienceBody.asset", "Assets/Resources/Aerospace/Fonts/ScienceHeading.asset" })
            {
                var baked = new HashSet<uint>(Regex.Matches(File.ReadAllText(path), @"m_Unicode: (\d+)").Cast<Match>().Select(m => uint.Parse(m.Groups[1].Value)));
                Assert.That(baked.Count, Is.GreaterThan(100), path + " 应是文本序列化的字体资源");
                string missing = new string(required.Where(c => !baked.Contains(c)).ToArray());
                if (missing.Length > 0) report.AppendLine(Path.GetFileName(path) + " 缺 " + missing.Length + " 个：" + missing);
            }

            CaseArtifactWriter.Trace("brain-panel-glyphs", "required=" + required.Length + "; " + report);
            Assert.That(report.ToString(), Is.Empty, "面板会显示的字符应全部预烘焙（重建科普界面字体）");
            ContractCompleted = true;
            yield return null;
        }

        // 面板和叠加层代码里的字符串、各棵树的节点名、地图定义里的区域名
        private static string RequiredCharacters()
        {
            var text = new StringBuilder();
            foreach (string folder in new[] { "Assets/Scripts/Gameplay/Agent/Telemetry/UI", "Assets/Scripts/Gameplay/Agent/Telemetry/Overlay" })
                foreach (string file in Directory.GetFiles(folder, "*.cs"))
                    foreach (Match literal in Regex.Matches(File.ReadAllText(file), "\"((?:[^\"\\\\]|\\\\.)*)\""))
                        text.Append(literal.Groups[1].Value);
            foreach (BehaviorTreeType tree in Trees())
                foreach (var node in tree.Nodes)
                    text.Append(node.DisplayName);
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SO_MapGraphDefinition)))
            {
                var map = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (map != null) foreach (var zone in map.Zones) if (zone != null) text.Append(zone.DisplayName);
            }

            return new string(text.ToString().Where(c => c > ' ' && c != '\\').Distinct().OrderBy(c => c).ToArray());
        }

        private static IEnumerable<BehaviorTreeType> Trees()
            => typeof(AgentBrainTrees).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(BehaviorTreeType) && method.GetParameters().Length == 0)
                .Select(method => (BehaviorTreeType)method.Invoke(null, null));

        // 编辑模式测试只能 yield null：按游戏时间等若干帧
        private static IEnumerator Frames(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private static bool IsHan(char c) => c >= 0x4E00 && c <= 0x9FFF;

        private static GameObject Named(AgentBrainPanel panel, string name)
        {
            var match = panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Assert.That(match, Is.Not.Null, "找不到 " + name);
            return match.gameObject;
        }

        private static TMP_Text Text(AgentBrainPanel panel, string name) => Named(panel, name).GetComponent<TMP_Text>();

        // 截图：场景和面板分两次渲染再合成。面板在游戏里是叠加层，画在全部相机效果之后；
        // 若直接让面板走场景相机，后处理和描边效果会压到面板上，看起来比实际透明。
        // 给了场景相机就复制它的机位和设置，否则俯视智能体。叠加层的线画在场景里，图例画布随面板一起走界面相机。
        internal static IEnumerator Capture(AgentBrainPanel panel, AgentPawnRoot agent, string name, Camera source = null, Canvas extra = null)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            const int UiLayer = 30, Width = 1920, Height = 1080;
            var worldObject = new GameObject("Panel evidence world camera");
            var world = worldObject.AddComponent<Camera>();
            var worldData = worldObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (source != null)
            {
                world.CopyFrom(source);
                var sourceData = source.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                worldData.renderPostProcessing = sourceData != null && sourceData.renderPostProcessing;
            }
            else
            {
                world.transform.position = agent.Position + new Vector3(0, 24, -14);
                world.transform.LookAt(agent.Position);
                world.clearFlags = CameraClearFlags.SolidColor;
                world.backgroundColor = new Color(.16f, .2f, .14f);
                worldData.renderPostProcessing = false;
            }
            world.cullingMask &= ~(1 << UiLayer);

            var uiObject = new GameObject("Panel evidence UI camera");
            uiObject.transform.position = new Vector3(0, -500, 0);
            var ui = uiObject.AddComponent<Camera>();
            uiObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            ui.clearFlags = CameraClearFlags.SolidColor;
            ui.backgroundColor = new Color(0, 0, 0, 0);
            ui.cullingMask = 1 << UiLayer;
            ui.nearClipPlane = .01f;
            ui.farClipPlane = 5f;

            var worldTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            worldTarget.Create();
            uiTarget.Create();
            world.targetTexture = worldTarget;
            ui.targetTexture = uiTarget;
            var canvases = new[] { panel.Canvas, extra }.Where(c => c != null).ToArray();
            var transforms = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).ToArray();
            var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            foreach (var t in transforms) t.gameObject.layer = UiLayer;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = ui;
                canvas.planeDistance = 1;
            }
            var worldPixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var uiPixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                yield return null;
                yield return null;
                panel.RefreshNow();
                Canvas.ForceUpdateCanvases();
                world.Render();
                ui.Render();
                RenderTexture.active = worldTarget;
                worldPixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = uiTarget;
                uiPixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = previous;

                // 面板以 SrcAlpha/OneMinusSrcAlpha 画在透明黑底上：颜色已乘过不透明度，透明通道是不透明度的平方
                Color32[] scene = worldPixels.GetPixels32(), overlay = uiPixels.GetPixels32();
                for (int i = 0; i < scene.Length; i++)
                {
                    float coverage = Mathf.Sqrt(overlay[i].a / 255f);
                    scene[i] = new Color32(
                        (byte)Mathf.Min(255, overlay[i].r + scene[i].r * (1 - coverage)),
                        (byte)Mathf.Min(255, overlay[i].g + scene[i].g * (1 - coverage)),
                        (byte)Mathf.Min(255, overlay[i].b + scene[i].b * (1 - coverage)), 255);
                }
                worldPixels.SetPixels32(scene);
                string folder = Path.Combine(TestRunContext.Load().outputPath, "screenshots");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), worldPixels.EncodeToPNG());
                CaseArtifactWriter.Trace("screenshot", name + ".png");
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].gameObject.layer = layers[i];
                world.targetTexture = null;
                ui.targetTexture = null;
                worldTarget.Release();
                uiTarget.Release();
                Object.DestroyImmediate(worldTarget);
                Object.DestroyImmediate(uiTarget);
                Object.DestroyImmediate(worldPixels);
                Object.DestroyImmediate(uiPixels);
                Object.DestroyImmediate(worldObject);
                Object.DestroyImmediate(uiObject);
            }
        }
    }
}
