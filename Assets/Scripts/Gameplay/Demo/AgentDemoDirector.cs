#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using ExtractionLike.Aerospace;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Telemetry;
using Gameplay.Agent.Telemetry.Overlay;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.MapGraph.Binding;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Demo
{
    /// <summary>
    /// 现场导演：在正式场景里真实触发三个演示场景，面板底部字幕逐步讲解，同时写事件记录。
    /// F5、F6、F7 分别开始"自主选目标""遇袭挂起恢复""指挥员改派"，F9 中断。每次先固定随机种子再重新载入当前场景，
    /// 两台智能体回到起点、切好焦点，打开 B 面板和 U 叠加层。导演只走正式入口：地图提交路线、命令路由、焦点切换、敌人察觉。
    /// 只在编辑器和开发构建中存在。
    /// </summary>
    [DefaultExecutionOrder(10020)]
    public sealed class AgentDemoDirector : MonoBehaviour
    {
        public enum Outcome { None, Running, Succeeded, Failed }

        public const int DefaultSeed = 20261020;
        public const string FocusAgentId = "1";
        private static readonly KeyCode[] SceneKeys = { KeyCode.F5, KeyCode.F6, KeyCode.F7 };
        public const KeyCode StopKey = KeyCode.F9;

        private static AgentDemoDirector _instance;

        private Coroutine _routine;
        private double _loadTime;

        public static AgentDemoDirector Instance => _instance;

        /// <summary>每步条件达成后至少停留的秒数（不受暂停影响），给解说留时间</summary>
        public float DwellSeconds { get; set; } = 3f;

        /// <summary>重新载入后等智能体和地图就绪的上限</summary>
        public float ReadySeconds { get; set; } = 30f;

        public bool IsRunning => _routine != null;
        public Outcome LastOutcome { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;
        public string LastLogPath { get; private set; } = string.Empty;
        public int LastLogLines { get; private set; }
        public int StepIndex { get; private set; }
        public AgentDemoRun CurrentRun { get; private set; }

        public static AgentDemoDirector Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("[AgentDemoDirector]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AgentDemoDirector>();
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => Ensure();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public void Run(AgentDemoScene scene, int seed = DefaultSeed)
        {
            Stop();
            _routine = StartCoroutine(RunScene(scene, seed));
        }

        public void Stop()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
            if (CurrentRun != null) Finish(CurrentRun, false, "手动中断");
        }

        private void Update()
        {
            if (!AerospaceUiInputGate.BlocksGameplayInput)
            {
                for (int index = 0; index < SceneKeys.Length; index++)
                    if (Input.GetKeyDown(SceneKeys[index])) Run(AgentDemoScenes.All[index]);
                if (Input.GetKeyDown(StopKey)) Stop();
            }
            CurrentRun?.Tick();
        }

        private void OnDestroy()
        {
            if (CurrentRun != null) Finish(CurrentRun, false, "手动中断");
            if (_instance == this) _instance = null;
        }

        private IEnumerator RunScene(AgentDemoScene scene, int seed)
        {
            LastOutcome = Outcome.Running;
            LastFailure = string.Empty;
            StepIndex = 0;
            var log = new AgentDemoLog(scene.Id);
            LastLogPath = log.FilePath;
            var run = new AgentDemoRun(scene, seed, log);
            CurrentRun = run;
            log.Begin("scene").Field("scene", scene.Id).Field("title", scene.Title).Field("seed", seed)
                .Field("fingerprint", scene.Fingerprint).Field("unity", Application.unityVersion).End();

            // 开局那轮评分发生在智能体注册后的第一帧，遥测要在重新载入前就在
            AgentBrainTelemetryHub.Ensure();
            AgentBrainPanel panel = AgentBrainPanel.Ensure();
            panel.SetOpen(true);
            panel.ShowDemoStep(0, 0, scene.Title, "准备中");

            Random.InitState(seed);
            Scene active = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync(active.buildIndex);
            while (!load.isDone) yield return null;
            _loadTime = Time.timeAsDouble;

            float deadline = Time.unscaledTime + ReadySeconds;
            AgentPawnRoot pawn;
            AgentBrainRecorder recorder;
            MapGraphBindingAuthoring binding;
            while (!TryFindReady(out pawn, out recorder, out binding))
            {
                if (Time.unscaledTime > deadline)
                {
                    Finish(run, false, "场景没有就绪");
                    yield break;
                }
                yield return null;
            }

            AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(pawn.AgentIdValue);
            AgentBrainOverlay.Ensure().SetMode(AgentBrainOverlay.Mode.Focused);
            run.Attach(pawn, recorder, binding);
            log.Begin("staged").Field("agent", pawn.AgentIdValue).Field("x", pawn.Position.x).Field("z", pawn.Position.z).End();

            AgentDemoStep[] steps = scene.Steps;
            for (int index = 0; index < steps.Length; index++)
            {
                AgentDemoStep step = steps[index];
                StepIndex = index + 1;
                panel.ShowDemoStep(index + 1, steps.Length, step.Title, step.Narration);
                log.Begin("step").Field("index", index + 1).Field("title", step.Title).Field("stage", "enter").End();
                run.BeginStep();
                step.Enter?.Invoke(run);

                float started = Time.unscaledTime;
                bool reached = false;
                while (!run.Failed)
                {
                    if (!reached && step.Done(run))
                    {
                        reached = true;
                        log.Begin("step").Field("index", index + 1).Field("title", step.Title).Field("stage", "reached")
                            .Field("seconds", Time.unscaledTime - started).End();
                    }
                    if (reached && Time.unscaledTime - started >= DwellSeconds) break;
                    if (!reached && Time.unscaledTime - started > step.TimeoutSeconds) run.Fail("等待超时：" + step.Title);
                    else yield return null;
                }

                if (run.Failed)
                {
                    Finish(run, false, run.FailureReason);
                    yield break;
                }
            }

            Finish(run, true, string.Empty);
        }

        // 焦点智能体（编号 1，没有时取第一台）已注册、路线已安装、站在导航网格上，地图绑定和记录器都在
        private static bool TryFindReady(out AgentPawnRoot pawn, out AgentBrainRecorder recorder, out MapGraphBindingAuthoring binding)
        {
            pawn = null;
            recorder = null;
            binding = null;
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            AgentBrainTelemetryHub hub = AgentBrainTelemetryHub.Instance;
            if (registry == null || hub == null) return false;
            IReadOnlyList<AgentRuntimeHandle> handles = registry.RegisteredAgents;
            for (int index = 0; index < handles.Count; index++)
            {
                if (!handles[index].IsValid) continue;
                AgentPawnRoot candidate = handles[index].PawnRoot;
                if (pawn == null || candidate.AgentIdValue == FocusAgentId) pawn = candidate;
            }
            if (pawn == null || !pawn.RouteSnapshot.IsInstalled || !AgentNavigationQuery.IsReady(pawn.NavMeshAgent)) return false;
            binding = FindObjectOfType<MapGraphBindingAuthoring>();
            recorder = hub.Find(pawn);
            return binding != null && recorder != null;
        }

        private void Finish(AgentDemoRun run, bool succeeded, string reason)
        {
            AgentDemoLog log = run.Log;
            WriteTimeline(run);
            log.Begin("outcome").Field("result", succeeded ? "succeeded" : "failed").Field("reason", reason).Field("step", StepIndex).End();
            run.Detach();
            LastLogLines = log.LineCount;
            log.Dispose();
            LastOutcome = succeeded ? Outcome.Succeeded : Outcome.Failed;
            LastFailure = reason;
            CurrentRun = null;
            _routine = null;
            if (!succeeded && AgentBrainPanel.Instance != null) AgentBrainPanel.Instance.ShowDemoStep(0, 0, "演示中断", reason);
        }

        // 面板时间线里本次载入之后的条目：状态转移、指令事件、自主选中
        private void WriteTimeline(AgentDemoRun run)
        {
            AgentBrainRecorder recorder = run.Recorder;
            if (recorder == null) return;
            for (int back = recorder.TimelineCount - 1; back >= 0; back--)
            {
                AgentBrainTimelineEntry entry = recorder.GetTimeline(back);
                if (entry.TimeSeconds < _loadTime) continue;
                AgentDemoLog line = run.Log.Begin("timeline").Field("time", entry.TimeSeconds).Field("entry", entry.Kind.ToString());
                switch (entry.Kind)
                {
                    case AgentBrainTimelineKind.Transition:
                        line.Field("from", entry.From != null ? entry.From.StateName : string.Empty)
                            .Field("to", entry.To != null ? entry.To.StateName : string.Empty)
                            .Field("transition", entry.Transition != null ? entry.Transition.Name : string.Empty);
                        break;
                    case AgentBrainTimelineKind.Directive:
                        line.Field("directive", entry.Directive.DirectiveType.ToString()).Field("stage", entry.Stage.ToString())
                            .Field("command", entry.Directive.CommandId).Field("failure", entry.Failure.ToString());
                        break;
                    default:
                        line.Field("target", entry.Target != null ? entry.Target.name : string.Empty).Field("score", entry.Score);
                        break;
                }
                line.End();
            }
        }
    }
}
#endif
