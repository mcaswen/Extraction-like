using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using Gameplay.Agent.Telemetry;
using Gameplay.Agent.Telemetry.Overlay;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.Demo;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    /// <summary>
    /// 现场导演：三个场景在正式场景里各连续成功 3 次，每次都写出完整的事件记录（场景头、三步达成、结局）；
    /// 第一次运行到最后一步时截一张图（面板、字幕和叠加层同时打开）。导演布景后按 4 倍速运行，每步停留缩短到半秒。
    /// </summary>
    public sealed class AgentDemoDirectorTests : ReproductionTestFixture
    {
        private const int Runs = 3;

        [UnityTest]
        public IEnumerator AutonomousSelectionSucceedsThreeTimes() => RunRepeatedly(AgentDemoScenes.AutonomousSelection, "demo-autonomous-selection");

        [UnityTest]
        public IEnumerator AmbushResumeSucceedsThreeTimes() => RunRepeatedly(AgentDemoScenes.AmbushResume, "demo-ambush-resume");

        [UnityTest]
        public IEnumerator CommanderReassignSucceedsThreeTimes() => RunRepeatedly(AgentDemoScenes.CommanderReassign, "demo-commander-reassign");

        private IEnumerator RunRepeatedly(AgentDemoScene scene, string screenshot)
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            var director = AgentDemoDirector.Ensure();
            director.DwellSeconds = .5f;
            director.TimeScale = 4f;
            try
            {
                for (int run = 1; run <= Runs; run++)
                {
                    director.Run(scene);
                    bool captured = run > 1;
                    float deadline = Time.realtimeSinceStartup + 150f;
                    while (director.IsRunning && Time.realtimeSinceStartup < deadline)
                    {
                        if (!captured && director.StepIndex == scene.Steps.Length && director.CurrentRun != null)
                        {
                            captured = true;
                            yield return BrainPanelTests.Capture(AgentBrainPanel.Instance, director.CurrentRun.Pawn, screenshot, Camera.main,
                                AgentBrainOverlay.Instance != null ? AgentBrainOverlay.Instance.Legend : null);
                        }
                        else yield return null;
                    }

                    string[] lines = File.Exists(director.LastLogPath) ? File.ReadAllLines(director.LastLogPath) : new string[0];
                    CaseArtifactWriter.Trace("demo-" + scene.Id + "-" + run, "outcome=" + director.LastOutcome + "; failure=" + director.LastFailure +
                        "; step=" + director.StepIndex + "; log=" + director.LastLogPath + "; lines=" + lines.Length + "; last=" + lines.LastOrDefault());
                    Assert.That(director.IsRunning, Is.False, scene.Title + " 第 " + run + " 次没在时限内结束");
                    Assert.That(director.LastOutcome, Is.EqualTo(AgentDemoDirector.Outcome.Succeeded), scene.Title + " 第 " + run + " 次失败：" + director.LastFailure);
                    Assert.That(lines.First(), Does.Contain("\"kind\":\"scene\"").And.Contain(scene.Fingerprint));
                    Assert.That(lines.Count(l => l.Contains("\"kind\":\"step\"") && l.Contains("\"stage\":\"reached\"")), Is.EqualTo(scene.Steps.Length));
                    Assert.That(lines.Any(l => l.Contains("\"kind\":\"timeline\"")), Is.True, "事件记录应包含面板时间线");
                    Assert.That(lines.Last(), Does.Contain("\"result\":\"succeeded\""));
                }
                ContractCompleted = true;
            }
            finally
            {
                Time.timeScale = 1f;
                if (director != null) Object.DestroyImmediate(director.gameObject);
                if (AgentBrainOverlay.Instance != null) Object.DestroyImmediate(AgentBrainOverlay.Instance.gameObject);
                if (AgentBrainPanel.Instance != null) Object.DestroyImmediate(AgentBrainPanel.Instance.gameObject);
                if (AgentBrainTelemetryHub.Instance != null) Object.DestroyImmediate(AgentBrainTelemetryHub.Instance.gameObject);
            }
        }
    }
}
